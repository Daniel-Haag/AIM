# Validação executada — 13/10/2026

Ambiente: Windows, PowerShell 7, .NET SDK 10.0.400, Node 24.13.1, npm 11.8.0. Comando final: `scripts/test.ps1`, exit code 0.

## Resultados

| Verificação | Resultado observado |
|---|---|
| `npm test` | 11/11 testes AST passaram |
| `npm run check --workspace fixtures/nest` | TypeScript da fixture compilou sem erros |
| `dotnet test Aim.slnx` | 18/18 testes passaram |
| `dotnet build src/Aim.Api/Aim.Api.csproj` | Sucesso, 0 avisos e 0 erros |
| `npm run build` | Build de produção da UI concluído |
| `npm run test:e2e` | 3/3 testes Playwright passaram, API e navegador reais |
| `scripts/start.ps1` / `stop.ps1` | Serviços iniciados e encerrados; health/API/UI acessíveis |
| Reinício via scripts | Investigação e relação preservadas no SQLite |
| Inspeção dos logs locais | Token não encontrado nos logs dos serviços |
| `scripts/demo.ps1`, Add + Restore | Adicionou AuditModule e restaurou exatamente o hash original da fixture |
| Auditoria npm de dependências de produção | Nenhuma vulnerabilidade reportada |
| Auditoria NuGet incluindo transitivas | Nenhum pacote vulnerável reportado nas fontes consultadas |

Total: **32 testes automatizados passaram**, sem testes ignorados neste ambiente. Os checks de scripts e auditorias acima são adicionais.

O build Vite emitiu aviso da dependência React Flow sobre a diretiva `use client`, ignorada no bundle client-side. Playwright/Node emitiu aviso de conflito NO_COLOR/FORCE_COLOR. Nenhum impediu compilação ou execução; não foram ocultados.

## Critérios de aceitação

| Critério da especificação | Evidência |
|---|---|
| Backend inicia | Smoke dos scripts e processo real no Playwright |
| Frontend inicia | Vite real acessado pelo navegador Chromium |
| Fixture NestJS analisada | AST real e typecheck contra `@nestjs/common` |
| Dependência real extraída | AppModule → OrdersModule, declaração na linha 4 |
| Relação visível no grafo | Teste seleciona nó React Flow e inspeciona evidência |
| Atualização incremental | Expansão OrdersModule e adição/remoção de AuditModule |
| Sem refresh manual | Teste edita a fonte no disco e aguarda mudança na mesma página por SSE |
| Evidência rastreável | Arquivo, linha, coluna, trecho e SHA-256; painel validado no navegador |
| Testes passam | 11 AST + 18 .NET + 3 E2E |
| Reprodução local | README, fixture, lockfiles e scripts start/test/demo/stop |

## Cobertura importante

- AST: import real e alias, comentários, construções dinâmicas, sintaxe inválida, remoção, traversal, junctions, casing Windows, spreads/propriedades computadas, candidatos ainda inexistentes e aliases de diretórios Windows.
- .NET: projeção/deduplicação, remoção de relações, SQLite reaberto, eventos duplicados e concorrentes, validação de caminho/proveniência, casing, limites e recuperação do reconciliador usando o processo AST real.
- Reconciliador: arquivo inicialmente inexistente criado sem nenhum FileSystemWatcher; fonte acima de 1 MiB invalida evidência sem lê-la e recupera quando corrigida.
- E2E: autenticação/Origin/traversal, abertura/expansão/seleção, evidências, eventos simulados idempotentes, watcher/SSE, recarga da interface, exclusão, sintaxe inválida e recuperação.

## Revisão independente e correções

Uma revisão somente leitura encontrou quatro P2: casing duplicava IDs; spread podia sobrescrever imports; fonte recusada mantinha evidência antiga; criação perdida de alvo não resolvido não era recuperada. Testes reproduziram as falhas antes das correções. A suíte final cobre os quatro casos. A revisão não foi tratada como prova de teste, nem se alegou integração externa real.

## Limitações e não validado

MCP e hooks Codex/Claude **não implementados nem testados com clientes reais**. Eventos de agente são simulados. Não foram feitos testes de escala empresarial, análise semântica geral, navegação C4 completa ou auditoria de isolamento contra processos maliciosos com o mesmo usuário. Essas capacidades não são alegadas na entrega.

Prévia capturada no navegador durante o teste: [aim-poc.png](aim-poc.png).
