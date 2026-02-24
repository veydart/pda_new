import { NavLink, Outlet, Navigate } from 'react-router-dom';
import { LayoutDashboard, Users, MessageSquare, Search, GitBranch, Radio, Shield, Settings, LogOut, Webhook } from 'lucide-react';
import { useAuth } from '../services/auth';

export default function Layout() {
    const { isAuthenticated, isSuperAdmin, user, logout, loading } = useAuth();

    if (loading) {
        return <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
            <div className="spinner" />
        </div>;
    }

    if (!isAuthenticated) return <Navigate to="/login" replace />;

    return (
        <div className="app-layout">
            <aside className="sidebar">
                <div className="sidebar-header">
                    <div className="sidebar-logo">
                        <div className="logo-icon">📡</div>
                        <div>
                            <div>PDA Analytics</div>
                            <div style={{ fontSize: '0.65rem', color: 'var(--text-muted)', fontWeight: 400, marginTop: 2 }}>
                                Intelligence Hub
                            </div>
                        </div>
                    </div>
                </div>

                <nav className="sidebar-nav">
                    <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)', padding: '8px 14px 4px', textTransform: 'uppercase', letterSpacing: '0.08em' }}>
                        Аналитика
                    </div>
                    <NavLink to="/" end className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                        <LayoutDashboard size={18} />
                        Dashboard
                    </NavLink>
                    <NavLink to="/live" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                        <Radio size={18} />
                        Live Spy
                    </NavLink>
                    <NavLink to="/players" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                        <Users size={18} />
                        Игроки
                    </NavLink>
                    <NavLink to="/messages" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                        <MessageSquare size={18} />
                        Сообщения
                    </NavLink>
                    <NavLink to="/search" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                        <Search size={18} />
                        Omni-Search
                    </NavLink>
                    <NavLink to="/graph" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                        <GitBranch size={18} />
                        Граф связей
                    </NavLink>

                    {isSuperAdmin && (
                        <>
                            <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)', padding: '16px 14px 4px', textTransform: 'uppercase', letterSpacing: '0.08em', borderTop: '1px solid var(--border-subtle)', marginTop: 8 }}>
                                Администрирование
                            </div>
                            <NavLink to="/admin/users" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                                <Shield size={18} />
                                Доступ
                            </NavLink>
                            <NavLink to="/admin/settings" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                                <Settings size={18} />
                                Настройки
                            </NavLink>
                            <NavLink to="/admin/integrations" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                                <Webhook size={18} />
                                Интеграции
                            </NavLink>
                        </>
                    )}
                </nav>

                <div className="sidebar-footer">
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <div style={{ fontWeight: 600, color: 'var(--text-secondary)', fontSize: '0.82rem' }}>
                                {user?.username}
                            </div>
                            <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>
                                {user?.role === 'SuperAdmin' ? '👑 SuperAdmin' : '📊 Operator'}
                            </div>
                        </div>
                        <button
                            onClick={logout}
                            className="btn btn-ghost"
                            style={{ padding: '4px 8px' }}
                            title="Выйти"
                        >
                            <LogOut size={16} />
                        </button>
                    </div>
                </div>
            </aside>

            <main className="main-content">
                <Outlet />
            </main>
        </div>
    );
}
