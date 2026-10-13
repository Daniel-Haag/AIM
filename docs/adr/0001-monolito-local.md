# ADR 0001 — monólito modular local

Status: aceito para POC.

API, aplicação, domínio e infraestrutura em projetos separados. SQLite local evita serviço de banco adicional. Não há nuvem, telemetria da aplicação ou processamento por LLM. Custo: um único processo coordenador e uma raiz por execução; não é serviço multiusuário.

Snapshots por fonte permitem substituição idempotente e remoção de relações antigas. IDs determinísticos são caminho relativo e símbolo. Transação SQLite armazena revisão e deduplicação de evento de forma atômica. Histórico visível limitado aos últimos 200 eventos que alteraram o grafo; IDs processados preservados no banco.
