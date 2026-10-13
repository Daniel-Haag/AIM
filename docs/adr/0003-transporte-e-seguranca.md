# ADR 0003 — API local autenticada e SSE

Status: aceito para POC.

Escuta em loopback, Bearer obrigatório, Origin/Host validados, raiz autorizada no startup. Token aleatório é fornecido ao operador; não é persistido pelo backend nem escrito em logs. UI guarda token somente em sessionStorage. Requisições e stream incluem Authorization. CORS permite apenas a origem explícita da UI.

SSE com fetch permite cabeçalho de autenticação, reconexão e envio inicial do snapshot persistido. Canais de capacidade 1 coalescem notificações; cada mensagem entrega o estado atual, não um delta que possa se perder.

A POC não é fronteira de isolamento contra outro processo malicioso executando com o mesmo usuário. Links e traversal são recusados, porém proteção contra troca adversarial de arquivos entre validação/leitura (TOCTOU) exige handles específicos por sistema operacional e é um marco futuro.
