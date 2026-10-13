import { useEffect } from 'react';
import { ReactFlow, Background, Controls, MiniMap, MarkerType, Position, useNodesState, type Node, type Edge } from '@xyflow/react';
import type { Graph } from './types';

function layout(graph: Graph): Node[] {
  const depths = new Map<string, number>();
  const roots = graph.nodes.filter(n => !graph.edges.some(e => e.target === n.id));
  const queue = roots.map(n => ({ id: n.id, depth: 0 }));
  for (let i = 0; i < queue.length; i++) {
    const current = queue[i];
    if (depths.has(current.id)) continue;
    depths.set(current.id, current.depth);
    for (const edge of graph.edges.filter(e => e.source === current.id))
      if (!depths.has(edge.target)) queue.push({ id: edge.target, depth: current.depth + 1 });
  }
  const rows = new Map<number, number>();
  return graph.nodes.map(n => {
    const depth = depths.get(n.id) ?? 0;
    const row = rows.get(depth) ?? 0; rows.set(depth, row + 1);
    return { id: n.id, sourcePosition: Position.Right, targetPosition: Position.Left, position: { x: depth * 280, y: row * 140 }, className: n.verification === 'analyzed' ? 'analyzed' : 'referenced',
      data: { label: <div className="node-content"><span className="node-kind">{n.verification === 'analyzed' ? 'MÓDULO ANALISADO' : 'REFERÊNCIA'}</span><strong>{n.label}</strong><span className="node-path">{n.path}</span></div> } };
  });
}

export function GraphCanvas({ graph, onNode, onEdge }: { graph: Graph; onNode: (id: string) => void; onEdge: (id: string) => void }) {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  useEffect(() => {
    setNodes(previous => layout(graph).map(node => ({ ...node, position: previous.find(n => n.id === node.id)?.position ?? node.position })));
  }, [graph, setNodes]);
  const edges: Edge[] = graph.edges.map(e => ({ id: e.id, source: e.source, target: e.target, label: 'imports',
    markerEnd: { type: MarkerType.ArrowClosed, color: '#668b87' }, style: { stroke: '#668b87', strokeWidth: 1.7 },
    labelStyle: { fill: '#44645f', fontSize: 11 }, labelBgStyle: { fill: '#f4f7f5' } }));
  return <ReactFlow nodes={nodes} edges={edges} onNodesChange={onNodesChange} fitView fitViewOptions={{ padding: 0.25 }}
    minZoom={0.2} maxZoom={2} nodesConnectable={false} onNodeClick={(_, node) => onNode(node.id)} onEdgeClick={(_, edge) => onEdge(edge.id)}
    onPaneClick={() => onNode('')} proOptions={{ hideAttribution: false }}>
    <Background color="#d4dfd9" gap={22} size={1} /><Controls showInteractive={false} /><MiniMap nodeColor="#9fc4b2" maskColor="rgba(246,249,246,.7)" />
  </ReactFlow>;
}
