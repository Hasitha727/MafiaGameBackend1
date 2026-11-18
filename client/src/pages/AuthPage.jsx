import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function AuthPage() {
  const [mode, setMode] = useState('login'); // 'login' | 'register'
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const { login, register, canInstall, promptInstall } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      if (mode === 'login') {
        await login(username.trim(), password);
      } else {
        await register(username.trim(), email.trim(), password);
      }
      navigate('/home', { replace: true });
    } catch (err) {
      setError(err.response?.data?.error || 'Authentication failed. Please check your details.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="d-flex flex-column gap-3 py-2">
      {/* App Hero Splash */}
      <div className="text-center py-3">
        <img
          src="/icon-192.png"
          alt="Mafia Online"
          style={{
            width: 84,
            height: 84,
            borderRadius: 22,
            border: '2px solid rgba(0, 240, 255, 0.45)',
            boxShadow: '0 0 28px rgba(0, 240, 255, 0.25)'
          }}
        />
        <h1 className="font-display fw-bold mt-3 mb-1" style={{ fontSize: '1.85rem', letterSpacing: '1px' }}>
          MAFIA ONLINE
        </h1>
        <p className="text-secondary small mb-0">
          Deceit, Strategy & Real-Time Multiplayer Survival
        </p>
        {canInstall && (
          <button
            type="button"
            className="install-pill mt-3"
            onClick={promptInstall}
          >
            📲 Install App on Device
          </button>
        )}
      </div>

      {/* Auth Form Card */}
      <div className="game-card">
        {/* Segmented Control */}
        <div
          className="d-flex p-1 mb-4 rounded-3"
          style={{ background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.08)' }}
        >
          <button
            type="button"
            className={`btn flex-fill rounded-3 fw-semibold py-2 ${
              mode === 'login' ? 'btn-game-primary' : 'text-secondary border-0'
            }`}
            onClick={() => { setMode('login'); setError(''); }}
          >
            Login
          </button>
          <button
            type="button"
            className={`btn flex-fill rounded-3 fw-semibold py-2 ${
              mode === 'register' ? 'btn-game-primary' : 'text-secondary border-0'
            }`}
            onClick={() => { setMode('register'); setError(''); }}
          >
            Register
          </button>
        </div>

        {error && (
          <div className="alert alert-danger py-2 px-3 small rounded-3 mb-3">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="d-flex flex-column gap-3">
          <div>
            <label className="form-label small text-secondary mb-1">
              {mode === 'login' ? 'Username or Email' : 'Player Username'}
            </label>
            <input
              type="text"
              className="form-control game-input"
              placeholder={mode === 'login' ? 'Enter username or email' : 'e.g. Detective07'}
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
            />
          </div>

          {mode === 'register' && (
            <div>
              <label className="form-label small text-secondary mb-1">Email Address</label>
              <input
                type="email"
                className="form-control game-input"
                placeholder="you@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
              />
            </div>
          )}

          <div>
            <label className="form-label small text-secondary mb-1">Password</label>
            <input
              type="password"
              className="form-control game-input"
              placeholder="Enter password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className={`btn w-100 mt-1 ${mode === 'login' ? 'btn-game-green' : 'btn-game-primary'}`}
          >
            {loading
              ? 'Please wait...'
              : mode === 'login'
              ? 'LOGIN & PLAY'
              : 'CREATE PLAYER ACCOUNT'}
          </button>
        </form>
      </div>
    </div>
  );
}
