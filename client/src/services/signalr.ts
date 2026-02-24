import { useEffect, useRef, useState, useCallback } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel, HubConnectionState } from '@microsoft/signalr';
import type { MessageDto } from './api';

const HUB_URL = import.meta.env.VITE_HUB_URL || '/hubs/pda';

export function useSignalR() {
    const connectionRef = useRef<HubConnection | null>(null);
    const [isConnected, setIsConnected] = useState(false);
    const [liveMessages, setLiveMessages] = useState<MessageDto[]>([]);

    useEffect(() => {
        const connection = new HubConnectionBuilder()
            .withUrl(HUB_URL)
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(LogLevel.Warning)
            .build();

        connectionRef.current = connection;

        connection.on('NewMessage', (message: MessageDto) => {
            setLiveMessages(prev => [message, ...prev].slice(0, 200));
        });

        connection.onreconnecting(() => setIsConnected(false));
        connection.onreconnected(() => setIsConnected(true));
        connection.onclose(() => setIsConnected(false));

        connection.start()
            .then(() => setIsConnected(true))
            .catch(err => console.error('SignalR connection error:', err));

        return () => {
            connection.stop();
        };
    }, []);

    const subscribeToInstance = useCallback(async (instanceName: string) => {
        const conn = connectionRef.current;
        if (conn?.state === HubConnectionState.Connected) {
            await conn.invoke('SubscribeToInstance', instanceName);
        }
    }, []);

    const subscribeToPlayer = useCallback(async (steamId: string) => {
        const conn = connectionRef.current;
        if (conn?.state === HubConnectionState.Connected) {
            await conn.invoke('SubscribeToPlayer', steamId);
        }
    }, []);

    const clearLiveMessages = useCallback(() => {
        setLiveMessages([]);
    }, []);

    return {
        isConnected,
        liveMessages,
        subscribeToInstance,
        subscribeToPlayer,
        clearLiveMessages,
    };
}
