import type { Graph } from './types';
const base = import.meta.env.VITE_API_URL ?? 'http://127.0.0.1:5187';

export async function request<T>(path: string, token: string, data?: unknown): Promise<T> {
  const response = await fetch(base + path, {
    method: data === undefined ? 'GET' : 'POST',
    headers: { Authorization: `Bearer ${token}`, ...(data === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: data === undefined ? undefined : JSON.stringify(data)
  });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new Error(error?.error ?? (response.status === 401 ? 'Token inválido. Conecte novamente.' : `Falha HTTP ${response.status}`));
  }
  return response.json();
}

export async function streamGraph(id: string, token: string, signal: AbortSignal, onGraph: (graph: Graph) => void, onState: (state: string) => void) {
  while (!signal.aborted) {
    try {
      const response = await fetch(`${base}/api/investigations/${id}/stream`, { headers: { Authorization: `Bearer ${token}` }, signal });
      if (response.status === 401 || response.status === 403) { onState('Acesso recusado'); return; }
      if (!response.ok || !response.body) throw new Error('Stream indisponível');
      onState('Ao vivo');
      const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
      let buffer = '';
      try {
        while (!signal.aborted) {
          const { value, done } = await reader.read();
          if (done) break;
          buffer += value;
          let end;
          while ((end = buffer.indexOf('\n\n')) >= 0) {
            const frame = buffer.slice(0, end); buffer = buffer.slice(end + 2);
            const data = frame.split('\n').find(line => line.startsWith('data: '));
            if (data) onGraph(JSON.parse(data.slice(6)) as Graph);
          }
        }
      } finally { await reader.cancel().catch(() => {}); reader.releaseLock(); }
    } catch { if (signal.aborted) return; }
    if (signal.aborted) return;
    onState('Reconectando…');
    await new Promise<void>(resolve => {
      const finish = () => { clearTimeout(timer); signal.removeEventListener('abort', finish); resolve(); };
      const timer = setTimeout(finish, 1500);
      signal.addEventListener('abort', finish, { once: true });
    });
  }
}
