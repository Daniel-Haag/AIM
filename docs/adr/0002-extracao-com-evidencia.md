# ADR 0002 — AST TypeScript e escopo explícito

Status: aceito para POC.

Usar API do compilador TypeScript 5.9, fixada no lockfile, em processo Node local conhecido. Não carregar tsconfig plugins nem executar módulos investigados. Rejeitar sintaxe inválida; imports dinâmicos ficam como diagnóstico, sem relação inferida.

Extrair apenas referências relativas usadas no array `imports` de uma classe com decorador `Module` importado de `@nestjs/common`. Um alvo referenciado não é anunciado como classe analisada. Evidência da relação comprova a declaração sintática, não semântica ou comportamento em runtime.

Cada arquivo é investigado explicitamente. O watcher reconcilia o conjunto já investigado com hash de conteúdo, sem varrer fontes externas ao escopo. Fontes removidas ou inválidas invalidam relações anteriores. Custo: chamadas ao parser em subprocesso; adequadas à POC, considerar worker persistente ao medir necessidade.

Referência: [TypeScript Compiler API](https://github.com/microsoft/TypeScript/wiki/Using-the-Compiler-API).

Após revisão: caminhos existentes usam casing canônico; a identidade dos nós no Windows é insensível ao casing do caminho, preservando o nome do símbolo. Diretórios Windows com arquivos distintos apenas por casing são recusados pelo backend. Metadados com spreads, propriedades computadas ou imports repetidos não produzem arestas. Candidatos relativos ainda inexistentes entram no fingerprint para que reconciliação periódica detecte sua criação mesmo sem notificação do sistema operacional. Fontes que excedem limites ou ficam inacessíveis invalidam o snapshot, sem ler o conteúdo recusado.
