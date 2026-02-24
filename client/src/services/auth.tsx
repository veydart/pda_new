import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import api from './api';

// ── Types ──

interface User {
    id: number;
    username: string;
    role: string;
}

interface AuthContextType {
    user: User | null;
    token: string | null;
    isAuthenticated: boolean;
    isSuperAdmin: boolean;
    login: (username: string, password: string) => Promise<{ success: boolean; error?: string }>;
    logout: () => void;
    loading: boolean;
}

const AuthContext = createContext<AuthContextType | null>(null);

// ── Provider ──

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<User | null>(null);
    const [token, setToken] = useState<string | null>(() => localStorage.getItem('pda_token'));
    const [loading, setLoading] = useState(true);

    // Set axios default header when token changes
    useEffect(() => {
        if (token) {
            api.defaults.headers.common['Authorization'] = `Bearer ${token}`;
            localStorage.setItem('pda_token', token);
        } else {
            delete api.defaults.headers.common['Authorization'];
            localStorage.removeItem('pda_token');
        }
    }, [token]);

    // Verify token on mount
    useEffect(() => {
        if (!token) {
            setLoading(false);
            return;
        }

        api.get('/auth/me')
            .then(res => {
                setUser(res.data);
            })
            .catch(() => {
                setToken(null);
                setUser(null);
            })
            .finally(() => setLoading(false));
    }, []); // eslint-disable-line react-hooks/exhaustive-deps

    const login = useCallback(async (username: string, password: string) => {
        try {
            const res = await api.post('/auth/login', { username, password });
            setToken(res.data.token);
            setUser(res.data.user);
            return { success: true };
        } catch (err: any) {
            const msg = err.response?.data?.message || 'Ошибка авторизации';
            return { success: false, error: msg };
        }
    }, []);

    const logout = useCallback(() => {
        setToken(null);
        setUser(null);
    }, []);

    return (
        <AuthContext.Provider
            value={{
                user,
                token,
                isAuthenticated: !!user,
                isSuperAdmin: user?.role === 'SuperAdmin',
                login,
                logout,
                loading,
            }}
        >
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth() {
    const ctx = useContext(AuthContext);
    if (!ctx) throw new Error('useAuth must be used within AuthProvider');
    return ctx;
}

// ── Axios Interceptor: auto-logout on 401 ──

api.interceptors.response.use(
    res => res,
    err => {
        if (err.response?.status === 401 && localStorage.getItem('pda_token')) {
            localStorage.removeItem('pda_token');
            window.location.href = '/login';
        }
        return Promise.reject(err);
    }
);
