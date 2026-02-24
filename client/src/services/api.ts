import axios from 'axios';

const API_BASE = import.meta.env.VITE_API_URL || '/api';

const api = axios.create({
    baseURL: API_BASE,
    timeout: 15000,
});

// ── Types ──

export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    page: number;
    pageSize: number;
    hasMore: boolean;
}

export interface DashboardStats {
    totalPlayers: number;
    totalPdaAccounts: number;
    totalMessages: number;
    totalPrivateMessages: number;
    totalGlobalMessages: number;
    totalFactions: number;
    totalInstances: number;
    lastMessageAt: string | null;
}

export interface TopChatter {
    steamId: string;
    nickname: string | null;
    login: string | null;
    messageCount: number;
    lastMessageAt: string;
}

export interface PlayerSearchHit {
    steamId: string;
    nickname: string | null;
    sourceInstance: string;
}

export interface PdaAccountSearchHit {
    sourceId: number;
    login: string;
    steamId: string;
    sourceInstance: string;
}

export interface MessageSearchHit {
    id: number;
    senderLogin: string | null;
    senderSteamId: string | null;
    receiverLogin: string | null;
    receiverSteamId: string | null;
    message: string;
    sentAt: string;
    sourceInstance: string;
}

export interface SearchResult {
    players: PlayerSearchHit[];
    pdaAccounts: PdaAccountSearchHit[];
    messages: MessageSearchHit[];
}

export interface PdaAccountDto {
    sourceId: number;
    login: string;
    lastActivity: string;
    sourceInstance: string;
}

export interface FactionMembershipDto {
    factionName: string;
    factionColor: number;
    factionIcon: string | null;
    rankId: number;
    sourceInstance: string;
}

export interface ContactDto {
    steamId: string;
    nickname: string | null;
    messageCount: number;
    lastMessageAt: string;
}

export interface PlayerProfile {
    steamId: string;
    nickname: string | null;
    registrationDate: string;
    lastLogonDate: string;
    sourceInstance: string;
    // Steam
    steamAvatarUrl: string | null;
    steamProfileUrl: string | null;
    steamPersonaName: string | null;
    steamRealName: string | null;
    steamCountryCode: string | null;
    steamPersonaState: number | null;
    // Data
    pdaAccounts: PdaAccountDto[];
    factions: FactionMembershipDto[];
    totalMessagesSent: number;
    totalMessagesReceived: number;
    contacts: ContactDto[];
}

export interface MessageDto {
    id: number;
    sourceMessageId: number;
    chatType: string;
    chatName: string | null;
    senderLogin: string | null;
    senderSteamId: string | null;
    senderNickname: string | null;
    receiverLogin: string | null;
    receiverSteamId: string | null;
    receiverNickname: string | null;
    message: string;
    attachments: string | null;
    sentAt: string;
    sourceInstance: string;
}

export interface GraphNode {
    steamId: string;
    nickname: string | null;
    isCenter: boolean;
}

export interface GraphEdge {
    id: string;
    source: string;
    target: string;
    weight: number;
    lastMessageAt: string;
}

export interface GraphData {
    nodes: GraphNode[];
    edges: GraphEdge[];
}

// ── API functions ──

export const dashboardApi = {
    getStats: () => api.get<DashboardStats>('/dashboard/stats').then(r => r.data),
    getTopChatters: (limit = 10) => api.get<TopChatter[]>(`/dashboard/top-chatters?limit=${limit}`).then(r => r.data),
};

export const playersApi = {
    search: (search: string, page = 1, pageSize = 20) =>
        api.get<PagedResult<PlayerSearchHit>>('/players', { params: { search, page, pageSize } }).then(r => r.data),
    getProfile: (steamId: string) =>
        api.get<PlayerProfile>(`/players/${steamId}`).then(r => r.data),
};

export const messagesApi = {
    getFeed: (page = 1, pageSize = 50, instance?: string, type?: string) =>
        api.get<PagedResult<MessageDto>>('/messages/feed', { params: { page, pageSize, instance, type } }).then(r => r.data),
    getBetween: (steamId1: string, steamId2: string, page = 1, pageSize = 50) =>
        api.get<PagedResult<MessageDto>>('/messages/between', { params: { steamId1, steamId2, page, pageSize } }).then(r => r.data),
    getByPlayer: (steamId: string, page = 1, pageSize = 50, direction?: string) =>
        api.get<PagedResult<MessageDto>>(`/messages/by-player/${steamId}`, { params: { page, pageSize, direction } }).then(r => r.data),
    getGraph: (steamId: string, depth = 1, maxNodes = 50) =>
        api.get<GraphData>(`/messages/graph/${steamId}`, { params: { depth, maxNodes } }).then(r => r.data),
};

export const searchApi = {
    omniSearch: (q: string, limit = 15) =>
        api.get<SearchResult>('/search', { params: { q, limit } }).then(r => r.data),
};

export default api;
