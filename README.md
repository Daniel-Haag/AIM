# AIM — Architecture Investigation Map

POC local para investigar dependências de módulos NestJS e ampliar um grafo com evidências rastreáveis. Backend .NET 10 + SQLite, extrator AST TypeScript, React Flow e atualizações SSE autenticadas.

## Iniciar no Windows

Pré-requisitos: PowerShell 7, .NET SDK 10 e Node.js 24. A primeira instalação baixa pacotes públicos; a aplicação opera localmente depois disso.

```powershell
cd C:\Projetos\AIM
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
npm ci
.\scripts\start.ps1
```

Abra http://127.0.0.1:5173 e cole o token exibido **somente no seu terminal**. API: `127.0.0.1:5187`. Logs dos serviços e SQLite ficam em `.local/`, ignorada pelo Git. O token permanece em memória e no sessionStorage da aba; desconectar remove-o do navegador. Não compartilhe o token. Nenhuma conta de IA é necessária.

O script usa `Start-Process -WindowStyle Hidden`, verifica disponibilidade dos serviços e registra apenas seus PIDs/horários, sem token. Para encerrar:

```powershell
.\scripts\stop.ps1
```

O banco é preservado. Para investigar outro repositório, encerre a sessão e use `start.ps1 -Repository 'C:\caminho\projeto-nest'`. Somente essa raiz fica autorizada; investigações de outra raiz não aparecem na API. O script de demonstração altera apenas a fixture incluída.

## Demonstração reproduzível

1. Conecte usando o token e inicie uma investigação em `src/app.module.ts`.
2. O grafo mostra **AppModule → OrdersModule**. A relação tem evidência na linha 4 de `src/app.module.ts`.
3. Selecione OrdersModule e clique **Investigar este arquivo**. Aparece **OrdersModule → UsersModule**.
4. Execute `.\scripts\demo.ps1`. Ele faz backup da fixture e adiciona **OrdersModule → AuditModule**. Observe o grafo mudar sem recarregar a página.
5. Selecione a relação ou seu botão no painel direito para ver arquivo, linha, coluna, trecho e SHA-256.
6. Execute `.\scripts\demo.ps1 -Action Restore`. A relação adicionada desaparece automaticamente.
7. Para demonstrar ingestão simulada, informe `src/audit.module.ts` em **Explorar outro arquivo** e use **Simular descoberta de agente**. Essa origem é identificada no histórico quando há uma nova descoberta. Repetir análise sem alterações não aumenta a revisão.

O watcher compara conteúdo somente de arquivos já investigados. Alterações de arquivos não explorados não ampliam o mapa sozinhas. Uma reconciliação a cada cinco segundos recupera notificações perdidas; o fluxo usual responde ao FileSystemWatcher com debounce.

## Testes

```powershell
npx playwright install chromium
.\scripts\test.ps1
```

Executa testes do extrator, typecheck da fixture, xUnit, builds e Playwright com API/UI reais. Testes E2E usam uma cópia temporária da fixture, banco separado e portas 5188/5175; não alteram o seu banco. Screenshot gerada em `test-results/aim-poc.png`.

## Organização

| Pasta | Responsabilidade |
|---|---|
| `src/Aim.Domain` | Componentes, relações, evidências e projeção do grafo |
| `src/Aim.Application` | Casos de uso, validação de caminhos e contratos |
| `src/Aim.Infrastructure` | SQLite, processo AST, watcher e notificações |
| `src/Aim.Api` | API HTTP, autorização e SSE |
| `analyzer` | Extração sintática sem executar o projeto analisado |
| `frontend` | Interface React/TypeScript e React Flow |
| `fixtures/nest` | Módulos NestJS pequenos e reproduzíveis |
| `tests` | Testes de backend e navegador |
| `docs` | Especificação fornecida, arquitetura, plano, ADRs e validação |

## API local

Todos os endpoints `/api` exigem `Authorization: Bearer <token>`; `/health` informa apenas disponibilidade. Requisições de navegador aceitam somente a origem configurada.

| Método / caminho | Uso |
|---|---|
| GET `/api/bootstrap` | Raiz autorizada e configuração mínima |
| GET/POST `/api/investigations` | Listar ou criar com `{name,path}` |
| GET `/api/investigations/{id}` | Grafo, evidências, diagnósticos e histórico |
| POST `/api/investigations/{id}/events` | Analisar `{eventId,path,source}` |
| GET `/api/investigations/{id}/stream` | SSE; snapshot atual no início e após mudanças |

`source` externo: `local` ou `simulated-agent`; `filesystem` é reservado ao watcher. UUID repetido não altera estado novamente. Não existe endpoint para executar comandos, enviar fontes ou escrever no repositório.

Configuração por ambiente: `AIM_TOKEN`, `AIM_REPOSITORY`, `AIM_DATABASE`, `AIM_ANALYZER`, `AIM_NODE`, `AIM_PORT`, `AIM_UI_ORIGIN`. UI: `VITE_API_URL`. Os scripts preenchem os valores padrão. Para iniciar manualmente, configure essas variáveis e rode `dotnet run --project src/Aim.Api --no-launch-profile` e `npm run dev` em terminais separados.

## Limites da POC

- Extrai apenas referências relativas estáticas no array `imports` de `@Module`. Não resolve aliases de tsconfig, barrel reexports, `forwardRef`, módulos dinâmicos, DI ou chamadas de serviços.
- “Verificada” significa declaração sintática com evidência. Referências ainda não analisadas são marcadas; não há promessa de semântica ou execução válida do alvo.
- Até 64 arquivos por investigação, 1 MiB por fonte, 10 segundos por chamada ao parser. A raiz deve estar acessível sem links/junctions.
- Estado atual e últimos 200 eventos que alteraram conteúdo são visíveis; não há replay temporal completo nem navegação C4 hierárquica nesta POC.
- Snapshot inteiro é persistido e transmitido após cada alteração. Adequado ao limite da POC; normalização e deltas dependem de medição futura.
- Não há servidor MCP ou hooks de agentes ativos. Há contratos de integração, documentação verificada e eventos simulados. Consulte [integrações](docs/integrations.md).
- Proteção local não isola processos maliciosos sob o mesmo usuário; veja [ADR de segurança](docs/adr/0003-transporte-e-seguranca.md).
- Sem publicação, cloud, upload automático, telemetria da aplicação ou execução de código investigado.

## Próximos marcos

1. Adaptador MCP STDIO via SDK oficial, testado com Codex e Claude Code reais.
2. Resolução semântica opt-in de aliases/reexports e evidências de DI, com testes dedicados.
3. Hierarquia C4, escopos e navegação entre níveis.
4. Histórico temporal completo, migrações SQLite e medição de desempenho em repositórios maiores.

A [especificação original](docs/specification.txt), o [desenho](docs/design.md) e os [ADRs](docs/adr) registram o escopo e as decisões. Resultados observados ficam em [validação](docs/validation.md).
