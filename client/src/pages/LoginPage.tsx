import { useState, type FormEvent } from 'react';
import { Navigate } from 'react-router-dom';
import { Lock, Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../services/auth';

export default function LoginPage() {
    const { login, isAuthenticated, loading } = useAuth();
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [showPassword, setShowPassword] = useState(false);

    if (loading) return <div className="login-page"><div className="spinner" /></div>;
    if (isAuthenticated) return <Navigate to="/" replace />;

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        setError('');
        setSubmitting(true);

        const result = await login(username, password);
        if (!result.success) {
            setError(result.error || 'Ошибка');
        }
        setSubmitting(false);
    };

    return (
        <div className="login-page">
            <div className="login-card">
                <div className="login-header">
                    <div className="login-logo">
                        <div className="logo-icon" style={{ width: 48, height: 48, fontSize: '1.5rem' }}>📡</div>
                    </div>
                    <h1>PDA Analytics</h1>
                    <p>Intelligence Hub — Авторизация</p>
                </div>

                <form onSubmit={handleSubmit}>
                    <div className="login-field">
                        <label>Имя пользователя</label>
                        <input
                            className="search-input"
                            style={{ paddingLeft: 16 }}
                            placeholder="admin"
                            value={username}
                            onChange={e => setUsername(e.target.value)}
                            autoFocus
                            autoComplete="username"
                        />
                    </div>

                    <div className="login-field">
                        <label>Пароль</label>
                        <div style={{ position: 'relative' }}>
                            <input
                                className="search-input"
                                style={{ paddingLeft: 16, paddingRight: 44 }}
                                type={showPassword ? 'text' : 'password'}
                                placeholder="••••••"
                                value={password}
                                onChange={e => setPassword(e.target.value)}
                                autoComplete="current-password"
                            />
                            <button
                                type="button"
                                onClick={() => setShowPassword(!showPassword)}
                                style={{
                                    position: 'absolute', right: 12, top: '50%', transform: 'translateY(-50%)',
                                    background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', padding: 0
                                }}
                            >
                                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                            </button>
                        </div>
                    </div>

                    {error && (
                        <div className="login-error">
                            <Lock size={14} /> {error}
                        </div>
                    )}

                    <button
                        type="submit"
                        className="btn btn-primary login-btn"
                        disabled={submitting || !username || !password}
                    >
                        {submitting ? <div className="spinner" style={{ width: 18, height: 18, borderWidth: 2 }} /> : <Lock size={16} />}
                        {submitting ? 'Вход...' : 'Войти'}
                    </button>
                </form>

                <div className="login-footer">
                    v1.0 • Read-Only Analytics Mode
                </div>
            </div>
        </div>
    );
}
