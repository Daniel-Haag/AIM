import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, symlinkSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { extract, safePath } from './extract.mjs';

const fixture = resolve('fixtures/nest');
test('extrai dependência Nest real com linha, trecho e hash rastreáveis', () => {
  const result = extract(fixture, 'src/app.module.ts');
  assert.equal(result.edges.length, 1);
  assert.equal(result.edges[0].source, 'src/app.module.ts#AppModule');
  assert.equal(result.edges[0].target, 'src/orders.module.ts#OrdersModule');
  assert.equal(result.edges[0].evidence.line, 4);
  assert.equal(result.edges[0].evidence.snippet, 'OrdersModule');
  assert.match(result.edges[0].evidence.contentHash, /^[a-f0-9]{64}$/);
});
function withSources(source, check) {
  const root = mkdtempSync(join(tmpdir(), 'aim-ast-'));
  try {
    mkdirSync(join(root, 'src'));
    writeFileSync(join(root, 'src/b.ts'), 'export class B {}');
    writeFileSync(join(root, 'src/a.ts'), source);
    check(root);
  } finally { rmSync(root, { recursive: true, force: true }); }
}
test('reconhece alias de Module e import sem duplicar relações', () => {
  withSources("import { Module as M } from '@nestjs/common';\nimport { B as Local } from './b';\n@M({ imports: [Local, Local] }) export class A {}", root => {
    const r = extract(root, 'src/a.ts');
    assert.equal(r.edges.length, 1);
    assert.equal(r.edges[0].target, 'src/b.ts#B');
  });
});
test('não inventa relações para comentários, chamadas dinâmicas ou imports não resolvidos', () => {
  withSources("import { Module } from '@nestjs/common';\n// @Module({imports: [Fake]})\n@Module({ imports: [Factory.make(), Unknown] }) export class A {}", root => {
    const r = extract(root, 'src/a.ts');
    assert.equal(r.edges.length, 0);
    assert.equal(r.diagnostics.length, 2);
  });
});
test('retira relação quando a declaração é removida', () => {
  withSources("import { Module } from '@nestjs/common'; @Module({}) export class A {}", root => {
    assert.equal(extract(root, 'src/a.ts').edges.length, 0);
  });
});
test('recusa traversal, caminhos absolutos e arquivos sensíveis', () => {
  for (const path of ['../outside.ts', '/etc/a.ts', 'C:/secret.ts', '.env', 'node_modules/a.ts']) {
    assert.throws(() => extract(fixture, path));
  }
});
test('sintaxe quebrada não produz grafo válido', () => {
  withSources('@Module({ imports: [', root => assert.throws(() => extract(root, 'src/a.ts'), /sintaxe/i));
});

test('casing no Windows mantém identidade canônica do arquivo e de seus imports', { skip: process.platform !== 'win32' }, () => {
  assert.deepEqual(extract(fixture, 'SRC/app.module.ts'), extract(fixture, 'src/app.module.ts'));
});

test('metadados sobrescritos por spread ou propriedade computada não geram relação presumida', () => {
  for (const extra of [", ...{ imports: [] }", ", ['imports']: []", ', imports: []']) {
    withSources(`import { Module } from '@nestjs/common'; import { B } from './b'; @Module({imports:[B]${extra}}) export class A {}`, root => {
      const result = extract(root, 'src/a.ts');
      assert.equal(result.edges.length, 0);
      assert.ok(result.diagnostics.length > 0);
    });
  }
});

test('guarda candidatos inexistentes para recuperar criação perdida de alvo', () => {
  withSources("import { Module } from '@nestjs/common'; import { Missing } from './missing'; @Module({imports:[Missing]}) export class A {}", root => {
    const result = extract(root, 'src/a.ts');
    assert.equal(result.edges.length, 0);
    assert.deepEqual(result.references, ['src/missing.ts', 'src/missing/index.ts']);
  });
});

test('recusa junctions sem ler a fonte através delas', () => {
  withSources('export class A {}', root => {
    symlinkSync(join(root, 'src'), join(root, 'linked'), process.platform === 'win32' ? 'junction' : 'dir');
    assert.throws(() => extract(root, 'linked/a.ts'), /Links/);
  });
});

test('recusa aliases Windows com ponto ou espaço ao fim de diretório protegido', () => {
  for (const relative of ['secrets./a.ts', 'node_modules /a.ts', 'src./app.module.ts'])
    assert.throws(() => safePath(fixture, relative), /permitido|inválido/);
});
