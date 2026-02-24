import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search as SearchIcon } from 'lucide-react';
import { searchApi, type SearchResult } from '../services/api';

export default function SearchPage() {
    const [query, setQuery] = useState('');
    const [result, setResult] = useState<SearchResult | null>(null);
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();

    const doSearch = () => {
        if (query.trim().length < 2) return;
        setLoading(true);
        searchApi.omniSearch(query.trim())
            .then(setResult)
            .finally(() => setLoading(false));
    };

    const formatDate = (iso: string) => new Date(iso).toLocaleString('ru-RU');

    return (
        <div className="fade-in">
            <div className="page-header">
                <h1 className="page-title"><SearchIcon size={24} /> Omni-Search</h1>
                <p className="page-subtitle">Поиск по игрокам, PDA-аккаунтам и тексту сообщений</p>
            </div>

            <div className="search-container" style={{ marginBottom: 24 }}>
                <SearchIcon className="search-icon" />
                <input
                    className="search-input"
                    placeholder="Никнейм, SteamID, логин PDA или текст сообщения..."
                    value={query}
                    onChange={e => setQuery(e.target.value)}
                    onKeyDown={e => e.key === 'Enter' && doSearch()}
                />
            </div>

            {loading && <div className="loading-container"><div className="spinner" /></div>}

            {result && !loading && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
                    {/* Players */}
                    {result.players.length > 0 && (
                        <div className="card">
                            <div className="card-header"><h2 className="card-title">👤 Игроки ({result.players.length})</h2></div>
                            <table className="data-table">
                                <thead><tr><th>Никнейм</th><th>SteamID</th><th>Инстанс</th></tr></thead>
                                <tbody>
                                    {result.players.map(p => (
                                        <tr key={`${p.steamId}-${p.sourceInstance}`} style={{ cursor: 'pointer' }} onClick={() => navigate(`/players/${p.steamId}`)}>
                                            <td style={{ fontWeight: 600 }}>{p.nickname || '—'}</td>
                                            <td className="steam-id">{p.steamId}</td>
                                            <td><span className="instance-badge">{p.sourceInstance}</span></td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    {/* PDA Accounts */}
                    {result.pdaAccounts.length > 0 && (
                        <div className="card">
                            <div className="card-header"><h2 className="card-title">🔑 PDA Аккаунты ({result.pdaAccounts.length})</h2></div>
                            <table className="data-table">
                                <thead><tr><th>Логин</th><th>SteamID</th><th>Инстанс</th></tr></thead>
                                <tbody>
                                    {result.pdaAccounts.map(a => (
                                        <tr key={`${a.sourceId}-${a.sourceInstance}`} style={{ cursor: 'pointer' }} onClick={() => navigate(`/players/${a.steamId}`)}>
                                            <td style={{ fontWeight: 600, color: 'var(--accent-purple)' }}>{a.login}</td>
                                            <td className="steam-id">{a.steamId}</td>
                                            <td><span className="instance-badge">{a.sourceInstance}</span></td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    {/* Messages */}
                    {result.messages.length > 0 && (
                        <div className="card" style={{ padding: 0 }}>
                            <div style={{ padding: '16px 24px', borderBottom: '1px solid var(--border-subtle)' }}>
                                <h2 className="card-title">💬 Сообщения ({result.messages.length})</h2>
                            </div>
                            <div className="message-feed">
                                {result.messages.map(msg => (
                                    <div className="message-item" key={msg.id}>
                                        <div className="message-meta">
                                            <span className="message-time">{formatDate(msg.sentAt)}</span>
                                            <span className="message-sender" onClick={() => msg.senderSteamId && navigate(`/players/${msg.senderSteamId}`)}>
                                                {msg.senderLogin || msg.senderSteamId || '?'}
                                            </span>
                                            {msg.receiverLogin && (
                                                <span className="message-receiver">
                                                    → <span onClick={() => msg.receiverSteamId && navigate(`/players/${msg.receiverSteamId}`)}>{msg.receiverLogin}</span>
                                                </span>
                                            )}
                                        </div>
                                        <div className="message-body">
                                            <span className="message-text">{msg.message}</span>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}

                    {result.players.length === 0 && result.pdaAccounts.length === 0 && result.messages.length === 0 && (
                        <div className="empty-state">Ничего не найдено по запросу «{query}»</div>
                    )}
                </div>
            )}
        </div>
    );
}
