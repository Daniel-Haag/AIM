export type Component = { id: string; label: string; path: string; kind: string; verification: string };
export type Evidence = { path: string; line: number; column: number; snippet: string; contentHash: string };
export type Dependency = { id: string; source: string; target: string; kind: string; confidence: string; verification: string; evidence: Evidence };
export type Graph = {
  id: string; name: string; revision: number; nodes: Component[]; edges: Dependency[];
  files: string[]; diagnostics: string[];
  history: { eventId: string; path: string; source: string; at: string; revision: number }[];
};
export type Investigation = { id: string; name: string; revision: number };
