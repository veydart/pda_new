import { useEffect, useState } from 'react';
import { Settings, Database, CheckCircle, XCircle, Archive, Zap, Gamepad2 } from 'lucide-react';
import { settingsApi, type ConnectionInfo, type ServerSessionDto, type TestConnectionResult } from '../services/adminApi';

export default function ServerSettingsPage() {
    const [_connection, setConnection] = useState<ConnectionInfo | null>(null);
    const [sessions, setSessions] = useState<ServerSessionDto[]>([]);
    const [loading, setLoading] = useState(true);

    // Form state
    const [host, setHost] = useState('');
    const [user, setUser] = useState('');
    const [password, setPassword] = useState('');
    const [selectedDbs, setSelectedDbs] = useState<string[]>([]);
    const [sessionName, setSessionName] = useState('');
    const [description, setDescription] = useState('');

    // Test state
    const [testResult, setTestResult] = useState<TestConnectionResult | null>(null);
    const [testing, setTesting] = useState(false);

    // Save state
    const [saving, setSaving] = useState(false);
    const [saveMsg, setSaveMsg] = useState('');

    // Steam API Key state
    const [steamKeyConfigured, setSteamKeyConfigured] = useState(false);
    const [steamKeyMasked, setSteamKeyMasked] = useState('');
    const [steamKeyInput, setSteamKeyInput] = useState('');
    const [steamKeySaving, setSteamKeySaving] = useState(false);
    const [steamKeyMsg, setSteamKeyMsg] = useState('');

    const loadData = () => {
        setLoading(true);
        Promise.all([
            settingsApi.getConnection(),
            settingsApi.getSessions(),
        ]).then(([conn, sess]) => {
            setConnection(conn);
            setSessions(sess);
            setHost(conn.host);
            setUser(conn.user);
            setSelectedDbs(conn.instanceNames);
        }).finally(() => setLoading(false));
    };

    useEffect(() => {
        loadData();
        settingsApi.getSteamApiKey().then(r => {
            setSteamKeyConfigured(r.isConfigured);
            setSteamKeyMasked(r.maskedKey);
        });
    }, []);

    const handleSaveSteamKey = async () => {
        if (!steamKeyInput.trim()) return;
        setSteamKeySaving(true);
        setSteamKeyMsg('');
        try {
            await settingsApi.saveSteamApiKey(steamKeyInput.trim());
            setSteamKeyMsg('✅ Steam API Key сохранён');
            setSteamKeyInput('');
            const r = await settingsApi.getSteamApiKey();
            setSteamKeyConfigured(r.isConfigured);
            setSteamKeyMasked(r.maskedKey);
        } catch (err: any) {
            setSteamKeyMsg(`❌ ${err.response?.data?.message || 'Ошибка'}`);
        }
        setSteamKeySaving(false);
    };

    const handleTest = async () => {
        setTesting(true);
        setTestResult(null);
        try {
            const result = await settingsApi.testConnection(host, user, password);
            setTestResult(result);
        } catch {
            setTestResult({ success: false, message: 'Ошибка запроса', databases: [] });
        }
        setTesting(false);
    };

    const handleSave = async () => {
        if (!confirm('⚠️ ВНИМАНИЕ!\n\nЭто действие:\n1. Архивирует ВСЕ текущие данные\n2. Очистит рабочие таблицы\n3. Начнёт синхронизацию с нуля\n\nПродолжить?'))
            return;

        setSaving(true);
        setSaveMsg('');
        try {
            await settingsApi.saveConnection({
                host, user, password,
                instanceNames: selectedDbs,
                sessionName,
                description: description || undefined,
            });
            setSaveMsg('✅ Подключение сохранено! Данные архивированы. ETL начнёт заново.');
            setPassword('');
            loadData();
        } catch (err: any) {
            setSaveMsg(`❌ ${err.response?.data?.message || 'Ошибка'}`);
        }
        setSaving(false);
    };

    const toggleDb = (db: string) => {
        setSelectedDbs(prev => prev.includes(db) ? prev.filter(d => d !== db) : [...prev, db]);
    };

    if (loading) return <div className="loading-container"><div className="spinner" /></div>;

    const activeSession = sessions.find(s => s.isActive);
    const archivedSessions = sessions.filter(s => !s.isActive);

    return (
        <div className="fade-in">
            <div className="page-header">
                <h1 className="page-title"><Settings size={24} /> Настройки сервера</h1>
                <p className="page-subtitle">Управление подключением MariaDB и серверными сессиями</p>
            </div>

            {/* Current session info */}
            {activeSession && (
                <div className="card" style={{ marginBottom: 20, borderColor: 'rgba(52, 211, 153, 0.3)' }}>
                    <div className="card-header">
                        <h2 className="card-title"><Zap size={16} style={{ color: 'var(--accent-green)' }} /> Текущая сессия</h2>
                        <span className="live-indicator"><span className="live-dot" /> ACTIVE</span>
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
                        <div>
                            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginBottom: 2 }}>Имя</div>
                            <div style={{ fontWeight: 600 }}>{activeSession.name}</div>
                        </div>
                        <div>
                            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginBottom: 2 }}>Хост</div>
                            <div style={{ fontFamily: 'var(--font-mono)', color: 'var(--accent-cyan)' }}>{activeSession.mariaDbHost}</div>
                        </div>
                        <div>
                            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginBottom: 2 }}>Пользователь</div>
                            <div>{activeSession.mariaDbUser}</div>
                        </div>
                        <div>
                            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginBottom: 2 }}>Создана</div>
                            <div>{new Date(activeSession.createdAt).toLocaleString('ru-RU')}</div>
                        </div>
                    </div>
                </div>
            )}

            {/* Connection form */}
            <div className="card" style={{ marginBottom: 20 }}>
                <div className="card-header">
                    <h2 className="card-title"><Database size={16} /> Подключение MariaDB</h2>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16, marginBottom: 16 }}>
                    <div>
                        <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Хост (IP:Port)</label>
                        <input className="search-input" style={{ paddingLeft: 16 }} placeholder="212.22.93.25"
                            value={host} onChange={e => setHost(e.target.value)} />
                    </div>
                    <div>
                        <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Логин</label>
                        <input className="search-input" style={{ paddingLeft: 16 }} placeholder="nh-partner"
                            value={user} onChange={e => setUser(e.target.value)} />
                    </div>
                    <div>
                        <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Пароль</label>
                        <input className="search-input" style={{ paddingLeft: 16 }} type="password" placeholder="••••••"
                            value={password} onChange={e => setPassword(e.target.value)} />
                    </div>
                </div>

                <div style={{ display: 'flex', gap: 12, marginBottom: 16 }}>
                    <button className="btn btn-primary" onClick={handleTest} disabled={testing || !host || !user}>
                        {testing ? <div className="spinner" style={{ width: 16, height: 16, borderWidth: 2 }} /> : <Database size={16} />}
                        {testing ? 'Проверка...' : 'Тест подключения'}
                    </button>
                </div>

                {/* Test result */}
                {testResult && (
                    <div style={{
                        padding: '12px 16px', borderRadius: 'var(--radius-md)', marginBottom: 16,
                        background: testResult.success ? 'rgba(52, 211, 153, 0.08)' : 'rgba(248, 113, 113, 0.08)',
                        border: `1px solid ${testResult.success ? 'rgba(52, 211, 153, 0.3)' : 'rgba(248, 113, 113, 0.3)'}`
                    }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
                            {testResult.success ? <CheckCircle size={18} style={{ color: 'var(--accent-green)' }} /> : <XCircle size={18} style={{ color: 'var(--accent-red)' }} />}
                            <span style={{ fontWeight: 600, color: testResult.success ? 'var(--accent-green)' : 'var(--accent-red)' }}>
                                {testResult.message}
                            </span>
                        </div>
                        {testResult.databases.length > 0 && (
                            <div>
                                <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 8 }}>
                                    Выберите базы данных (инстансы):
                                </div>
                                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                                    {testResult.databases.map(db => (
                                        <button
                                            key={db}
                                            className={`btn ${selectedDbs.includes(db) ? 'btn-primary' : 'btn-ghost'}`}
                                            style={{ padding: '4px 12px', fontSize: '0.82rem' }}
                                            onClick={() => toggleDb(db)}
                                        >
                                            {selectedDbs.includes(db) ? '☑' : '☐'} {db}
                                        </button>
                                    ))}
                                </div>
                            </div>
                        )}
                    </div>
                )}

                {/* Session name for save */}
                {selectedDbs.length > 0 && (
                    <div style={{ borderTop: '1px solid var(--border-subtle)', paddingTop: 16 }}>
                        <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', marginBottom: 12 }}>
                            <strong>Выбрано инстансов:</strong> {selectedDbs.join(', ')}
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 12, marginBottom: 16 }}>
                            <div>
                                <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Название сессии *</label>
                                <input className="search-input" style={{ paddingLeft: 16 }} placeholder="Сезон 4"
                                    value={sessionName} onChange={e => setSessionName(e.target.value)} />
                            </div>
                            <div>
                                <label style={{ fontSize: '0.8rem', color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>Описание (опционально)</label>
                                <input className="search-input" style={{ paddingLeft: 16 }} placeholder="Новый сервер, свежие данные"
                                    value={description} onChange={e => setDescription(e.target.value)} />
                            </div>
                        </div>
                        <button
                            className="btn btn-primary"
                            style={{ background: 'rgba(248, 113, 113, 0.15)', color: 'var(--accent-red)', borderColor: 'rgba(248, 113, 113, 0.3)', fontWeight: 700 }}
                            onClick={handleSave}
                            disabled={saving || !sessionName || !password || selectedDbs.length === 0}
                        >
                            {saving ? <div className="spinner" style={{ width: 16, height: 16, borderWidth: 2 }} /> : <Archive size={16} />}
                            {saving ? 'Архивация и переподключение...' : '⚠️ Сохранить и переподключиться'}
                        </button>
                    </div>
                )}

                {saveMsg && (
                    <div style={{
                        marginTop: 12, padding: '10px 14px', borderRadius: 'var(--radius-md)',
                        fontSize: '0.88rem', fontWeight: 500,
                        background: saveMsg.startsWith('✅') ? 'rgba(52, 211, 153, 0.08)' : 'rgba(248, 113, 113, 0.08)',
                        color: saveMsg.startsWith('✅') ? 'var(--accent-green)' : 'var(--accent-red)'
                    }}>
                        {saveMsg}
                    </div>
                )}
            </div>

            {/* Steam API Key */}
            <div className="card" style={{ marginBottom: 20 }}>
                <div className="card-header">
                    <h2 className="card-title"><Gamepad2 size={16} style={{ color: 'var(--accent-cyan)' }} /> Steam Web API</h2>
                    {steamKeyConfigured && <span style={{ fontSize: '0.78rem', color: 'var(--accent-green)', fontWeight: 600 }}>✓ Настроен</span>}
                </div>
                <p style={{ fontSize: '0.82rem', color: 'var(--text-muted)', marginBottom: 12 }}>
                    Ключ Steam Web API используется для подгрузки аватарок и информации профилей игроков.
                    Получить: <a href="https://steamcommunity.com/dev/apikey" target="_blank" rel="noreferrer" style={{ color: 'var(--accent-cyan)' }}>steamcommunity.com/dev/apikey</a>
                </p>
                {steamKeyConfigured && (
                    <div style={{ marginBottom: 12, padding: '8px 12px', borderRadius: 'var(--radius-sm)', background: 'var(--bg-elevated)', fontFamily: 'var(--font-mono)', fontSize: '0.82rem', color: 'var(--text-muted)' }}>
                        Текущий ключ: {steamKeyMasked}
                    </div>
                )}
                <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                    <input
                        className="search-input"
                        style={{ paddingLeft: 16, maxWidth: 400 }}
                        type="password"
                        placeholder={steamKeyConfigured ? 'Новый ключ (заменит текущий)' : 'Вставьте Steam API Key'}
                        value={steamKeyInput}
                        onChange={e => setSteamKeyInput(e.target.value)}
                    />
                    <button
                        className="btn btn-primary"
                        onClick={handleSaveSteamKey}
                        disabled={steamKeySaving || !steamKeyInput.trim()}
                    >
                        {steamKeySaving ? 'Сохранение...' : 'Сохранить'}
                    </button>
                </div>
                {steamKeyMsg && (
                    <div style={{
                        marginTop: 8, fontSize: '0.85rem', fontWeight: 500,
                        color: steamKeyMsg.startsWith('✅') ? 'var(--accent-green)' : 'var(--accent-red)'
                    }}>
                        {steamKeyMsg}
                    </div>
                )}
            </div>

            {/* Session history */}
            {archivedSessions.length > 0 && (
                <div className="card">
                    <div className="card-header">
                        <h2 className="card-title"><Archive size={16} /> Архив сессий ({archivedSessions.length})</h2>
                    </div>
                    <table className="data-table">
                        <thead>
                            <tr>
                                <th>ID</th>
                                <th>Имя</th>
                                <th>Хост</th>
                                <th>Сообщений</th>
                                <th>Игроков</th>
                                <th>Создана</th>
                                <th>Архивирована</th>
                            </tr>
                        </thead>
                        <tbody>
                            {archivedSessions.map(s => (
                                <tr key={s.id}>
                                    <td style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-muted)' }}>{s.id}</td>
                                    <td style={{ fontWeight: 600 }}>{s.name}</td>
                                    <td style={{ fontFamily: 'var(--font-mono)', fontSize: '0.82rem' }}>{s.mariaDbHost}</td>
                                    <td style={{ fontFamily: 'var(--font-mono)', color: 'var(--accent-cyan)' }}>
                                        {s.archivedMessageCount?.toLocaleString() ?? '—'}
                                    </td>
                                    <td style={{ fontFamily: 'var(--font-mono)' }}>{s.archivedPlayerCount?.toLocaleString() ?? '—'}</td>
                                    <td style={{ fontSize: '0.82rem', color: 'var(--text-muted)' }}>
                                        {new Date(s.createdAt).toLocaleDateString('ru-RU')}
                                    </td>
                                    <td style={{ fontSize: '0.82rem', color: 'var(--text-muted)' }}>
                                        {s.archivedAt ? new Date(s.archivedAt).toLocaleDateString('ru-RU') : '—'}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </div>
    );
}
