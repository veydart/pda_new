import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './services/auth';
import Layout from './components/Layout';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import LiveSpyPage from './pages/LiveSpyPage';
import PlayersPage from './pages/PlayersPage';
import PlayerProfilePage from './pages/PlayerProfilePage';
import MessagesPage from './pages/MessagesPage';
import SearchPage from './pages/SearchPage';
import GraphPage from './pages/GraphPage';
import UsersPage from './pages/UsersPage';
import ServerSettingsPage from './pages/ServerSettingsPage';
import IntegrationsPage from './pages/IntegrationsPage';
import type { ReactNode } from 'react';

function AdminGuard({ children }: { children: ReactNode }) {
    const { isSuperAdmin } = useAuth();
    if (!isSuperAdmin) return <Navigate to="/" replace />;
    return <>{children}</>;
}

function AppRoutes() {
    return (
        <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route element={<Layout />}>
                <Route index element={<DashboardPage />} />
                <Route path="live" element={<LiveSpyPage />} />
                <Route path="players" element={<PlayersPage />} />
                <Route path="players/:steamId" element={<PlayerProfilePage />} />
                <Route path="messages" element={<MessagesPage />} />
                <Route path="search" element={<SearchPage />} />
                <Route path="graph" element={<GraphPage />} />
                <Route path="admin/users" element={<AdminGuard><UsersPage /></AdminGuard>} />
                <Route path="admin/settings" element={<AdminGuard><ServerSettingsPage /></AdminGuard>} />
                <Route path="admin/integrations" element={<AdminGuard><IntegrationsPage /></AdminGuard>} />
            </Route>
        </Routes>
    );
}

export default function App() {
    return (
        <BrowserRouter>
            <AuthProvider>
                <AppRoutes />
            </AuthProvider>
        </BrowserRouter>
    );
}
