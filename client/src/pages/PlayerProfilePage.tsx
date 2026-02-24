import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { User, Send, Inbox, GitBranch, Clock, ExternalLink } from 'lucide-react';
import { playersApi, type PlayerProfile } from '../services/api';

const STEAM_STATE_LABELS: Record<number, { label: string; color: string }> = {
    0: { label: 'Оффлайн', color: '#6b7280' },
    1: { label: 'Онлайн', color: '#34d399' },
    2: { label: 'Занят', color: '#f87171' },
    3: { label: 'Отошёл', color: '#fbbf24' },
    4: { label: 'Спит', color: '#818cf8' },
    5: { label: 'Готов к обмену', color: '#38bdf8' },
    6: { label: 'Хочет играть', color: '#a78bfa' },
};

export default function PlayerProfilePage() {
    const { steamId } = useParams<{ steamId: string }>();
    const [profile, setProfile] = useState<PlayerProfile | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const navigate = useNavigate();

    useEffect(() => {
        if (!steamId) return;
        setLoading(true);
        setError(null);
        playersApi.getProfile(steamId)
            .then(setProfile)
            .catch(err => setError(err.response?.data?.message || 'Игрок не найден'))
            .finally(() => setLoading(false));
    }, [steamId]);

    if (loading) return <div className="loading-container"><div className="spinner" /><span style={{ color: 'var(--text-muted)' }}>Загрузка профиля...</span></div>;
    if (error) return <div className="empty-state"><p>{error}</p></div>;
    if (!profile) return null;

    const initial = (profile.nickname || profile.steamId)[0].toUpperCase();
    const steamState = profile.steamPersonaState != null ? STEAM_STATE_LABELS[profile.steamPersonaState] : null;

    return (
        <div className="fade-in">
            {/* ── Profile Header ── */}
            <div className="profile-header">
                {/* Avatar: Steam or fallback letter */}
                {profile.steamAvatarUrl ? (
                    <img
                        src={profile.steamAvatarUrl}
                        alt="Steam Avatar"
                        style={{
                            width: 72, height: 72, borderRadius: '50%',
                            border: '3px solid var(--border-subtle)',
                            flexShrink: 0, objectFit: 'cover'
                        }}
                    />
                ) : (
                    <div className="profile-avatar">{initial}</div>
                )}
                <div className="profile-info">
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <h1 style={{ margin: 0 }}>{profile.nickname || 'Без имени'}</h1>
                        {/* Steam persona state badge */}
                        {steamState && (
                            <span style={{
                                display: 'inline-flex', alignItems: 'center', gap: 4,
                                fontSize: '0.72rem', fontWeight: 600,
                                padding: '2px 8px', borderRadius: 12,
                                background: `${steamState.color}20`,
                                color: steamState.color,
                                border: `1px solid ${steamState.color}40`,
                            }}>
                                <span style={{
                                    width: 6, height: 6, borderRadius: '50%',
                                    background: steamState.color,
                                    display: 'inline-block'
                                }} />
                                {steamState.label}
                            </span>
                        )}
                    </div>

                    {/* Steam persona name & real name */}
                    {profile.steamPersonaName && profile.steamPersonaName !== profile.nickname && (
                        <div style={{ fontSize: '0.82rem', color: 'var(--text-muted)', marginTop: 2 }}>
                            Steam: <strong style={{ color: 'var(--text-secondary)' }}>{profile.steamPersonaName}</strong>
                            {profile.steamRealName && (
                                <span> • {profile.steamRealName}</span>
                            )}
                            {profile.steamCountryCode && (
                                <span> • 🌍 {profile.steamCountryCode}</span>
                            )}
                        </div>
                    )}

                    <div className="profile-steam-id">
                        {profile.steamId}
                        {profile.steamProfileUrl && (
                            <a
                                href={profile.steamProfileUrl}
                                target="_blank"
                                rel="noreferrer"
                                style={{
                                    marginLeft: 8,
                                    display: 'inline-flex', alignItems: 'center', gap: 4,
                                    color: 'var(--accent-cyan)',
                                    fontSize: '0.78rem',
                                    textDecoration: 'none'
                                }}
                            >
                                <ExternalLink size={12} /> Steam Profile
                            </a>
                        )}
                    </div>

                    <div className="profile-stats">
                        <span className="profile-stat"><Send size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} /><strong>{profile.totalMessagesSent}</strong> отправлено</span>
                        <span className="profile-stat"><Inbox size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} /><strong>{profile.totalMessagesReceived}</strong> получено</span>
                        <span className="profile-stat"><Clock size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} />Онлайн: {new Date(profile.lastLogonDate).toLocaleDateString('ru-RU')}</span>
                        {profile.factions.length > 0 && profile.factions.map((f, i) => (
                            <span key={i} className="profile-stat" style={{
                                display: 'inline-flex', alignItems: 'center', gap: 5,
                                padding: '2px 10px', borderRadius: 12,
                                background: `hsl(${(f.factionColor % 360 + 360) % 360}, 60%, 50%, 0.15)`,
                                border: `1px solid hsl(${(f.factionColor % 360 + 360) % 360}, 60%, 50%, 0.3)`,
                                color: `hsl(${(f.factionColor % 360 + 360) % 360}, 60%, 65%)`
                            }}>
                                <span style={{ width: 8, height: 8, borderRadius: 2, background: `hsl(${(f.factionColor % 360 + 360) % 360}, 60%, 50%)`, display: 'inline-block' }} />
                                🛡️ {f.factionName} <span style={{ opacity: 0.7, fontSize: '0.75rem' }}>Rank #{f.rankId}</span>
                            </span>
                        ))}
                    </div>
                </div>
                <div style={{ marginLeft: 'auto' }}>
                    <button className="btn btn-primary" onClick={() => navigate(`/graph?steamId=${profile.steamId}`)}>
                        <GitBranch size={16} /> Граф связей
                    </button>
                </div>
            </div>

            {/* ── PDA Accounts (мультиаккаунты) ── */}
            <div className="card" style={{ marginTop: 20 }}>
                <div className="card-header">
                    <h2 className="card-title">🔑 PDA Аккаунты ({profile.pdaAccounts.length})</h2>
                </div>
                {profile.pdaAccounts.length === 0 ? (
                    <div className="empty-state" style={{ padding: '20px 0' }}>Нет PDA-аккаунтов</div>
                ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                        {profile.pdaAccounts.map(acc => (
                            <div className="pda-account-item" key={`${acc.sourceId}-${acc.sourceInstance}`}>
                                <User size={18} style={{ color: 'var(--accent-purple)', flexShrink: 0 }} />
                                <div>
                                    <div className="pda-login">{acc.login}</div>
                                    <div className="pda-instance">
                                        {acc.sourceInstance} • Активность: {new Date(acc.lastActivity).toLocaleDateString('ru-RU')}
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {/* ── Контакты (собеседники) ── */}
            <div className="card" style={{ marginTop: 20 }}>
                <div className="card-header">
                    <h2 className="card-title">💬 Собеседники ({profile.contacts.length})</h2>
                </div>
                {profile.contacts.length === 0 ? (
                    <div className="empty-state" style={{ padding: '20px 0' }}>Нет переписок</div>
                ) : (
                    <div className="contact-list">
                        {profile.contacts.map(c => (
                            <div
                                className="contact-item"
                                key={c.steamId}
                                onClick={() => navigate(`/players/${c.steamId}`)}
                            >
                                <div>
                                    <span className="contact-name">{c.nickname || c.steamId}</span>
                                    <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginLeft: 8 }}>{c.steamId}</span>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
                                    <span className="contact-count">{c.messageCount} msg</span>
                                    <button
                                        className="btn btn-primary"
                                        style={{ padding: '4px 12px', fontSize: '0.78rem' }}
                                        onClick={(e) => {
                                            e.stopPropagation();
                                            navigate(`/messages?steamId1=${profile.steamId}&steamId2=${c.steamId}`);
                                        }}
                                    >
                                        Переписка
                                    </button>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
