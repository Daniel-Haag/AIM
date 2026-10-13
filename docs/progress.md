# Registro de execução — docs/plan.md

- Especificação aceita: o próprio anexo; não há arquivo externo pendente.
- Ambiente: .NET SDK 10.0.400, Node 24.13.1, npm 11.8.0.
- Diretório novo e isolado C:/Projetos/AIM, branch poc-foundation.
- Execução direta autorizada pelo usuário, sem novas aprovações intermediárias de desenho/plano.
- Contratos compartilhados: AST JSON → modelos de snapshot → projeção e SQLite → API/SSE → UI. Um mesmo formato camelCase será usado em toda a fronteira.
- Tarefa 1: implementada extração AST, fixture NestJS válida e testes de evidência, alias, sintaxe, dinâmica, caminhos e links.
- Tarefa 2: implementados projetos de domínio/aplicação/infraestrutura, SQLite transacional, serviço de ingestão, watcher e reconciliação incremental.
- Tarefa 3: implementados API autenticada, stream SSE com reconexão, UI React Flow em português, histórico e evidências. Fluxo de navegador passou após correção de CSS e nome acessível de botão.
- Tarefa 4: scripts start/stop/restart verificados, sem token nos logs; README, ADRs e limites de integração documentados.
- Revisão independente: quatro P2 confirmados (casing Windows, spread sobrescrevendo imports, invalidação de fonte recusada, alvo criado sem evento). Reproduzidos com testes antes das correções.
- Correções: caminhos canônicos e identidade consistente, metadados ambíguos diagnosticados, snapshots inválidos removem arestas, candidatos inexistentes persistidos e considerados no fingerprint. Reconciler extraído para teste real sem FileSystemWatcher.
- Proteção adicional: aliases de diretório Windows terminados em ponto/espaço recusados em ambas as fronteiras, com regressões observadas antes da correção.
- Dependência transitiva SQLite vulnerável detectada no primeiro restore; atualização para Microsoft.Data.Sqlite 10.0.13 resolveu a auditoria. Lockfiles npm/NuGet para reprodutibilidade.
- Validação final: scripts/test.ps1 exit 0; 11 testes AST, 18 .NET e 3 Playwright; build API sem avisos/erros, build UI concluído e fixture com typecheck válido. Scripts de demonstração restauraram bytes originais. Resultados detalhados em docs/validation.md.
- Entrega preservada na branch local poc-foundation. Sem remote/push/publicação, pois o escopo solicitado é local.
