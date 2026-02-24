import api from './api';

// ── Types ──

export interface WebUser {
    id: number;
    username: string;
    role: string;
    createdAt: string;
    isActive: boolean;
}

export interface ConnectionInfo {
    host: string;
    user: string;
    password: string;
    instanceNames: string[];
    isConfigured: boolean;
}

export interface ServerSessionDto {
    id: number;
    name: string;
    description: string | null;
    mariaDbHost: string;
    mariaDbUser: string;
    instanceNames: string;
    isActive: boolean;
    createdAt: string;
    archivedAt: string | null;
    archivedMessageCount: number | null;
    archivedPlayerCount: number | null;
}

export interface TestConnectionResult {
    success: boolean;
    message: string;
    databases: string[];
}

// ── User management ──

export const usersApi = {
    getAll: () => api.get<WebUser[]>('/auth/users').then(r => r.data),

    create: (username: string, password: string) =>
        api.post('/auth/users', { username, password }).then(r => r.data),

    delete: (id: number) =>
        api.delete(`/auth/users/${id}`).then(r => r.data),

    changePassword: (id: number, newPassword: string) =>
        api.put(`/auth/users/${id}/password`, { newPassword }).then(r => r.data),
};

// ── Server settings ──

export const settingsApi = {
    getConnection: () =>
        api.get<ConnectionInfo>('/settings/connection').then(r => r.data),

    saveConnection: (data: {
        host: string;
        user: string;
        password: string;
        instanceNames: string[];
        sessionName: string;
        description?: string;
    }) => api.post('/settings/connection', data).then(r => r.data),

    testConnection: (host: string, user: string, password: string) =>
        api.post<TestConnectionResult>('/settings/test-connection', { host, user, password }).then(r => r.data),

    getSessions: () =>
        api.get<ServerSessionDto[]>('/settings/sessions').then(r => r.data),

    getSyncSettings: () =>
        api.get<{ intervalSeconds: number; messageBatchSize: number }>('/settings/sync').then(r => r.data),

    getSteamApiKey: () =>
        api.get<{ isConfigured: boolean; maskedKey: string }>('/settings/steam-api-key').then(r => r.data),

    saveSteamApiKey: (apiKey: string) =>
        api.put('/settings/steam-api-key', { apiKey }).then(r => r.data),
};

// ── Integrations (Discord Webhooks) ──

export interface ChatWebhookDto {
    id: number;
    chatSourceId: number;
    sourceInstance: string;
    chatName: string | null;
    webhookUrl: string;
    isEnabled: boolean;
    createdAt: string;
    updatedAt: string;
}

export interface FactionMentionDto {
    id: number;
    factionSourceId: number | null;
    sourceInstance: string | null;
    displayName: string;
    aliases: string[];
    discordRoleId: string | null;
    webhookUrl: string;
    isEnabled: boolean;
    createdAt: string;
    updatedAt: string;
}

export interface ChatRefDto {
    sourceId: number;
    name: string | null;
    type: string;
    sourceInstance: string;
}

export interface FactionRefDto {
    sourceId: number;
    name: string;
    icon: string | null;
    sourceInstance: string;
}

export const integrationsApi = {
    // Chat webhooks
    getChatWebhooks: () =>
        api.get<ChatWebhookDto[]>('/integrations/chat-webhooks').then(r => r.data),

    createChatWebhook: (data: { chatSourceId: number; sourceInstance: string; chatName?: string; webhookUrl: string }) =>
        api.post<ChatWebhookDto>('/integrations/chat-webhooks', data).then(r => r.data),

    updateChatWebhook: (id: number, data: { chatName?: string; webhookUrl?: string; isEnabled?: boolean }) =>
        api.put<ChatWebhookDto>(`/integrations/chat-webhooks/${id}`, data).then(r => r.data),

    deleteChatWebhook: (id: number) =>
        api.delete(`/integrations/chat-webhooks/${id}`).then(r => r.data),

    // Faction mentions
    getFactionMentions: () =>
        api.get<FactionMentionDto[]>('/integrations/faction-mentions').then(r => r.data),

    createFactionMention: (data: { factionSourceId?: number; sourceInstance?: string; displayName: string; aliases: string[]; discordRoleId?: string; webhookUrl: string }) =>
        api.post<FactionMentionDto>('/integrations/faction-mentions', data).then(r => r.data),

    updateFactionMention: (id: number, data: { displayName?: string; aliases?: string[]; discordRoleId?: string; webhookUrl?: string; isEnabled?: boolean }) =>
        api.put<FactionMentionDto>(`/integrations/faction-mentions/${id}`, data).then(r => r.data),

    deleteFactionMention: (id: number) =>
        api.delete(`/integrations/faction-mentions/${id}`).then(r => r.data),

    // Reference data
    getChatsRef: () =>
        api.get<ChatRefDto[]>('/integrations/ref/chats').then(r => r.data),

    getFactionsRef: () =>
        api.get<FactionRefDto[]>('/integrations/ref/factions').then(r => r.data),
};
