import ts from 'typescript';
import { readFileSync, existsSync, lstatSync, realpathSync } from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';

// No repository code, tsconfig plugins, package scripts or loaders are executed.
function validateRelative(relative) {
  if (typeof relative !== 'string' || !relative || relative.length > 500 ||
      path.isAbsolute(relative) || /^[a-z]:/i.test(relative) || relative.includes('\\') || /[<>:"|?*\u0000]/.test(relative))
    throw new Error('Caminho relativo inválido');
  const parts = relative.split('/');
  if (parts.some(p => !p || p === '..' || p.startsWith('.') || p.endsWith('.') || p.endsWith(' ') ||
      ['node_modules', 'dist', 'bin', 'obj', 'secrets'].includes(p.toLowerCase())) || !relative.endsWith('.ts'))
    throw new Error('Caminho de fonte não permitido');
}

export function safePath(root, relative) {
  validateRelative(relative);
  const parts = relative.split('/');
  let current = path.resolve(root);
  // Check ancestors too: the configured root itself must not traverse a junction.
  for (let ancestor = current; ; ancestor = path.dirname(ancestor)) {
    if (lstatSync(ancestor).isSymbolicLink()) throw new Error('Links não são permitidos');
    if (path.dirname(ancestor) === ancestor) break;
  }
  for (const part of parts) {
    current = path.join(current, part);
    if (existsSync(current) && lstatSync(current).isSymbolicLink()) throw new Error('Links não são permitidos');
  }
  if (existsSync(current)) {
    const canonical = realpathSync(current);
    const rel = path.relative(realpathSync(root), canonical);
    if (rel.startsWith('..') || path.isAbsolute(rel)) throw new Error('Fonte fora da raiz');
    if (!lstatSync(current).isFile() || lstatSync(current).size > 1024 * 1024) throw new Error('Fonte excede limite ou não é arquivo');
  }
  return current;
}

export function extract(root, relative) {
  const absolute = safePath(root, relative);
  if (!existsSync(absolute)) return { path: relative, nodes: [], edges: [], diagnostics: ['Arquivo removido ou inexistente'], references: [] };
  const canonical = absolutePath => path.relative(realpathSync.native(root), realpathSync.native(absolutePath)).split(path.sep).join('/');
  relative = canonical(absolute);
  const identity = (filePath, symbol) => `${process.platform === 'win32' ? filePath.toLowerCase() : filePath}#${symbol}`;
  const source = readFileSync(absolute, 'utf8');
  const contentHash = createHash('sha256').update(source).digest('hex');
  const file = ts.createSourceFile(relative, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);
  if (file.parseDiagnostics.length) throw new Error('Erro de sintaxe na fonte; corrija o arquivo e tente novamente');
  const imports = new Map();
  const decorators = new Set();
  const nodes = new Map();
  const edges = new Map();
  const diagnostics = [];
  const references = new Set();
  for (const statement of file.statements) {
    if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier)) continue;
    const bindings = statement.importClause?.namedBindings;
    if (!bindings || !ts.isNamedImports(bindings) || statement.importClause.isTypeOnly) continue;
    for (const binding of bindings.elements) {
      if (binding.isTypeOnly) continue;
      const symbol = binding.propertyName?.text ?? binding.name.text;
      imports.set(binding.name.text, { symbol, specifier: statement.moduleSpecifier.text });
      if (statement.moduleSpecifier.text === '@nestjs/common' && symbol === 'Module') decorators.add(binding.name.text);
    }
  }
  for (const statement of file.statements) {
    if (!ts.isClassDeclaration(statement) || !statement.name) continue;
    for (const decorator of ts.getDecorators(statement) ?? []) {
      const call = decorator.expression;
      if (!ts.isCallExpression(call) || !ts.isIdentifier(call.expression) || !decorators.has(call.expression.text)) continue;
      const id = identity(relative, statement.name.text);
      nodes.set(id, { id, label: statement.name.text, path: relative, kind: 'module', verification: 'analyzed' });
      const metadata = call.arguments[0];
      if (!metadata || !ts.isObjectLiteralExpression(metadata)) {
        diagnostics.push('Metadados dinâmicos de @Module não resolvidos'); continue;
      }
      if (metadata.properties.some(p => ts.isSpreadAssignment(p) || (p.name && ts.isComputedPropertyName(p.name))) ||
          metadata.properties.filter(p => p.name?.getText(file).replace(/['"]/g, '') === 'imports').length > 1) {
        diagnostics.push('Metadados com sobrescrita, spread ou propriedade computada não resolvidos'); continue;
      }
      for (const property of metadata.properties) {
        if (!ts.isPropertyAssignment(property) || property.name.getText(file).replace(/['"]/g, '') !== 'imports') continue;
        if (!ts.isArrayLiteralExpression(property.initializer)) {
          diagnostics.push('Array imports dinâmico não resolvido'); continue;
        }
        for (const element of property.initializer.elements) {
          const ref = ts.isIdentifier(element) ? imports.get(element.text) : undefined;
          if (!ref || !ref.specifier.startsWith('.')) {
            diagnostics.push(`Import não resolvido na linha ${file.getLineAndCharacterOfPosition(element.getStart(file)).line + 1}`);
            continue;
          }
          const base = path.posix.normalize(path.posix.join(path.posix.dirname(relative), ref.specifier));
          const candidates = base.endsWith('.ts') ? [base] : [`${base}.ts`, `${base}/index.ts`];
          let targetPath;
          for (const candidate of candidates) {
            try {
              validateRelative(candidate);
              references.add(candidate);
              const targetFile = safePath(root, candidate);
              if (existsSync(targetFile)) { targetPath = canonical(targetFile); break; }
            }
            catch { /* Unsafe paths never become graph relationships. */ }
          }
          if (!targetPath) { diagnostics.push(`Fonte do import não resolvida: ${ref.symbol}`); continue; }
          const target = identity(targetPath, ref.symbol);
          if (!nodes.has(target)) nodes.set(target, { id: target, label: ref.symbol, path: targetPath, kind: 'module-reference', verification: 'referenced' });
          const position = file.getLineAndCharacterOfPosition(element.getStart(file));
          const edgeId = `${id}->${target}:imports`;
          if (!edges.has(edgeId)) edges.set(edgeId, {
            id: edgeId, source: id, target, kind: 'imports', confidence: 'static-syntax', verification: 'source-verified',
            evidence: { path: relative, line: position.line + 1, column: position.character + 1, snippet: element.getText(file), contentHash }
          });
        }
      }
    }
  }
  if (!nodes.size) diagnostics.push('Nenhuma classe com @Module importado de @nestjs/common foi encontrada');
  return { path: relative, nodes: [...nodes.values()], edges: [...edges.values()], diagnostics, references: [...references] };
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  try { process.stdout.write(JSON.stringify(extract(process.argv[2], process.argv[3]))); }
  catch (error) { process.stderr.write(error.message); process.exitCode = 1; }
}
