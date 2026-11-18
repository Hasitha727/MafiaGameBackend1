import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';

export default function HomePage() {
  const [roomCode, setRoomCode] = useState('');
  const [chatLanguage, setChatLanguage] = useState('Singlish');
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState('');
  const { user, canInstall, promptInstall } = useAuth();
  const navigate = useNavigate();

  const handleCreateRoom = async () => {
    setError('');
    setCreating(true);
    try {
      const { data } = await api.post('/room/create', { modifiers: 0, chatLanguage });
      if (data.room_code) {
        navigate(`/room/${data.room_code}`);
      }
    } catch (err) {
      setError(err.response?.data?.error || 'Could not create room. Please try again.');
    } finally {
      setCreating(false);
    }
  };

  const handleJoinRoom = (e) => {
    e.preventDefault();
    const code = roomCode.trim().toUpperCase();
    if (!code || code.length < 4) {
      setError('Please enter a valid 6-digit Room Code.');
      return;
    }
    navigate(`/room/${code}`);
  };

  return (
    <div className="d-flex flex-column gap-3">
      {/* Greeting Banner */}
      <div className="game-card d-flex justify-content-between align-items-center">
        <div>
          <div className="small text-secondary">Welcome back,</div>
          <h4 className="font-display fw-bold mb-0 text-info">{user?.username}</h4>
        </div>
        <span className="badge rounded-pill text-bg-dark border border-info text-info px-3 py-2">
          {user?.role === 'Admin' ? '🛡️ Admin' : '🎮 Player'}
        </span>
      </div>

      {/* Mobile App Install Prompt Banner */}
      {canInstall && (
        <div
          className="game-card d-flex justify-content-between align-items-center gap-2"
          style={{
            background: 'linear-gradient(135deg, rgba(0, 240, 255, 0.14), rgba(124, 58, 237, 0.18))',
            borderColor: 'rgba(0, 240, 255, 0.4)'
          }}
        >
          <div>
            <div className="fw-bold small">📲 Install Mafia Online App</div>
            <div className="text-secondary" style={{ fontSize: '0.78rem' }}>
              Play full-screen directly from your home screen!
            </div>
          </div>
          <button type="button" className="btn btn-sm btn-game-primary px-3" onClick={promptInstall}>
            Install
          </button>
        </div>
      )}

      {error && <div className="alert alert-danger py-2 px-3 small rounded-3 mb-0">{error}</div>}

      {/* Create Game Room Card */}
      <div className="game-card">
        <div className="d-flex align-items-center gap-2 mb-2">
          <span style={{ fontSize: '1.35rem' }}>🎮</span>
          <h5 className="font-display fw-bold mb-0">Host a Match (Friends or AI Bots)</h5>
        </div>
        <p className="text-secondary small mb-3">
          Play with friends or tap <strong>+ Add AI Bot</strong> inside the lobby to play solo with smart Singlish/English AI bots!
        </p>

        {/* Bot & Announcement Language Toggle */}
        <div className="mb-3">
          <div className="small text-secondary mb-1">Game & AI Bot Chat Language</div>
          <div
            className="d-flex p-1 rounded-3"
            style={{ background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.08)' }}
          >
            <button
              type="button"
              className={`btn btn-sm flex-fill rounded-3 fw-semibold py-2 ${
                chatLanguage === 'Singlish' ? 'btn-game-primary' : 'text-secondary border-0'
              }`}
              onClick={() => setChatLanguage('Singlish')}
            >
              🇱🇰 Singlish
            </button>
            <button
              type="button"
              className={`btn btn-sm flex-fill rounded-3 fw-semibold py-2 ${
                chatLanguage === 'English' ? 'btn-game-primary' : 'text-secondary border-0'
              }`}
              onClick={() => setChatLanguage('English')}
            >
              🇬🇧 English
            </button>
          </div>
        </div>

        <button
          type="button"
          className="btn btn-game-primary w-100"
          disabled={creating}
          onClick={handleCreateRoom}
        >
          {creating ? 'Creating Room...' : '➕ CREATE NEW ROOM'}
        </button>
      </div>

      {/* Join Existing Room Card */}
      <div className="game-card">
        <div className="d-flex align-items-center gap-2 mb-2">
          <span style={{ fontSize: '1.35rem' }}>🔑</span>
          <h5 className="font-display fw-bold mb-0">Join Friend&apos;s Room</h5>
        </div>
        <form onSubmit={handleJoinRoom} className="d-flex flex-column gap-2 mt-2">
          <input
            type="text"
            className="form-control game-input text-center fw-bold"
            style={{ letterSpacing: '4px', fontSize: '1.1rem', textTransform: 'uppercase' }}
            placeholder="ENTER CODE (E.G. X8K2P9)"
            maxLength={6}
            value={roomCode}
            onChange={(e) => setRoomCode(e.target.value.toUpperCase())}
          />
          <button type="submit" className="btn btn-game-amber w-100">
            🚀 JOIN ROOM
          </button>
        </form>
      </div>

      {/* Quick Roles Cheat Sheet */}
      <div className="game-card">
        <h6 className="font-display fw-bold text-info mb-3">🕵️ Secret Roles Overview</h6>
        <div className="row g-2 small">
          <div className="col-6">
            <div className="p-2 rounded-3" style={{ background: 'rgba(244,63,94,0.1)', border: '1px solid rgba(244,63,94,0.25)' }}>
              <div className="fw-bold text-danger">🔫 Mafia</div>
              <div className="text-secondary" style={{ fontSize: '0.75rem' }}>Eliminate townsfolk at night without getting caught.</div>
            </div>
          </div>
          <div className="col-6">
            <div className="p-2 rounded-3" style={{ background: 'rgba(16,185,129,0.1)', border: '1px solid rgba(16,185,129,0.25)' }}>
              <div className="fw-bold text-success">🩺 Doctor</div>
              <div className="text-secondary" style={{ fontSize: '0.75rem' }}>Protect one player from Mafia attacks each night.</div>
            </div>
          </div>
          <div className="col-6">
            <div className="p-2 rounded-3" style={{ background: 'rgba(0,240,255,0.1)', border: '1px solid rgba(0,240,255,0.25)' }}>
              <div className="fw-bold text-info">🔍 Police</div>
              <div className="text-secondary" style={{ fontSize: '0.75rem' }}>Investigate suspects at night to uncover the Mafia.</div>
            </div>
          </div>
          <div className="col-6">
            <div className="p-2 rounded-3" style={{ background: 'rgba(245,158,11,0.1)', border: '1px solid rgba(245,158,11,0.25)' }}>
              <div className="fw-bold text-warning">🧑 Civilian</div>
              <div className="text-secondary" style={{ fontSize: '0.75rem' }}>Discuss & vote out the Mafia during the Day phase.</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
