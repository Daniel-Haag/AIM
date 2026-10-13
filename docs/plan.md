# AIM POC Implementation Plan

> Execução: superpowers:executing-plans, nesta sessão, conforme pedido de autonomia.

**Goal:** entregar investigação incremental local de NestJS com evidência e grafo vivo.
**Architecture:** monólito .NET com contratos de domínio/aplicação, infraestrutura SQLite/AST/watcher e API autenticada. UI React Flow consome eventos SSE.
**Tech Stack:** .NET 10, SQLite, Node 24, TypeScript 5.9, React, React Flow, Vite, xUnit, Playwright.
**Spec:** `specification.txt` e `design.md`.

## Restrições globais

Sem nuvem, telemetria, upload de fontes, comandos externos arbitrários ou alterações em outros repositórios. Análise incremental, idempotente e sempre fundamentada em fonte. Separar simulação de integração com agentes reais.

## Foco da revisão

- Imports removidos devem remover arestas antigas (testes domínio e navegador).
- Eventos repetidos e concorrentes não duplicam grafo/histórico (SQLite e serviço).
- Caminhos fora da raiz e links não podem ler arquivos (extrator/segurança).
- Sintaxe inválida ou dinâmica não pode virar relação presumida (AST).
- Reconexão/reinício preservam grafo e retomam observação (persistência e SSE).

## Tarefa 1 — extração e fixture

Arquivos: `analyzer/extract.mjs`, `analyzer/extract.test.mjs`, `fixtures/nest/src/*.ts`.
Interface: `extract(root, relativePath)` produz `{path,nodes,edges,diagnostics}`; IDs path#symbol, evidência com hash/linha.
- [x] Escrever testes de import real, alias, comentário, dinâmica, remoção e traversal; observar falha.
- [x] Implementar AST restrita e fixture NestJS válida; executar `npm test`.

## Tarefa 2 — domínio, armazenamento e ingestão

Arquivos: `src/Aim.Domain/*`, `src/Aim.Application/*`, `src/Aim.Infrastructure/*`, `tests/Aim.Tests/*`.
Interfaces: `GraphProjection.Build(snapshots)`, `IExtractor.ExtractAsync(path, ct)`, `IInvestigationStore.Get/Save/List`, `InvestigationService.IngestAsync(id,event,ct)`.
- [x] Testar substituição de snapshots, deduplicação e reabertura SQLite; observar falha.
- [x] Implementar modelos, contratos, validação, persistência transacional, revisão, processo AST e watcher com reconciliação.
- [x] Executar `dotnet test` e corrigir falhas.

## Tarefa 3 — API e interface

Arquivos: `src/Aim.Api/Program.cs`, `frontend/src/*`, `tests/e2e/*`.
Interface HTTP: bootstrap, listar/criar investigações, grafo, eventos e SSE sob `/api` com Bearer.
- [x] Escrever teste de fluxo real: criar → ampliar → editar arquivo → SSE → evidência → remoção; autenticação/traversal.
- [x] Implementar endpoints e UI em português, seleção de nós/arestas, status e histórico.
- [x] Executar build e Playwright contra serviços reais.

## Tarefa 4 — entrega

Arquivos: `scripts/*.ps1`, `README.md`, `docs/adr/*`, `docs/integrations.md`, `docs/validation.md`.
- [x] Scripts locais de iniciar/parar, teste e demonstração reproduzível.
- [x] Verificar capacidades documentadas de Codex/Claude, registrar limitações.
- [x] Revisão final, correções necessárias e nova execução dos testes afetados.
