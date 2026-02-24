import { useEffect, useState, useCallback, useRef } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { GitBranch } from 'lucide-react';
import ForceGraph2D from 'react-force-graph-2d';
import { messagesApi, type GraphData } from '../services/api';

interface GraphNode {
    id: string;
    name: string;
    isCenter: boolean;
    val: number;
}

interface GraphLink {
    source: string;
    target: string;
    weight: number;
}

export default function GraphPage() {
    const [params] = useSearchParams();
    const initSteamId = params.get('steamId') || '';
    const [steamId, setSteamId] = useState(initSteamId);
    const [graphData, setGraphData] = useState<{ nodes: GraphNode[]; links: GraphLink[] } | null>(null);
    const [loading, setLoading] = useState(false);
    const [depth, setDepth] = useState(1);
    const containerRef = useRef<HTMLDivElement>(null);
    const navigate = useNavigate();

    const loadGraph = useCallback((sid: string) => {
        if (!sid.trim()) return;
        setLoading(true);
        messagesApi.getGraph(sid.trim(), depth, 60)
            .then((data: GraphData) => {
                const nodes: GraphNode[] = data.nodes.map(n => ({
                    id: n.steamId,
                    name: n.nickname || n.steamId.slice(-6),
                    isCenter: n.isCenter,
                    val: n.isCenter ? 20 : 8,
                }));
                const links: GraphLink[] = data.edges.map(e => ({
                    source: e.source,
                    target: e.target,
                    weight: e.weight,
                }));
                setGraphData({ nodes, links });
            })
            .finally(() => setLoading(false));
    }, [depth]);

    useEffect(() => {
        if (initSteamId) loadGraph(initSteamId);
    }, [initSteamId, loadGraph]);

    const handleNodeClick = useCallback((node: GraphNode) => {
        navigate(`/players/${node.id}`);
    }, [navigate]);

    return (
        <div className="fade-in">
            <div className="page-header">
                <h1 className="page-title"><GitBranch size={24} /> Граф связей</h1>
                <p className="page-subtitle">Визуализация переписок между игроками</p>
            </div>

            <div className="card" style={{ marginBottom: 20 }}>
                <div style={{ display: 'flex', gap: 12, alignItems: 'end' }}>
                    <div style={{ flex: 1 }}>
                        <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>SteamID центрального игрока</label>
                        <input className="search-input" style={{ paddingLeft: 16 }} placeholder="76561198..." value={steamId} onChange={e => setSteamId(e.target.value)} />
                    </div>
                    <div>
                        <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Глубина</label>
                        <div className="tabs" style={{ marginBottom: 0 }}>
                            <button className={`tab ${depth === 1 ? 'active' : ''}`} onClick={() => setDepth(1)}>1</button>
                            <button className={`tab ${depth === 2 ? 'active' : ''}`} onClick={() => setDepth(2)}>2</button>
                        </div>
                    </div>
                    <button className="btn btn-primary" onClick={() => loadGraph(steamId)}>Построить</button>
                </div>
            </div>

            {loading && <div className="loading-container"><div className="spinner" /><span style={{ color: 'var(--text-muted)' }}>Строим граф...</span></div>}

            {graphData && !loading && (
                <div className="graph-container" ref={containerRef}>
                    <ForceGraph2D
                        graphData={graphData}
                        width={containerRef.current?.clientWidth || 900}
                        height={500}
                        backgroundColor="#0f1520"
                        nodeLabel={(node: GraphNode) => `${node.name}\n${node.id}`}
                        nodeColor={(node: GraphNode) => node.isCenter ? '#38bdf8' : '#a78bfa'}
                        nodeRelSize={5}
                        nodeVal={(node: GraphNode) => node.val}
                        linkColor={() => 'rgba(56, 189, 248, 0.15)'}
                        linkWidth={(link: GraphLink) => Math.min(Math.max(link.weight / 20, 0.5), 6)}
                        linkDirectionalParticles={2}
                        linkDirectionalParticleWidth={(link: GraphLink) => Math.min(link.weight / 30, 3)}
                        linkDirectionalParticleColor={() => '#38bdf8'}
                        onNodeClick={(_node) => handleNodeClick(_node as GraphNode)}
                        nodeCanvasObject={(node: GraphNode, ctx: CanvasRenderingContext2D, globalScale: number) => {
                            const n = node as GraphNode & { x: number; y: number };
                            const label = n.name;
                            const fontSize = (n.isCenter ? 14 : 11) / globalScale;
                            const r = (n.isCenter ? 8 : 5);

                            // Node circle
                            ctx.beginPath();
                            ctx.arc(n.x, n.y, r, 0, 2 * Math.PI);
                            ctx.fillStyle = n.isCenter ? '#38bdf8' : '#a78bfa';
                            ctx.fill();

                            // Glow
                            if (n.isCenter) {
                                ctx.beginPath();
                                ctx.arc(n.x, n.y, r + 3, 0, 2 * Math.PI);
                                ctx.strokeStyle = 'rgba(56, 189, 248, 0.3)';
                                ctx.lineWidth = 2 / globalScale;
                                ctx.stroke();
                            }

                            // Label
                            ctx.font = `${n.isCenter ? '600' : '400'} ${fontSize}px Inter, sans-serif`;
                            ctx.fillStyle = '#e2e8f0';
                            ctx.textAlign = 'center';
                            ctx.textBaseline = 'top';
                            ctx.fillText(label, n.x, n.y + r + 3);
                        }}
                    />
                </div>
            )}

            {graphData && !loading && (
                <div style={{ marginTop: 12, fontSize: '0.82rem', color: 'var(--text-muted)' }}>
                    {graphData.nodes.length} узлов • {graphData.links.length} связей • Клик по узлу → профиль игрока
                </div>
            )}
        </div>
    );
}
