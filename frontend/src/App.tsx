import { useEffect, useState } from 'react';
import { request, streamGraph } from './api';
import { GraphCanvas } from './GraphCanvas';
import type { Graph, Investigation } from './types';

const sourceLabel: Record<string, string> = { local: 'Investigação local', filesystem: 'Sistema de arquivos', 'simulated-agent': 'Agente simulado' };

export function App() {
  const [token, setToken] = useState(() => sessionStorage.getItem('aim-token') ?? '');
  const [connected, setConnected] = useState(false);
  const [repository, setRepository] = useState('');
  const [investigations, setInvestigations] = useState<Investigation[]>([]);
  const [graph, setGraph] = useState<Graph | null>(null);
  const [activeId, setActiveId] = useState('');
  const [name, setName] = useState('Investigação de pedidos');
  const [path, setPath] = useState('src/app.module.ts');
  const [discovery, setDiscovery] = useState('');
  const [nodeId, setNodeId] = useState('');
  const [edgeId, setEdgeId] = useState('');
  const [status, setStatus] = useState('Aguardando');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function run(action: () => Promise<void>) {
    setBusy(true); setError('');
    try { await action(); } catch (e) { setError(e instanceof Error ? e.message : 'Não foi possível concluir a operação.'); }
    finally { setBusy(false); }
  }
  async function refreshList() { setInvestigations(await request<Investigation[]>('/api/investigations', token)); }
  async function connect() {
    const config = await request<{ root: string }>('/api/bootstrap', token);
    sessionStorage.setItem('aim-token', token); setRepository(config.root); setConnected(true);
    await refreshList();
  }
  useEffect(() => { if (sessionStorage.getItem('aim-token')) void run(connect); }, []);
  useEffect(() => {
    if (!activeId || !connected) return;
    const controller = new AbortController(); setStatus('Conectando…');
    void streamGraph(activeId, token, controller.signal, next => {
      if (controller.signal.aborted) return;
      setGraph(old => old?.id === next.id && old.revision > next.revision ? old : next);
    }, setStatus);
    return () => controller.abort();
  }, [activeId, token, connected]);

  async function open(id: string) {
    const next = await request<Graph>(`/api/investigations/${id}`, token);
    setGraph(next); setActiveId(id); setNodeId(''); setEdgeId('');
  }
  async function create() {
    const next = await request<Graph>('/api/investigations', token, { name, path });
    setGraph(next); setActiveId(next.id); setNodeId(''); setEdgeId(''); await refreshList();
  }
  async function investigate(file: string, source = 'local') {
    if (!graph) return;
    const next = await request<Graph>(`/api/investigations/${graph.id}/events`, token, { eventId: crypto.randomUUID(), path: file, source });
    setGraph(old => old?.id === next.id && old.revision > next.revision ? old : next); await refreshList();
  }
  const node = graph?.nodes.find(n => n.id === nodeId);
  const edge = graph?.edges.find(e => e.id === edgeId);
  const label = (id: string) => graph?.nodes.find(n => n.id === id)?.label ?? id;

  return <div className="app">
    <header className="topbar"><div className="brand"><span className="brand-symbol">A</span><strong>AIM</strong><span className="brand-description">Architecture Investigation Map</span></div><span className="local-badge">● Local · POC</span></header>
    {!connected ? <main className="login"><span className="eyebrow">INVESTIGAÇÃO DE ARQUITETURA</span><h1>Entenda o caminho<br/>de uma dependência.</h1><p>Explore um módulo por vez. Cada relação leva até a evidência no código, e o mapa acompanha suas descobertas.</p>
      <form onSubmit={e => { e.preventDefault(); void run(connect); }}><label>Token local<input type="password" value={token} onChange={e => setToken(e.target.value)} autoComplete="off" required /></label><button className="primary" disabled={busy}>Conectar</button></form>
      <small>Use o token gerado pelo comando de inicialização. A análise acontece neste computador.</small>{error && <p className="error" role="alert">{error}</p>}
    </main> : <div className="workspace">
      <aside className="sidebar"><div className="section-heading">WORKSPACE <span>01</span></div><div className="repository"><span>Raiz autorizada</span><strong>{repository.split(/[\\/]/).at(-1)}</strong><small title={repository}>{repository}</small></div>
        <div className="section-heading">INVESTIGAÇÕES <span>{investigations.length}</span></div>
        <nav aria-label="Investigações">{investigations.length === 0 && <p className="muted">Comece pelo módulo relacionado ao problema.</p>}{investigations.map(i => <button key={i.id} className={`investigation-link ${i.id === activeId ? 'active' : ''}`} disabled={busy} onClick={() => void run(() => open(i.id))}><span aria-hidden="true">◇</span>{i.name}</button>)}</nav>
        <form className="new-investigation" onSubmit={e => { e.preventDefault(); void run(create); }}><h3>Nova investigação</h3><label>Nome da investigação<input value={name} onChange={e => setName(e.target.value)} required maxLength={100}/></label><label>Arquivo inicial<input value={path} onChange={e => setPath(e.target.value)} required placeholder="src/app.module.ts" /></label><button className="primary" disabled={busy}>Iniciar investigação</button></form>
        <div className="sidebar-footer"><span>Somente arquivos selecionados</span><small>Sem análise global do repositório.</small><button className="text-button" onClick={() => { sessionStorage.removeItem('aim-token'); setToken(''); setConnected(false); setGraph(null); setActiveId(''); }}>Desconectar</button></div>
      </aside>
      <main className="main-panel">
        <div className="page-heading"><div><span className="eyebrow">MAPA DE DEPENDÊNCIAS</span><h1>{graph?.name ?? 'Sua próxima investigação'}</h1><p>{graph ? 'Amplie o mapa com descobertas apoiadas no código.' : 'Selecione um arquivo para começar a construir o mapa.'}</p></div>{graph && <span className={`live-status ${status === 'Ao vivo' ? 'live' : ''}`} data-testid="stream-status">{status}</span>}</div>
        {error && <div role="alert" className="error">{error}</div>}
        {graph ? <><div className="graph-toolbar"><span data-testid="graph-summary">{graph.nodes.length} componentes · {graph.edges.length} {graph.edges.length === 1 ? 'relação' : 'relações'}</span><span>Revisão {graph.revision} <b>·</b> {graph.files.length} arquivos analisados</span></div>
          <div className="graph-and-details"><section className="canvas" aria-label="Grafo da investigação"><GraphCanvas key={graph.id} graph={graph} onNode={id => { setNodeId(id); setEdgeId(''); }} onEdge={id => { setEdgeId(id); setNodeId(''); }} />
            <div className="graph-legend"><span className="legend-solid"/> Analisado <span className="legend-dashed"/> Referenciado</div></section>
            <aside className="inspector"><span className="eyebrow">INSPECIONAR</span>
              {edge ? <div data-testid="evidence"><h2>Evidência da relação</h2><p className="relation-title">{label(edge.source)} → {label(edge.target)}</p><span className="verified">✓ Declaração verificada na fonte</span><p className="file-reference">{edge.evidence.path}:{edge.evidence.line}:{edge.evidence.column}</p><pre>{edge.evidence.snippet}</pre><dl><dt>Tipo</dt><dd>{edge.kind}</dd><dt>Confiança</dt><dd>Sintaxe estática</dd><dt>SHA-256 da fonte</dt><dd className="hash">{edge.evidence.contentHash}</dd></dl><p className="muted">Comprova a referência declarada. Não comprova comportamento em execução.</p></div>
              : node ? <><h2>{node.label}</h2><p className="file-reference">{node.path}</p><span className="verified">{node.verification === 'analyzed' ? '✓ Módulo analisado' : 'Referência ainda não analisada'}</span><p>Investigue este arquivo para descobrir suas dependências diretas.</p><button className="primary" disabled={busy} onClick={() => void run(() => investigate(node.path))}>Investigar este arquivo</button></>
              : <><h2>Do mapa ao código</h2><p>Selecione um componente para ampliar a investigação ou uma relação para conferir sua origem.</p><div className="inspector-note">Cada seta contém uma referência de arquivo, posição e versão da fonte.</div></>}
              <div className="relations"><h3>Relações {node ? 'do componente' : 'do mapa'}</h3>{graph.edges.filter(e => !node || e.source === node.id || e.target === node.id).map(e => <button key={e.id} aria-label={`Ver evidência ${label(e.source)} → ${label(e.target)}`} onClick={() => { setEdgeId(e.id); setNodeId(''); }}><span>{label(e.source)} <b>→</b> {label(e.target)}</span><small>{e.evidence.path}:{e.evidence.line}</small></button>)}{graph.edges.length === 0 && <p className="muted">Nenhuma relação estática encontrada.</p>}</div>
            </aside></div>
          {graph.diagnostics.length > 0 && <details className="diagnostics" open><summary>Limites e diagnósticos ({graph.diagnostics.length})</summary>{graph.diagnostics.map((d, i) => <p key={i}>{d}</p>)}</details>}
          <section className="activity" data-testid="history"><div><span className="eyebrow">HISTÓRICO</span><h2>Descobertas recentes</h2></div><div className="history-items">{graph.history.slice(-5).reverse().map(h => <div className="history-item" key={h.eventId}><span className="history-dot"/><div><strong>{sourceLabel[h.source] ?? h.source}</strong><span>{h.path}</span></div><small>r{h.revision} · {new Date(h.at).toLocaleTimeString('pt-BR')}</small></div>)}</div></section>
          <form className="discovery" onSubmit={e => { e.preventDefault(); void run(() => investigate(discovery)); }}><label>Explorar outro arquivo<input value={discovery} onChange={e => setDiscovery(e.target.value)} placeholder="src/audit.module.ts" required /></label><button disabled={busy || !discovery}>Investigar arquivo</button><button type="button" disabled={busy || !discovery} onClick={() => void run(() => investigate(discovery, 'simulated-agent'))}>Simular descoberta de agente</button><small>Simulação local. Hooks Codex/Claude ainda não conectados.</small></form>
        </> : <section className="empty-state"><span>◇ → ◇</span><h2>Um ponto de partida. Um mapa em evolução.</h2><p>A fixture incluída começa em <code>src/app.module.ts</code>.<br/>Inicie a investigação no painel à esquerda.</p></section>}
      </main>
    </div>}
  </div>;
}
