import React from 'react';
import { Routes, Route, Navigate, NavLink, Link } from 'react-router-dom';
import { useAuth } from './context/AuthContext';
import AuthPage from './pages/AuthPage';
import HomePage from './pages/HomePage';
import RoomPage from './pages/RoomPage';
import LeaderboardPage from './pages/LeaderboardPage';
import ProfilePage from './pages/ProfilePage';
import AdminPage from './pages/AdminPage';

function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? children : <Navigate to="/auth" replace />;
}

function AdminRoute({ children }) {
  const { isAuthenticated, isAdmin } = useAuth();
  if (!isAuthenticated) return <Navigate to="/auth" replace />;
  return isAdmin ? children : <Navigate to="/home" replace />;
}

export default function App() {
  const { isAuthenticated, isAdmin, canInstall, promptInstall } = useAuth();

  return (
    <div className="app-shell">
      {/* Native-Style Top App Bar */}
      <header className="app-topbar">
        <Link to={isAuthenticated ? '/home' : '/auth'} className="app-brand">
          <img src="/icon-192.png" alt="Mafia Online" />
          <span className="app-brand-title">MAFIA ONLINE</span>
        </Link>

        <div className="d-flex align-items-center gap-2">
          {canInstall && (
            <button type="button" className="install-pill" onClick={promptInstall}>
              📲 Install
            </button>
          )}
        </div>
      </header>

      {/* Active Route Screen */}
      <main className={`app-content ${isAuthenticated ? '' : 'no-nav'}`}>
        <Routes>
          <Route
            path="/auth"
            element={isAuthenticated ? <Navigate to="/home" replace /> : <AuthPage />}
          />
          <Route
            path="/home"
            element={
              <ProtectedRoute>
                <HomePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/room/:code"
            element={
              <ProtectedRoute>
                <RoomPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/leaderboard"
            element={
              <ProtectedRoute>
                <LeaderboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/profile"
            element={
              <ProtectedRoute>
                <ProfilePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/admin"
            element={
              <AdminRoute>
                <AdminPage />
              </AdminRoute>
            }
          />
          <Route path="*" element={<Navigate to={isAuthenticated ? '/home' : '/auth'} replace />} />
        </Routes>
      </main>

      {/* Fixed Bottom Native Tab Navigation */}
      {isAuthenticated && (
        <nav className="app-bottom-nav" aria-label="Main App Navigation">
          <NavLink to="/home" className={({ isActive }) => `nav-tab ${isActive ? 'active' : ''}`}>
            <span className="tab-icon">🎮</span>
            <span>Play</span>
          </NavLink>
          <NavLink to="/leaderboard" className={({ isActive }) => `nav-tab ${isActive ? 'active' : ''}`}>
            <span className="tab-icon">🏆</span>
            <span>Ranks</span>
          </NavLink>
          {isAdmin && (
            <NavLink to="/admin" className={({ isActive }) => `nav-tab ${isActive ? 'active' : ''}`}>
              <span className="tab-icon">🛡️</span>
              <span>Admin</span>
            </NavLink>
          )}
          <NavLink to="/profile" className={({ isActive }) => `nav-tab ${isActive ? 'active' : ''}`}>
            <span className="tab-icon">👤</span>
            <span>Profile</span>
          </NavLink>
        </nav>
      )}
    </div>
  );
}
