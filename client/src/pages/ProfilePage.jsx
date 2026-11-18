import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function ProfilePage() {
  const { user, logout, canInstall, isInstalled, promptInstall } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/auth', { replace: true });
  };

  return (
    <div className="d-flex flex-column gap-3">
      {/* Player Profile Card */}
      <div className="game-card text-center py-4">
        <div
          className="mx-auto d-flex align-items-center justify-content-center mb-3"
          style={{
            width: 72,
            height: 72,
            borderRadius: '50%',
            background: 'linear-gradient(135deg, #2563EB, #7C3AED)',
            fontSize: '2rem',
            fontWeight: 800
          }}
        >
          {user?.username?.charAt(0)?.toUpperCase() || 'P'}
        </div>
        <h4 className="font-display fw-bold mb-1">{user?.username}</h4>
        <div className="text-secondary small mb-2">{user?.email}</div>
        <span className="badge rounded-pill bg-info bg-opacity-10 text-info border border-info px-3 py-1">
          Role: {user?.role}
        </span>
      </div>

      {/* Install on Mobile Phone Card */}
      <div className="game-card">
        <h6 className="font-display fw-bold text-info mb-2">📱 Install as Mobile App</h6>
        {isInstalled ? (
          <div className="alert alert-success py-2 px-3 small mb-0 rounded-3">
            ✅ Mafia Online is installed and running in Full-Screen App Mode!
          </div>
        ) : (
          <>
            {canInstall ? (
              <button type="button" className="btn btn-game-primary w-100 mb-2" onClick={promptInstall}>
                📲 Install Mafia Online Now
              </button>
            ) : (
              <div className="small text-secondary" style={{ lineHeight: 1.6 }}>
                <strong>Android (Chrome):</strong> Tap <code>⋮ Menu</code> → <strong>Install app / Add to Home screen</strong>.<br />
                <strong>iPhone (Safari):</strong> Tap <code>Share ⬆️</code> → <strong>Add to Home Screen</strong>.
              </div>
            )}
          </>
        )}
      </div>

      {/* Actions */}
      <div className="game-card d-flex flex-column gap-2">
        <a
          href="/swagger"
          target="_blank"
          rel="noreferrer"
          className="btn btn-outline-info w-100 rounded-3 py-2 fw-semibold"
        >
          📑 Open Swagger API Docs
        </a>
        <button
          type="button"
          className="btn btn-outline-danger w-100 rounded-3 py-2 fw-semibold"
          onClick={handleLogout}
        >
          🚪 Logout
        </button>
      </div>
    </div>
  );
}
