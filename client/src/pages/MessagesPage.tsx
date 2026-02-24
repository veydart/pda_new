import { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { MessageSquare } from 'lucide-react';
import { messagesApi, type MessageDto, type PagedResult } from '../services/api';

export default function MessagesPage() {
    const [params] = useSearchParams();
    const steamId1 = params.get('steamId1') || '';
    const steamId2 = params.get('steamId2') || '';

    const [s1, setS1] = useState(steamId1);
    const [s2, setS2] = useState(steamId2);
    const [result, setResult] = useState<PagedResult<MessageDto> | null>(null);
    const [loading, setLoading] = useState(false);
    const [page, setPage] = useState(1);
    const navigate = useNavigate();

    useEffect(() => {
        if (steamId1 && steamId2) {
            loadConversation(steamId1, steamId2, 1);
        }
    }, [steamId1, steamId2]);

    const loadConversation = (id1: string, id2: string, p: number) => {
        if (!id1.trim() || !id2.trim()) return;
        setLoading(true);
        setPage(p);
        messagesApi.getBetween(id1.trim(), id2.trim(), p, 50)
            .then(setResult)
            .finally(() => setLoading(false));
    };

    const formatTime = (iso: string) => new Date(iso).toLocaleString('ru-RU');

    return (
        <div className="fade-in">
            <div className="page-header">
                <h1 className="page-title"><MessageSquare size={24} /> Переписка</h1>
                <p className="page-subtitle">Просмотр истории сообщений между двумя игроками</p>
            </div>

            <div className="card" style={{ marginBottom: 24 }}>
                <div style={{ display: 'flex', gap: 12, alignItems: 'end' }}>
                    <div style={{ flex: 1 }}>
                        <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>SteamID Игрока 1</label>
                        <input className="search-input" style={{ paddingLeft: 16 }} placeholder="76561198..." value={s1} onChange={e => setS1(e.target.value)} />
                    </div>
                    <div style={{ flex: 1 }}>
                        <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>SteamID Игрока 2</label>
                        <input className="search-input" style={{ paddingLeft: 16 }} placeholder="76561198..." value={s2} onChange={e => setS2(e.target.value)} />
                    </div>
                    <button className="btn btn-primary" onClick={() => loadConversation(s1, s2, 1)}>Найти</button>
                </div>
            </div>

            {loading && <div className="loading-container"><div className="spinner" /></div>}

            {result && !loading && (
                <div className="card" style={{ padding: 0 }}>
                    <div style={{ padding: '16px 24px', borderBottom: '1px solid var(--border-subtle)', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
                        Найдено: <strong style={{ color: 'var(--text-primary)' }}>{result.totalCount}</strong> сообщений
                    </div>
                    <div className="message-feed">
                        {result.items.map(msg => (
                            <div className="message-item" key={msg.id}>
                                <div className="message-meta">
                                    <span className="message-time">{formatTime(msg.sentAt)}</span>
                                    <span className="message-sender" onClick={() => msg.senderSteamId && navigate(`/players/${msg.senderSteamId}`)}>
                                        {msg.senderLogin || msg.senderNickname || '?'}
                                    </span>
                                    {msg.receiverLogin && (
                                        <span className="message-receiver">
                                            → <span onClick={() => msg.receiverSteamId && navigate(`/players/${msg.receiverSteamId}`)}>{msg.receiverLogin}</span>
                                        </span>
                                    )}
                                    <span className="instance-badge" style={{ marginTop: 4, alignSelf: 'flex-start' }}>{msg.sourceInstance}</span>
                                </div>
                                <div className="message-body">
                                    <span className="message-text">{msg.message || <em style={{ color: 'var(--text-muted)' }}>[вложение]</em>}</span>
                                </div>
                            </div>
                        ))}
                    </div>
                    <div className="pagination">
                        <button disabled={page <= 1} onClick={() => loadConversation(s1, s2, page - 1)}>← Назад</button>
                        <span className="page-info">Стр. {page} / {Math.ceil(result.totalCount / 50)}</span>
                        <button disabled={!result.hasMore} onClick={() => loadConversation(s1, s2, page + 1)}>Далее →</button>
                    </div>
                </div>
            )}
        </div>
    );
}
