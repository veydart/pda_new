import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Users, MessageSquare, Shield, Server, Lock, Globe, Trophy } from 'lucide-react';
import { dashboardApi, type DashboardStats, type TopChatter } from '../services/api';

export default function DashboardPage() {
    const [stats, setStats] = useState<DashboardStats | null>(null);
    const [topChatters, setTopChatters] = useState<TopChatter[]>([]);
    const [loading, setLoading] = useState(true);
    const navigate = useNavigate();

    useEffect(() => {
        Promise.all([
            dashboardApi.getStats(),
            dashboardApi.getTopChatters(10),
        ]).then(([s, tc]) => {
            setStats(s);
            setTopChatters(tc);
        }).finally(() => setLoading(false));
    }, []);

    if (loading) return <div className="loading-container"><div className="spinner" /><span style={{ color: 'var(--text-muted)' }}>Загрузка аналитики...</span></div>;
    if (!stats) return null;

    const statCards = [
        { icon: <Users size={20} />, color: 'cyan', value: stats.totalPlayers.toLocaleString(), label: 'Игроков' },
        { icon: <Shield size={20} />, color: 'purple', value: stats.totalPdaAccounts.toLocaleString(), label: 'PDA Аккаунтов' },
        { icon: <MessageSquare size={20} />, color: 'green', value: stats.totalMessages.toLocaleString(), label: 'Всего сообщений' },
        { icon: <Lock size={20} />, color: 'orange', value: stats.totalPrivateMessages.toLocaleString(), label: 'Приватных' },
        { icon: <Globe size={20} />, color: 'pink', value: stats.totalGlobalMessages.toLocaleString(), label: 'Глобальных' },
        { icon: <Shield size={20} />, color: 'purple', value: stats.totalFactions.toLocaleString(), label: 'Фракций' },
        { icon: <Server size={20} />, color: 'cyan', value: stats.totalInstances.toLocaleString(), label: 'Инстансов' },
    ];

    return (
        <div className="fade-in">
            <div className="page-header">
                <h1 className="page-title">📊 Dashboard</h1>
                <p className="page-subtitle">
                    Обзор данных PDA • Последнее сообщение: {stats.lastMessageAt ? new Date(stats.lastMessageAt).toLocaleString('ru-RU') : '—'}
                </p>
            </div>

            <div className="stats-grid">
                {statCards.map((s, i) => (
                    <div className="stat-card" key={i}>
                        <div className={`stat-icon ${s.color}`}>{s.icon}</div>
                        <div className="stat-value">{s.value}</div>
                        <div className="stat-label">{s.label}</div>
                    </div>
                ))}
            </div>

            <div className="card">
                <div className="card-header">
                    <h2 className="card-title"><Trophy size={18} style={{ color: 'var(--accent-orange)' }} /> Топ активных игроков</h2>
                </div>
                <table className="data-table">
                    <thead>
                        <tr>
                            <th>#</th>
                            <th>Никнейм</th>
                            <th>Логин PDA</th>
                            <th>SteamID</th>
                            <th>Сообщений</th>
                            <th>Последнее</th>
                        </tr>
                    </thead>
                    <tbody>
                        {topChatters.map((c, i) => (
                            <tr key={c.steamId} style={{ cursor: 'pointer' }} onClick={() => navigate(`/players/${c.steamId}`)}>
                                <td style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-muted)' }}>{i + 1}</td>
                                <td style={{ fontWeight: 600 }}>{c.nickname || '—'}</td>
                                <td style={{ color: 'var(--accent-purple)' }}>{c.login || '—'}</td>
                                <td className="steam-id">{c.steamId}</td>
                                <td style={{ fontFamily: 'var(--font-mono)', color: 'var(--accent-cyan)' }}>{c.messageCount.toLocaleString()}</td>
                                <td style={{ fontSize: '0.82rem', color: 'var(--text-muted)' }}>{new Date(c.lastMessageAt).toLocaleDateString('ru-RU')}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
