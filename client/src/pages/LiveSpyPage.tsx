import { useEffect, useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Radio, Pause, Play } from 'lucide-react';
import { messagesApi, type MessageDto } from '../services/api';
import { useSignalR } from '../services/signalr';

export default function LiveSpyPage() {
    const { isConnected, liveMessages } = useSignalR();
    const [historicalMessages, setHistoricalMessages] = useState<MessageDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [paused, setPaused] = useState(false);
    const [typeFilter, setTypeFilter] = useState<string | undefined>(undefined);
    const listRef = useRef<HTMLDivElement>(null);
    const navigate = useNavigate();

    // Load initial messages
    useEffect(() => {
        messagesApi.getFeed(1, 50, undefined, typeFilter)
            .then(r => setHistoricalMessages(r.items))
            .finally(() => setLoading(false));
    }, [typeFilter]);

    // Merge live + historical (dedup by id)
    const allMessages = (() => {
        if (paused) return historicalMessages;
        const seen = new Set<number>();
        const merged: MessageDto[] = [];
        for (const m of [...liveMessages, ...historicalMessages]) {
            if (!seen.has(m.id)) {
                seen.add(m.id);
                merged.push(m);
            }
        }
        return merged.sort((a, b) => new Date(b.sentAt).getTime() - new Date(a.sentAt).getTime()).slice(0, 100);
    })();

    const formatTime = (iso: string) => {
        const d = new Date(iso);
        return d.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' });
    };

    if (loading) return <div className="loading-container"><div className="spinner" /></div>;

    return (
        <div className="fade-in">
            <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                    <h1 className="page-title">
                        <Radio size={24} style={{ color: 'var(--accent-green)' }} />
                        Live Spy Mode
                    </h1>
                    <p className="page-subtitle">Перехват PDA-сообщений в реальном времени</p>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
                    <div className={`live-indicator ${isConnected ? '' : 'disconnected'}`}>
                        <span className="live-dot" />
                        {isConnected ? 'CONNECTED' : 'DISCONNECTED'}
                    </div>
                    <button className="btn btn-ghost" onClick={() => setPaused(!paused)}>
                        {paused ? <Play size={16} /> : <Pause size={16} />}
                        {paused ? 'Resume' : 'Pause'}
                    </button>
                </div>
            </div>

            <div className="tabs">
                <button className={`tab ${!typeFilter ? 'active' : ''}`} onClick={() => setTypeFilter(undefined)}>Все</button>
                <button className={`tab ${typeFilter === 'private' ? 'active' : ''}`} onClick={() => setTypeFilter('private')}>Приватные</button>
                <button className={`tab ${typeFilter === 'global' ? 'active' : ''}`} onClick={() => setTypeFilter('global')}>Глобальные</button>
            </div>

            <div className="card" style={{ padding: 0 }}>
                <div className="message-feed" ref={listRef}>
                    {allMessages.length === 0 ? (
                        <div className="empty-state">Нет сообщений</div>
                    ) : (
                        allMessages.map((msg) => (
                            <div
                                key={msg.id}
                                className={`message-item ${liveMessages.some(lm => lm.id === msg.id) ? 'new' : ''}`}
                            >
                                <div className="message-meta">
                                    <span className="message-time">{formatTime(msg.sentAt)}</span>
                                    <span
                                        className="message-sender"
                                        onClick={() => msg.senderSteamId && navigate(`/players/${msg.senderSteamId}`)}
                                        title={`SteamID: ${msg.senderSteamId}`}
                                    >
                                        {msg.senderLogin || msg.senderNickname || msg.senderSteamId || '?'}
                                    </span>
                                    {msg.chatType === 'Private' && msg.receiverLogin && (
                                        <span className="message-receiver">
                                            → <span
                                                onClick={() => msg.receiverSteamId && navigate(`/players/${msg.receiverSteamId}`)}
                                                title={`SteamID: ${msg.receiverSteamId}`}
                                            >
                                                {msg.receiverLogin || msg.receiverNickname || '?'}
                                            </span>
                                        </span>
                                    )}
                                    <div style={{ display: 'flex', gap: 6, marginTop: 4 }}>
                                        <span className={`chat-type-badge ${msg.chatType.toLowerCase()}`}>{msg.chatType}</span>
                                        <span className="instance-badge">{msg.sourceInstance}</span>
                                    </div>
                                </div>
                                <div className="message-body">
                                    <span className="message-text">
                                        {msg.message || <em style={{ color: 'var(--text-muted)' }}>[вложение]</em>}
                                    </span>
                                </div>
                            </div>
                        ))
                    )}
                </div>
            </div>
        </div>
    );
}
