import { useEffect, useState } from 'react';
import { Shield, UserPlus, Trash2, Key } from 'lucide-react';
import { usersApi, type WebUser } from '../services/adminApi';

export default function UsersPage() {
    const [users, setUsers] = useState<WebUser[]>([]);
    const [loading, setLoading] = useState(true);
    const [showCreate, setShowCreate] = useState(false);
    const [newUsername, setNewUsername] = useState('');
    const [newPassword, setNewPassword] = useState('');
    const [createError, setCreateError] = useState('');

    // Change password modal state
    const [changePwUser, setChangePwUser] = useState<WebUser | null>(null);
    const [newPw, setNewPw] = useState('');
    const [pwMsg, setPwMsg] = useState('');

    const loadUsers = () => {
        setLoading(true);
        usersApi.getAll().then(setUsers).finally(() => setLoading(false));
    };

    useEffect(() => { loadUsers(); }, []);

    const handleCreate = async () => {
        setCreateError('');
        try {
            await usersApi.create(newUsername, newPassword);
            setShowCreate(false);
            setNewUsername('');
            setNewPassword('');
            loadUsers();
        } catch (err: any) {
            setCreateError(err.response?.data?.message || 'Ошибка');
        }
    };

    const handleDelete = async (user: WebUser) => {
        if (!confirm(`Удалить пользователя "${user.username}"?`)) return;
        try {
            await usersApi.delete(user.id);
            loadUsers();
        } catch (err: any) {
            alert(err.response?.data?.message || 'Ошибка удаления');
        }
    };

    const handleChangePassword = async () => {
        if (!changePwUser) return;
        setPwMsg('');
        try {
            await usersApi.changePassword(changePwUser.id, newPw);
            setPwMsg('✅ Пароль изменён');
            setNewPw('');
            setTimeout(() => { setChangePwUser(null); setPwMsg(''); }, 1500);
        } catch (err: any) {
            setPwMsg(err.response?.data?.message || 'Ошибка');
        }
    };

    if (loading) return <div className="loading-container"><div className="spinner" /></div>;

    return (
        <div className="fade-in">
            <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                    <h1 className="page-title"><Shield size={24} /> Управление доступом</h1>
                    <p className="page-subtitle">Создание и удаление операторов</p>
                </div>
                <button className="btn btn-primary" onClick={() => setShowCreate(!showCreate)}>
                    <UserPlus size={16} /> Новый оператор
                </button>
            </div>

            {/* Create form */}
            {showCreate && (
                <div className="card" style={{ marginBottom: 20 }}>
                    <h3 className="card-title" style={{ marginBottom: 12 }}>Создать оператора</h3>
                    <div style={{ display: 'flex', gap: 12, alignItems: 'end' }}>
                        <div style={{ flex: 1 }}>
                            <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Логин</label>
                            <input className="search-input" style={{ paddingLeft: 16 }} placeholder="operator1"
                                value={newUsername} onChange={e => setNewUsername(e.target.value)} />
                        </div>
                        <div style={{ flex: 1 }}>
                            <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Пароль</label>
                            <input className="search-input" style={{ paddingLeft: 16 }} type="password" placeholder="••••••"
                                value={newPassword} onChange={e => setNewPassword(e.target.value)} />
                        </div>
                        <button className="btn btn-primary" onClick={handleCreate} disabled={!newUsername || !newPassword}>
                            Создать
                        </button>
                        <button className="btn btn-ghost" onClick={() => setShowCreate(false)}>Отмена</button>
                    </div>
                    {createError && <div className="login-error" style={{ marginTop: 8 }}>{createError}</div>}
                </div>
            )}

            {/* Users table */}
            <div className="card" style={{ padding: 0 }}>
                <table className="data-table">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Логин</th>
                            <th>Роль</th>
                            <th>Создан</th>
                            <th>Статус</th>
                            <th style={{ textAlign: 'right' }}>Действия</th>
                        </tr>
                    </thead>
                    <tbody>
                        {users.map(u => (
                            <tr key={u.id}>
                                <td style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-muted)' }}>{u.id}</td>
                                <td style={{ fontWeight: 600 }}>{u.username}</td>
                                <td>
                                    <span className={`chat-type-badge ${u.role === 'SuperAdmin' ? 'private' : 'global'}`}>
                                        {u.role === 'SuperAdmin' ? '👑 SuperAdmin' : '📊 Operator'}
                                    </span>
                                </td>
                                <td style={{ fontSize: '0.82rem', color: 'var(--text-muted)' }}>
                                    {new Date(u.createdAt).toLocaleDateString('ru-RU')}
                                </td>
                                <td>
                                    <span style={{ color: u.isActive ? 'var(--accent-green)' : 'var(--accent-red)' }}>
                                        {u.isActive ? '● Active' : '○ Inactive'}
                                    </span>
                                </td>
                                <td style={{ textAlign: 'right' }}>
                                    <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
                                        <button className="btn btn-ghost" style={{ padding: '4px 8px' }}
                                            onClick={() => { setChangePwUser(u); setNewPw(''); setPwMsg(''); }}>
                                            <Key size={14} />
                                        </button>
                                        {u.role !== 'SuperAdmin' && (
                                            <button className="btn btn-ghost" style={{ padding: '4px 8px', color: 'var(--accent-red)' }}
                                                onClick={() => handleDelete(u)}>
                                                <Trash2 size={14} />
                                            </button>
                                        )}
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* Change password modal */}
            {changePwUser && (
                <div className="modal-overlay" onClick={() => setChangePwUser(null)}>
                    <div className="modal-card" onClick={e => e.stopPropagation()}>
                        <h3 className="card-title">🔑 Смена пароля: {changePwUser.username}</h3>
                        <div style={{ marginTop: 16 }}>
                            <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Новый пароль</label>
                            <input className="search-input" style={{ paddingLeft: 16 }} type="password" placeholder="••••••"
                                value={newPw} onChange={e => setNewPw(e.target.value)} autoFocus />
                        </div>
                        {pwMsg && <div style={{ marginTop: 8, fontSize: '0.85rem', color: pwMsg.startsWith('✅') ? 'var(--accent-green)' : 'var(--accent-red)' }}>{pwMsg}</div>}
                        <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
                            <button className="btn btn-primary" onClick={handleChangePassword} disabled={!newPw}>Сохранить</button>
                            <button className="btn btn-ghost" onClick={() => setChangePwUser(null)}>Отмена</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
