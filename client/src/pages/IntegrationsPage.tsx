import { useEffect, useState } from 'react';
import { Webhook, Plus, Trash2, ToggleLeft, ToggleRight, Hash, AtSign, X, Check, AlertTriangle, Pencil } from 'lucide-react';
import {
    integrationsApi,
    type ChatWebhookDto,
    type FactionMentionDto,
    type ChatRefDto,
    type FactionRefDto,
} from '../services/adminApi';

type Tab = 'chats' | 'mentions';

// ═══════════════════════════════════════════════════════════════
//  Toast Notification
// ═══════════════════════════════════════════════════════════════

interface Toast {
    id: number;
    message: string;
    type: 'success' | 'error';
}

let toastId = 0;

function ToastContainer({ toasts, onDismiss }: { toasts: Toast[]; onDismiss: (id: number) => void }) {
    return (
        <div style={{
            position: 'fixed', bottom: 24, right: 24, zIndex: 9999,
            display: 'flex', flexDirection: 'column', gap: 8,
        }}>
            {toasts.map(t => (
                <div key={t.id} className="toast-notification" data-type={t.type}
                    onClick={() => onDismiss(t.id)}>
                    {t.type === 'success' ? <Check size={16} /> : <AlertTriangle size={16} />}
                    {t.message}
                </div>
            ))}
        </div>
    );
}

// ═══════════════════════════════════════════════════════════════
//  Main Page
// ═══════════════════════════════════════════════════════════════

