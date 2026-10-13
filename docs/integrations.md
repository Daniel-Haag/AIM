# Integrações com agentes — estado verificado em 13/10/2026

## Capacidades documentadas

- Codex documenta clientes MCP por STDIO e HTTP, incluindo configuração de token Bearer por variável de ambiente: [documentação oficial](https://developers.openai.com/codex/mcp).
- Codex documenta `PostToolUse`, incluindo ferramentas de shell, `apply_patch` e MCP. Disponibilidade e aprovação dos hooks dependem da configuração e da superfície usada: [hooks oficiais](https://developers.openai.com/codex/hooks).
- Claude Code documenta servidores MCP locais por STDIO e transportes remotos: [MCP](https://code.claude.com/docs/en/mcp).
- Claude Code documenta `PostToolUse` após sucesso de uma ferramenta. O formato varia por ferramenta; observar `Edit|Write` não cobre alterações feitas por shell ou outros processos: [hooks](https://code.claude.com/docs/en/hooks).

## O que esta POC entrega

`IInvestigationCommands` define os casos de uso para um futuro adaptador MCP. A API autenticada aceita eventos independentes de fornecedor:

```json
{
  "eventId": "8dd6f870-2381-4c3d-9c44-3b5c9777b9ca",
  "path": "src/orders.module.ts",
  "source": "simulated-agent"
}
```

Origens admitidas: `local`, `simulated-agent`, `filesystem`. O endpoint externo admite somente as duas primeiras; o watcher usa a última internamente. O evento solicita análise; não pode criar uma relação arbitrária, enviar código ou executar comandos. O backend relê a fonte autorizada e calcula a evidência.

Contrato proposto para ferramentas MCP: `aim_start_investigation(name,path)`, `aim_investigate_file(investigationId,eventId,path)` e `aim_get_graph(investigationId)`. O adaptador deverá delegar aos mesmos casos de uso e aplicar autorização de raiz.

## Não implementado / não validado

Não há servidor MCP nem hooks Codex/Claude instalados por esta POC. Nenhuma configuração pessoal de agentes foi alterada. Não foi executado teste ponta a ponta com um desses clientes. Os testes de simulação não demonstram integração automática real.

Próximo marco: implementar STDIO com SDK MCP oficial e testar separadamente com as versões locais dos dois clientes. Depois, mapear eventos documentados por ferramenta, minimizar payloads e validar deduplicação e confiança. Não coletar transcrições nem prompt completo.
