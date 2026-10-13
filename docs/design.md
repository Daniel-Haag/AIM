# AIM — desenho da primeira POC

Fonte de verdade: conteúdo integral do anexo fornecido na conversa, preservado em `specification.txt`. Não há documento externo obrigatório.

## Objetivo e requisitos

Investigar um módulo de um projeto NestJS local e ampliar um mapa conforme descobertas, sem varrer o repositório. Extrair relações reais, evitar duplicações, persistir investigações e evidências, refletir mudanças sem atualizar a página. Não executar o código investigado. Nenhuma dependência de nuvem ou agente para operar a POC.

## Decisões

Monólito modular .NET 10 (SDK disponível), projetos Domain, Application, Infrastructure e Api. SQLite persiste snapshots por arquivo e histórico de eventos em transação. React/TypeScript e React Flow exibem o grafo. Um processo Node fixo usa TypeScript 5.9 AST para extrair `@Module({ imports: [...] })`, sem executar JavaScript do repositório. A alternativa regex foi descartada por confundir comentários e sintaxe; embutir um parser próprio em C# adicionaria manutenção desnecessária.

Uma única raiz de repositório é autorizada por configuração de inicialização. Cada investigação começa por um arquivo relativo escolhido e só amplia seu escopo por ação explícita. Limite de 64 arquivos investigados, 1 MiB por fonte. Imports relativos estáticos e aliases de importação são suportados; aliases tsconfig, reexports e módulos dinâmicos serão diagnosticados, sem inventar relações.

Relações registram arquivo, linha, coluna, trecho e SHA-256 da versão da fonte. A verificação significa evidência sintática, não execução ou confirmação de comportamento em runtime. Nós apenas referenciados distinguem-se de nós analisados.

Eventos identificados por UUID atualizam um snapshot de arquivo; substituição remove relações obsoletas. A revisão cresce somente quando muda o conteúdo observado. FileSystemWatcher agenda reconciliação dos arquivos já investigados; SSE autenticado envia snapshots atualizados. Reconciliar também no início e periodicamente recupera eventos perdidos. A POC não faz análise global nem resolução semântica completa.

## Segurança

Serviços em 127.0.0.1, token obrigatório, CORS restrito, validação de Host e Origin. Token nunca vai na query HTTP nem nos logs. Caminhos relativos sem traversal, links simbólicos/junctions e diretórios sensíveis recusados. Apenas `.ts` autorizados; nenhum comando arbitrário. Backend inicia somente o extrator fixo com ArgumentList e timeout. Fonte sensível em código continua sendo responsabilidade do operador: use apenas repositório autorizado.

## Integrações e limites

Contratos independentes de agente para eventos e futuras ferramentas MCP. Eventos simulados identificados explicitamente. Documentação oficial de Codex e Claude Code será registrada; não declarar hooks ou MCP implementados/validados sem teste com os clientes reais.

## Aceitação

Testes reais de AST, remoção/duplicação, persistência/reabertura SQLite, eventos repetidos, traversal, autenticação e fluxo de navegador. Demonstração: AppModule → OrdersModule; expandir OrdersModule → UsersModule; alterar fixture para adicionar AuditModule; observar atualização por SSE e consultar evidência. Documentar execução e limitações.