export default function IntegrationsPage() {
    const [tab, setTab] = useState<Tab>('chats');

    // Data
    const [chatHooks, setChatHooks] = useState<ChatWebhookDto[]>([]);
    const [mentionRules, setMentionRules] = useState<FactionMentionDto[]>([]);
    const [chatsRef, setChatsRef] = useState<ChatRefDto[]>([]);
    const [factionsRef, setFactionsRef] = useState<FactionRefDto[]>([]);
    const [loading, setLoading] = useState(true);

    // Modals
    const [showChatModal, setShowChatModal] = useState(false);
    const [showMentionModal, setShowMentionModal] = useState(false);

    // Editing
    const [editingHook, setEditingHook] = useState<ChatWebhookDto | null>(null);
    const [editingMention, setEditingMention] = useState<FactionMentionDto | null>(null);

    // Toasts
    const [toasts, setToasts] = useState<Toast[]>([]);

    const toast = (message: string, type: 'success' | 'error' = 'success') => {
        const id = ++toastId;
        setToasts(prev => [...prev, { id, message, type }]);
        setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 3500);
    };
    const dismissToast = (id: number) => setToasts(prev => prev.filter(t => t.id !== id));

    // ── Load data ──

    const loadAll = async () => {
        setLoading(true);
        try {
            const [hooks, mentions, chats, factions] = await Promise.all([
                integrationsApi.getChatWebhooks(),
                integrationsApi.getFactionMentions(),
                integrationsApi.getChatsRef(),
                integrationsApi.getFactionsRef(),
            ]);
            setChatHooks(hooks);
            setMentionRules(mentions);
            setChatsRef(chats);
            setFactionsRef(factions);
        } catch (err: any) {
            toast(err.response?.data?.message || 'Ошибка загрузки', 'error');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { loadAll(); }, []);

    // ── Actions ──

    const toggleChatHook = async (hook: ChatWebhookDto) => {
        try {
            await integrationsApi.updateChatWebhook(hook.id, { isEnabled: !hook.isEnabled });
            toast(hook.isEnabled ? 'Трансляция отключена' : 'Трансляция включена');
            loadAll();
        } catch { toast('Ошибка обновления', 'error'); }
    };

    const deleteChatHook = async (hook: ChatWebhookDto) => {
        if (!confirm(`Удалить трансляцию для «${hook.chatName || '#' + hook.chatSourceId}»?`)) return;
        try {
            await integrationsApi.deleteChatWebhook(hook.id);
            toast('Трансляция удалена');
            loadAll();
        } catch { toast('Ошибка удаления', 'error'); }
    };

    const toggleMention = async (rule: FactionMentionDto) => {
        try {
            await integrationsApi.updateFactionMention(rule.id, { isEnabled: !rule.isEnabled });
            toast(rule.isEnabled ? 'Правило отключено' : 'Правило включено');
            loadAll();
        } catch { toast('Ошибка обновления', 'error'); }
    };

    const deleteMention = async (rule: FactionMentionDto) => {
        if (!confirm(`Удалить правило «${rule.displayName}»?`)) return;
        try {
            await integrationsApi.deleteFactionMention(rule.id);
            toast('Правило удалено');
            loadAll();
        } catch { toast('Ошибка удаления', 'error'); }
    };

    // ── Render ──

    if (loading) return <div className="loading-container"><div className="spinner" /></div>;

    return (
        <div className="fade-in">
            <div className="page-header">
                <div>
                    <h1 className="page-title"><Webhook size={24} /> Интеграции Discord</h1>
                    <p className="page-subtitle">Маршрутизация сообщений PDA → Discord Webhooks</p>
                </div>
            </div>

            {/* Tabs */}
            <div className="integrations-tabs">
                <button
                    className={`integrations-tab ${tab === 'chats' ? 'active' : ''}`}
                    onClick={() => setTab('chats')}
                >
                    <Hash size={16} />
                    Трансляции чатов
                    <span className="tab-badge">{chatHooks.length}</span>
                </button>
                <button
                    className={`integrations-tab ${tab === 'mentions' ? 'active' : ''}`}
                    onClick={() => setTab('mentions')}
                >
                    <AtSign size={16} />
                    Упоминания фракций
                    <span className="tab-badge">{mentionRules.length}</span>
                </button>
            </div>

            {/* Tab content */}
            {tab === 'chats' && (
                <ChatWebhooksTab
                    hooks={chatHooks}
                    onToggle={toggleChatHook}
                    onDelete={deleteChatHook}
                    onAdd={() => { setEditingHook(null); setShowChatModal(true); }}
                    onEdit={(h) => { setEditingHook(h); setShowChatModal(true); }}
                />
            )}

            {tab === 'mentions' && (
                <FactionMentionsTab
                    rules={mentionRules}
                    onToggle={toggleMention}
                    onDelete={deleteMention}
                    onAdd={() => { setEditingMention(null); setShowMentionModal(true); }}
                    onEdit={(r) => { setEditingMention(r); setShowMentionModal(true); }}
                />
            )}

            {/* Modals */}
            {showChatModal && (
                <ChatWebhookModal
                    editing={editingHook}
                    chatsRef={chatsRef}
                    existingChatIds={chatHooks.map(h => `${h.chatSourceId}|${h.sourceInstance}`)}
                    onClose={() => { setShowChatModal(false); setEditingHook(null); }}
                    onSave={async (data) => {
                        if (editingHook) {
                            await integrationsApi.updateChatWebhook(editingHook.id, { webhookUrl: data.webhookUrl });
                            toast('Трансляция обновлена');
                        } else {
                            await integrationsApi.createChatWebhook(data);
                            toast('Трансляция добавлена');
                        }
                        setShowChatModal(false);
                        setEditingHook(null);
                        loadAll();
                    }}
                    onError={(msg) => toast(msg, 'error')}
                />
            )}

            {showMentionModal && (
                <MentionModal
                    editing={editingMention}
                    factionsRef={factionsRef}
                    onClose={() => { setShowMentionModal(false); setEditingMention(null); }}
                    onSave={async (data) => {
                        if (editingMention) {
                            await integrationsApi.updateFactionMention(editingMention.id, data);
                            toast('Правило обновлено');
                        } else {
                            await integrationsApi.createFactionMention(data as any);
                            toast('Правило упоминания добавлено');
                        }
                        setShowMentionModal(false);
                        setEditingMention(null);
                        loadAll();
                    }}
                    onError={(msg) => toast(msg, 'error')}
                />
            )}

            <ToastContainer toasts={toasts} onDismiss={dismissToast} />
        </div>
    );
}

// ═══════════════════════════════════════════════════════════════
//  Chat Webhooks Tab
// ═══════════════════════════════════════════════════════════════

