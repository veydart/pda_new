import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Users } from 'lucide-react';
import { playersApi, type PlayerSearchHit, type PagedResult } from '../services/api';

export default function PlayersPage() {
    const [search, setSearch] = useState('');
    const [result, setResult] = useState<PagedResult<PlayerSearchHit> | null>(null);
    const [loading, setLoading] = useState(false);
    const [page, setPage] = useState(1);
    const navigate = useNavigate();

    const doSearch = (p = 1) => {
        if (!search.trim()) return;
        setLoading(true);
        setPage(p);
        playersApi.search(search.trim(), p, 20)
            .then(setResult)
            .finally(() => setLoading(false));
    };

    return (
        <div className="fade-in">
            <div className="page-header">
                <h1 className="page-title"><Users size={24} /> Игроки</h1>
                <p className="page-subtitle">Поиск по никнейму или SteamID</p>
            </div>

            <div className="search-container" style={{ marginBottom: 24 }}>
                <Search className="search-icon" />
                <input
                    className="search-input"
                    placeholder="Введите никнейм или SteamID..."
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    onKeyDown={e => e.key === 'Enter' && doSearch(1)}
                />
            </div>

            {loading && <div className="loading-container"><div className="spinner" /></div>}

            {result && !loading && (
                <div className="card" style={{ padding: 0 }}>
                    <div style={{ padding: '16px 24px', borderBottom: '1px solid var(--border-subtle)', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
                        Найдено: <strong style={{ color: 'var(--text-primary)' }}>{result.totalCount}</strong> игроков
                    </div>
                    <table className="data-table">
                        <thead><tr><th>Никнейм</th><th>SteamID</th><th>Инстанс</th></tr></thead>
                        <tbody>
                            {result.items.map(p => (
                                <tr key={`${p.steamId}-${p.sourceInstance}`} style={{ cursor: 'pointer' }} onClick={() => navigate(`/players/${p.steamId}`)}>
                                    <td style={{ fontWeight: 600 }}>{p.nickname || '—'}</td>
                                    <td className="steam-id">{p.steamId}</td>
                                    <td><span className="instance-badge">{p.sourceInstance}</span></td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                    {result.totalCount > 20 && (
                        <div className="pagination">
                            <button disabled={page <= 1} onClick={() => doSearch(page - 1)}>← Назад</button>
                            <span className="page-info">Стр. {page} / {Math.ceil(result.totalCount / 20)}</span>
                            <button disabled={!result.hasMore} onClick={() => doSearch(page + 1)}>Далее →</button>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}