function ChatWebhooksTab({ hooks, onToggle, onDelete, onAdd, onEdit }: {
    hooks: ChatWebhookDto[];
    onToggle: (h: ChatWebhookDto) => void;
    onDelete: (h: ChatWebhookDto) => void;
    onAdd: () => void;
    onEdit: (h: ChatWebhookDto) => void;
}) {
    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem', margin: 0 }}>
                    Все сообщения из глобального чата транслируются в Discord Webhook
                </p>
                <button className="btn btn-primary" onClick={onAdd}>
                    <Plus size={16} /> Добавить
                </button>
            </div>

            {hooks.length === 0 ? (
                <div className="card" style={{ textAlign: 'center', padding: 48 }}>
                    <Hash size={48} style={{ color: 'var(--text-muted)', marginBottom: 12 }} />
                    <p style={{ color: 'var(--text-muted)' }}>Нет настроенных трансляций</p>
                    <button className="btn btn-primary" onClick={onAdd} style={{ marginTop: 8 }}>
                        <Plus size={16} /> Создать первую
                    </button>
                </div>
            ) : (
                <div className="integrations-grid">
                    {hooks.map(h => (
                        <div key={h.id} className={`integration-card ${!h.isEnabled ? 'disabled' : ''}`}>
                            <div className="integration-card-header">
                                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                                    <Hash size={18} style={{ color: 'var(--accent-cyan)' }} />
                                    <span className="integration-name">{h.chatName || `Chat #${h.chatSourceId}`}</span>
                                </div>
                                <div style={{ display: 'flex', gap: 4 }}>
                                    <button className="btn btn-ghost btn-icon" onClick={() => onEdit(h)}
                                        title="Редактировать">
                                        <Pencil size={16} style={{ color: 'var(--accent-cyan)' }} />
                                    </button>
                                    <button className="btn btn-ghost btn-icon" onClick={() => onToggle(h)}
                                        title={h.isEnabled ? 'Отключить' : 'Включить'}>
                                        {h.isEnabled
                                            ? <ToggleRight size={20} style={{ color: 'var(--accent-green)' }} />
                                            : <ToggleLeft size={20} style={{ color: 'var(--text-muted)' }} />}
                                    </button>
                                    <button className="btn btn-ghost btn-icon" onClick={() => onDelete(h)}
                                        style={{ color: 'var(--accent-red)' }}>
                                        <Trash2 size={16} />
                                    </button>
                                </div>
                            </div>
                            <div className="integration-card-body">
                                <div className="integration-meta">
                                    <span className="meta-label">Instance</span>
                                    <span className="meta-value">{h.sourceInstance}</span>
                                </div>
                                <div className="integration-meta">
                                    <span className="meta-label">Webhook</span>
                                    <span className="meta-value webhook-url">{maskWebhookUrl(h.webhookUrl)}</span>
                                </div>
                                <div className="integration-meta">
                                    <span className="meta-label">Статус</span>
                                    <span style={{ color: h.isEnabled ? 'var(--accent-green)' : 'var(--text-muted)' }}>
                                        {h.isEnabled ? '● Активна' : '○ Отключена'}
                                    </span>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

// ═══════════════════════════════════════════════════════════════
//  Faction Mentions Tab
// ═══════════════════════════════════════════════════════════════

function FactionMentionsTab({ rules, onToggle, onDelete, onAdd, onEdit }: {
    rules: FactionMentionDto[];
    onToggle: (r: FactionMentionDto) => void;
    onDelete: (r: FactionMentionDto) => void;
    onAdd: () => void;
    onEdit: (r: FactionMentionDto) => void;
}) {
    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem', margin: 0 }}>
                    Отслеживание упоминаний фракций в тексте сообщений (@alias, слово-триггер)
                </p>
                <button className="btn btn-primary" onClick={onAdd}>
                    <Plus size={16} /> Добавить
                </button>
            </div>

            {rules.length === 0 ? (
                <div className="card" style={{ textAlign: 'center', padding: 48 }}>
                    <AtSign size={48} style={{ color: 'var(--text-muted)', marginBottom: 12 }} />
                    <p style={{ color: 'var(--text-muted)' }}>Нет правил упоминаний</p>
                    <button className="btn btn-primary" onClick={onAdd} style={{ marginTop: 8 }}>
                        <Plus size={16} /> Создать первое
                    </button>
                </div>
            ) : (
                <div className="integrations-grid">
                    {rules.map(r => (
                        <div key={r.id} className={`integration-card ${!r.isEnabled ? 'disabled' : ''}`}>
                            <div className="integration-card-header">
                                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                                    <AtSign size={18} style={{ color: 'var(--accent-pink)' }} />
                                    <span className="integration-name">{r.displayName}</span>
                                </div>
                                <div style={{ display: 'flex', gap: 4 }}>
                                    <button className="btn btn-ghost btn-icon" onClick={() => onEdit(r)}
                                        title="Редактировать">
                                        <Pencil size={16} style={{ color: 'var(--accent-cyan)' }} />
                                    </button>
                                    <button className="btn btn-ghost btn-icon" onClick={() => onToggle(r)}
                                        title={r.isEnabled ? 'Отключить' : 'Включить'}>
                                        {r.isEnabled
                                            ? <ToggleRight size={20} style={{ color: 'var(--accent-green)' }} />
                                            : <ToggleLeft size={20} style={{ color: 'var(--text-muted)' }} />}
                                    </button>
                                    <button className="btn btn-ghost btn-icon" onClick={() => onDelete(r)}
                                        style={{ color: 'var(--accent-red)' }}>
                                        <Trash2 size={16} />
                                    </button>
                                </div>
                            </div>
                            <div className="integration-card-body">
                                <div className="integration-meta">
                                    <span className="meta-label">Алиасы</span>
                                    <div className="aliases-list">
                                        {r.aliases.map((a, i) => (
                                            <span key={i} className="alias-tag">{a}</span>
                                        ))}
                                    </div>
                                </div>
                                <div className="integration-meta">
                                    <span className="meta-label">Webhook</span>
                                    <span className="meta-value webhook-url">{maskWebhookUrl(r.webhookUrl)}</span>
                                </div>
                                {r.discordRoleId && (
                                    <div className="integration-meta">
                                        <span className="meta-label">Role Ping</span>
                                        <span className="role-id-badge">@&{r.discordRoleId}</span>
                                    </div>
                                )}
                                <div className="integration-meta">
                                    <span className="meta-label">Instance</span>
                                    <span className="meta-value">{r.sourceInstance || 'Все'}</span>
                                </div>
                                <div className="integration-meta">
                                    <span className="meta-label">Статус</span>
                                    <span style={{ color: r.isEnabled ? 'var(--accent-green)' : 'var(--text-muted)' }}>
                                        {r.isEnabled ? '● Активно' : '○ Отключено'}
                                    </span>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

// ═══════════════════════════════════════════════════════════════
//  Chat Webhook Modal (Create / Edit)
// ═══════════════════════════════════════════════════════════════

function ChatWebhookModal({ editing, chatsRef, existingChatIds, onClose, onSave, onError }: {
    editing: ChatWebhookDto | null;
    chatsRef: ChatRefDto[];
    existingChatIds: string[];
    onClose: () => void;
    onSave: (data: { chatSourceId: number; sourceInstance: string; chatName?: string; webhookUrl: string }) => Promise<void>;
    onError: (msg: string) => void;
}) {
    const [selectedChat, setSelectedChat] = useState(
        editing ? `${editing.chatSourceId}|${editing.sourceInstance}` : ''
    );
    const [webhookUrl, setWebhookUrl] = useState(editing?.webhookUrl || '');
    const [saving, setSaving] = useState(false);

    const availableChats = editing
        ? chatsRef // В режиме edit показываем все чаты (текущий уже выбран)
        : chatsRef.filter(c => !existingChatIds.includes(`${c.sourceId}|${c.sourceInstance}`));

    const handleSave = async () => {
        if (!selectedChat || !webhookUrl) return;
        const [idStr, instance] = selectedChat.split('|');
        const chat = chatsRef.find(c => c.sourceId === +idStr && c.sourceInstance === instance);
        setSaving(true);
        try {
            await onSave({
                chatSourceId: +idStr,
                sourceInstance: instance,
                chatName: chat?.name || undefined,
                webhookUrl,
            });
        } catch (err: any) {
            onError(err.response?.data?.message || 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    const isEdit = !!editing;

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal-card modal-lg" onClick={e => e.stopPropagation()}>
                <div className="modal-header">
                    <h3 className="card-title">
                        <Hash size={20} /> {isEdit ? 'Редактирование трансляции' : 'Новая трансляция чата'}
                    </h3>
                    <button className="btn btn-ghost btn-icon" onClick={onClose}><X size={18} /></button>
                </div>
                <div style={{ marginTop: 20 }}>
                    <label className="field-label">Глобальный чат</label>
                    <select
                        className="search-input"
                        value={selectedChat}
                        onChange={e => setSelectedChat(e.target.value)}
                        style={{ paddingLeft: 16 }}
                        disabled={isEdit}
                    >
                        <option value="">— Выберите чат —</option>
                        {availableChats.map(c => (
                            <option key={`${c.sourceId}|${c.sourceInstance}`} value={`${c.sourceId}|${c.sourceInstance}`}>
                                {c.name || `Chat #${c.sourceId}`} [{c.sourceInstance}]
                            </option>
                        ))}
                    </select>
                </div>
                <div style={{ marginTop: 16 }}>
                    <label className="field-label">Discord Webhook URL</label>
                    <input
                        className="search-input"
                        style={{ paddingLeft: 16 }}
                        placeholder="https://discord.com/api/webhooks/..."
                        value={webhookUrl}
                        onChange={e => setWebhookUrl(e.target.value)}
                    />
                </div>
                <div className="modal-footer">
                    <button className="btn btn-ghost" onClick={onClose}>Отмена</button>
                    <button className="btn btn-primary"
                        onClick={handleSave}
                        disabled={!selectedChat || !webhookUrl || saving}>
                        {saving ? 'Сохранение…' : isEdit ? 'Сохранить' : 'Создать трансляцию'}
                    </button>
                </div>
            </div>
        </div>
    );
}

// ═══════════════════════════════════════════════════════════════
//  Faction Mention Modal (Create / Edit)
// ═══════════════════════════════════════════════════════════════

function MentionModal({ editing, factionsRef, onClose, onSave, onError }: {
    editing: FactionMentionDto | null;
    factionsRef: FactionRefDto[];
    onClose: () => void;
    onSave: (data: { factionSourceId?: number; sourceInstance?: string; displayName: string; aliases: string[]; discordRoleId?: string; webhookUrl: string }) => Promise<void>;
    onError: (msg: string) => void;
}) {
    const [selectedFaction, setSelectedFaction] = useState(
        editing?.factionSourceId ? `${editing.factionSourceId}|${editing.sourceInstance}` : ''
    );
    const [displayName, setDisplayName] = useState(editing?.displayName || '');
    const [aliasInput, setAliasInput] = useState('');
    const [aliases, setAliases] = useState<string[]>(editing?.aliases || []);
    const [discordRoleId, setDiscordRoleId] = useState(editing?.discordRoleId || '');
    const [webhookUrl, setWebhookUrl] = useState(editing?.webhookUrl || '');
    const [saving, setSaving] = useState(false);

    const isEdit = !!editing;

    const handleFactionChange = (val: string) => {
        setSelectedFaction(val);
        if (val) {
            const [idStr, inst] = val.split('|');
            const fac = factionsRef.find(f => f.sourceId === +idStr && f.sourceInstance === inst);
            if (fac && !displayName) {
                setDisplayName(fac.name);
                if (aliases.length === 0) {
                    setAliases([fac.name]);
                }
            }
        }
    };

    const addAlias = () => {
        const trimmed = aliasInput.trim();
        if (trimmed && !aliases.includes(trimmed)) {
            setAliases([...aliases, trimmed]);
            setAliasInput('');
        }
    };

    const removeAlias = (index: number) => {
        setAliases(aliases.filter((_, i) => i !== index));
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter' || e.key === ',') {
            e.preventDefault();
            addAlias();
        }
    };

    const handleSave = async () => {
        if (!displayName || aliases.length === 0 || !webhookUrl) return;
        setSaving(true);
        try {
            const data: any = { displayName, aliases, webhookUrl };
            if (discordRoleId.trim()) data.discordRoleId = discordRoleId.trim();
            else data.discordRoleId = '';
            if (selectedFaction) {
                const [idStr, inst] = selectedFaction.split('|');
                data.factionSourceId = +idStr;
                data.sourceInstance = inst;
            }
            await onSave(data);
        } catch (err: any) {
            onError(err.response?.data?.message || 'Ошибка сохранения');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal-card modal-lg" onClick={e => e.stopPropagation()}>
                <div className="modal-header">
                    <h3 className="card-title">
                        <AtSign size={20} /> {isEdit ? 'Редактирование правила' : 'Новое правило упоминания'}
                    </h3>
                    <button className="btn btn-ghost btn-icon" onClick={onClose}><X size={18} /></button>
                </div>

                <div style={{ marginTop: 20 }}>
                    <label className="field-label">Фракция (опционально)</label>
                    <select
                        className="search-input"
                        value={selectedFaction}
                        onChange={e => handleFactionChange(e.target.value)}
                        style={{ paddingLeft: 16 }}
                        disabled={isEdit}
                    >
                        <option value="">— Без привязки к фракции —</option>
                        {factionsRef.map(f => (
                            <option key={`${f.sourceId}|${f.sourceInstance}`} value={`${f.sourceId}|${f.sourceInstance}`}>
                                {f.name} [{f.sourceInstance}]
                            </option>
                        ))}
                    </select>
                </div>

                <div style={{ marginTop: 16 }}>
                    <label className="field-label">Название правила</label>
                    <input
                        className="search-input"
                        style={{ paddingLeft: 16 }}
                        placeholder="Например: Долг, Монолит..."
                        value={displayName}
                        onChange={e => setDisplayName(e.target.value)}
                    />
                </div>

                <div style={{ marginTop: 16 }}>
                    <label className="field-label">Триггер-слова / Алиасы</label>
                    <div className="aliases-input-container">
                        {aliases.map((a, i) => (
                            <span key={i} className="alias-tag editable">
                                {a}
                                <button onClick={() => removeAlias(i)} className="alias-remove"><X size={12} /></button>
                            </span>
                        ))}
                        <input
                            className="alias-input"
                            placeholder="Введите алиас и нажмите Enter"
                            value={aliasInput}
                            onChange={e => setAliasInput(e.target.value)}
                            onKeyDown={handleKeyDown}
                            onBlur={addAlias}
                        />
                    </div>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: 4 }}>
                        Нажмите Enter или запятую для добавления. Регистронезависимое сравнение.
                    </div>
                </div>

                <div style={{ marginTop: 16 }}>
                    <label className="field-label">Discord Role ID для пинга (опционально)</label>
                    <input
                        className="search-input"
                        style={{ paddingLeft: 16 }}
                        placeholder="Например: 1234567890123456789"
                        value={discordRoleId}
                        onChange={e => setDiscordRoleId(e.target.value)}
                    />
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: 4 }}>
                        Если указан — при срабатывании в сообщение добавится пинг роли {'<@&ID>'}.
                        Найти ID роли: Настройки сервера → Роли → ПКМ → Копировать ID.
                    </div>
                </div>

                <div style={{ marginTop: 16 }}>
                    <label className="field-label">Discord Webhook URL</label>
                    <input
                        className="search-input"
                        style={{ paddingLeft: 16 }}
                        placeholder="https://discord.com/api/webhooks/..."
                        value={webhookUrl}
                        onChange={e => setWebhookUrl(e.target.value)}
                    />
                </div>

                <div className="modal-footer">
                    <button className="btn btn-ghost" onClick={onClose}>Отмена</button>
                    <button className="btn btn-primary"
                        onClick={handleSave}
                        disabled={!displayName || aliases.length === 0 || !webhookUrl || saving}>
                        {saving ? 'Сохранение…' : isEdit ? 'Сохранить' : 'Создать правило'}
                    </button>
                </div>
            </div>
        </div>
    );
}

// ═══════════════════════════════════════════════════════════════
//  Helpers
// ═══════════════════════════════════════════════════════════════

function maskWebhookUrl(url: string): string {
    try {
        const parts = url.split('/');
        if (parts.length >= 2) {
            const token = parts[parts.length - 1];
            return url.replace(token, token.substring(0, 6) + '••••••');
        }
    } catch { /* ignore */ }
    return url.substring(0, 40) + '…';
}
