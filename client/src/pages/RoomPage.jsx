import React, { useEffect, useState, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import api from '../services/api';
import { createGameHubConnection } from '../services/signalrService';

export default function RoomPage() {
  const { code } = useParams();
  const roomCode = (code || '').toUpperCase();
  const navigate = useNavigate();
  const { token, user } = useAuth();

  const [roomState, setRoomState] = useState(null);
  const [connStatus, setConnStatus] = useState('Connecting...');
  const [secretRole, setSecretRole] = useState('');
  const [hideRole, setHideRole] = useState(false);
  const [feed, setFeed] = useState([]);
  const [chatText, setChatText] = useState('');
  const [secondsLeft, setSecondsLeft] = useState(null);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState('');

  const hubRef = useRef(null);
  const chatEndRef = useRef(null);

  const pushFeed = (entry) => {
    setFeed((prev) => [...prev, { id: Date.now() + Math.random(), ...entry }]);
  };

  // Auto-scroll chat feed
  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [feed]);

  // Live phase countdown timer
  useEffect(() => {
    if (!roomState?.phase_end_time) {
      setSecondsLeft(null);
      return;
    }
    const updateTimer = () => {
      const diff = Math.max(0, Math.floor((new Date(roomState.phase_end_time).getTime() - Date.now()) / 1000));
      setSecondsLeft(diff);
    };
    updateTimer();
    const interval = setInterval(updateTimer, 1000);
    return () => clearInterval(interval);
  }, [roomState?.phase_end_time]);

  // Connect SignalR & Load Initial Room Details
  useEffect(() => {
    let isMounted = true;
    const connection = createGameHubConnection(token, roomCode);
    hubRef.current = connection;

    connection.on('ReceiveRoomState', (state) => {
      if (!isMounted) return;
      setRoomState(state);
    });

    connection.on('ReceiveGameAnnouncement', (title, message) => {
      if (!isMounted) return;
      if (title === 'SECRET IDENTITY REVEALED') {
        setSecretRole(message);
      }
      pushFeed({ type: 'system', title, text: message });
    });

    connection.on('ReceiveVoteTrail', (voterId, voterName, targetId, targetName) => {
      if (!isMounted) return;
      pushFeed({ type: 'vote', text: `🗳️ ${voterName} voted for ${targetName}` });
    });

    connection.on('ReceiveChatMessage', (senderName, message) => {
      if (!isMounted) return;
      pushFeed({
        type: 'msg',
        sender: senderName,
        mine: senderName?.toLowerCase() === user?.username?.toLowerCase(),
        text: message
      });
    });

    connection.on('PlayerStatusChanged', (userId, username, isConnected) => {
      if (!isMounted) return;
      pushFeed({
        type: 'system',
        title: 'ROOM',
        text: `${username} ${isConnected ? 'connected' : 'disconnected'}.`
      });
    });

    connection.on('PlayerAbandonedGame', (username, reason) => {
      if (!isMounted) return;
      pushFeed({ type: 'vote', text: `⚠️ ${username} eliminated: ${reason}` });
    });

    connection.onreconnecting(() => isMounted && setConnStatus('Reconnecting...'));
    connection.onreconnected(() => {
      if (!isMounted) return;
      setConnStatus('Connected');
      connection.invoke('JoinRoomGroup', roomCode).catch(() => {});
    });

    async function initRoom() {
      try {
        // Verify room exists via REST API (Axios)
        const { data } = await api.get(`/room/${roomCode}`);
        if (isMounted) setRoomState(data);

        await connection.start();
        if (!isMounted) return;
        setConnStatus('Connected');
        await connection.invoke('JoinRoomGroup', roomCode);
      } catch (err) {
        if (!isMounted) return;
        setError(err.response?.data?.error || 'Could not join room. Check the room code.');
        setConnStatus('Disconnected');
      }
    }

    initRoom();

    return () => {
      isMounted = false;
      connection.stop().catch(() => {});
    };
  }, [roomCode, token, user?.username]);

  const myPlayer = roomState?.players?.find(
    (p) => p.username?.toLowerCase() === user?.username?.toLowerCase()
  );
  const phase = roomState?.status || 'Lobby';
  const isHost = Boolean(myPlayer?.is_host);

  const handleStartGame = async () => {
    try {
      await hubRef.current?.invoke('StartGame', roomCode);
    } catch (e) {
      pushFeed({ type: 'system', title: 'ERROR', text: e.message });
    }
  };

  const handleCastAction = async (targetUserId, targetUsername) => {
    try {
      await hubRef.current?.invoke('CastVote', roomCode, targetUserId);
      if (phase === 'Night') {
        pushFeed({ type: 'vote', text: `🎯 You targeted ${targetUsername} for night action.` });
      }
    } catch (e) {
      console.error(e);
    }
  };

  const handleSendChat = async (e) => {
    e.preventDefault();
    const trimmed = chatText.trim();
    if (!trimmed) return;
    try {
      await hubRef.current?.invoke('SendChatMessage', roomCode, trimmed);
      setChatText('');
    } catch (err) {
      console.error(err);
    }
  };

  const handleShareCode = async () => {
    const shareUrl = `${window.location.origin}/room/${roomCode}`;
    if (navigator.share) {
      try {
        await navigator.share({
          title: 'Join my Mafia Online Game!',
          text: `Join my Mafia Room using code: ${roomCode}`,
          url: shareUrl
        });
        return;
      } catch {
        // fallback to clipboard
      }
    }
    await navigator.clipboard.writeText(roomCode);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  if (error) {
    return (
      <div className="game-card text-center py-4">
        <div style={{ fontSize: '2.5rem' }}>🚫</div>
        <h5 className="font-display fw-bold mt-2">{error}</h5>
        <button className="btn btn-game-primary mt-3" onClick={() => navigate('/home')}>
          Back to Lobby
        </button>
      </div>
    );
  }

  const phaseClass =
    phase === 'Night' ? 'phase-night' : phase === 'Day' ? 'phase-day' : 'phase-lobby';
  const phaseEmoji = phase === 'Night' ? '🌙' : phase === 'Day' ? '☀️' : '⏳';

  return (
    <div className="d-flex flex-column gap-3">
      {/* Room Header & Share Bar */}
      <div className="game-card d-flex justify-content-between align-items-center py-3">
        <div>
          <div className="text-secondary" style={{ fontSize: '0.72rem', letterSpacing: '1px' }}>
            ROOM CODE
          </div>
          <div className="room-code-display">{roomCode}</div>
        </div>
        <div className="d-flex gap-2">
          <button type="button" className="btn btn-sm btn-game-primary px-3" onClick={handleShareCode}>
            {copied ? '✅ Copied' : '📤 Share'}
          </button>
          <button
            type="button"
            className="btn btn-sm btn-outline-light border-secondary px-3"
            onClick={() => navigate('/home')}
          >
            Leave
          </button>
        </div>
      </div>

      {/* Live Phase Banner + Countdown */}
      <div className={`phase-banner ${phaseClass}`}>
        <div>
          <div className="font-display fw-bold" style={{ fontSize: '1.1rem' }}>
            {phaseEmoji} Phase: {phase} {roomState?.current_day > 0 ? `(Day ${roomState.current_day})` : ''}
          </div>
          <div className="small text-light opacity-75">
            {connStatus === 'Connected' ? '🟢 Live SignalR Sync' : `🟡 ${connStatus}`}
          </div>
        </div>
        {secondsLeft !== null && phase !== 'Lobby' && (
          <div className="text-end">
            <div className="font-display fw-bold text-info" style={{ fontSize: '1.4rem', lineHeight: 1 }}>
              {secondsLeft}s
            </div>
            <div style={{ fontSize: '0.7rem' }} className="text-secondary">
              LEFT
            </div>
          </div>
        )}
      </div>

      {/* Secret Role Card (Tap to Hide/Show) */}
      {secretRole && (
        <div
          className="secret-role-card"
          role="button"
          onClick={() => setHideRole((v) => !v)}
        >
          <div className="small text-secondary mb-1">
            {hideRole ? 'Tap card to reveal your secret role' : 'Tap card to hide from nearby players'}
          </div>
          <h5 className="font-display fw-bold text-info mb-0">
            {hideRole ? '🕵️ •••••••••••••' : secretRole}
          </h5>
        </div>
      )}

      {/* Host Start Game Controls */}
      {isHost && phase === 'Lobby' && (
        <button type="button" className="btn btn-game-green w-100 py-3" onClick={handleStartGame}>
          ▶️ START GAME ({roomState?.players?.length || 1} Players Ready • Min 3)
        </button>
      )}

      {/* Players Roster */}
      <div className="game-card">
        <div className="d-flex justify-content-between align-items-center mb-2">
          <h6 className="font-display fw-bold mb-0">
            👥 Players ({roomState?.players?.length || 0})
          </h6>
          <span className="small text-secondary">
            {phase === 'Day' ? 'Tap Vote to eliminate' : phase === 'Night' ? 'Tap Target for Night Action' : 'Waiting in Lobby'}
          </span>
        </div>

        <div className="d-flex flex-column gap-2 mt-2">
          {roomState?.players?.map((p) => {
            const isMe = p.username?.toLowerCase() === user?.username?.toLowerCase();
            return (
              <div key={p.user_id} className={`player-tile ${p.is_alive ? '' : 'dead'}`}>
                <div>
                  <div className="fw-semibold">
                    {p.username} {p.is_host && '👑'} {isMe && <span className="text-info small">(You)</span>}
                  </div>
                  <div style={{ fontSize: '0.74rem' }} className="text-secondary">
                    {p.is_alive ? '💚 Alive' : '💀 Eliminated'} • {p.is_connected ? 'Online' : 'Offline'}
                  </div>
                </div>

                <div className="d-flex gap-2 align-items-center">
                  {!isMe && p.is_alive && phase === 'Day' && (
                    <button
                      type="button"
                      className="btn btn-sm btn-game-amber px-3"
                      onClick={() => handleCastAction(p.user_id, p.username)}
                    >
                      🗳️ Vote
                    </button>
                  )}
                  {!isMe && p.is_alive && phase === 'Night' && (
                    <button
                      type="button"
                      className="btn btn-sm btn-danger px-3 fw-bold"
                      onClick={() => handleCastAction(p.user_id, p.username)}
                    >
                      🎯 Target
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Town Discussion & Real-Time Feed */}
      <div className="game-card">
        <h6 className="font-display fw-bold mb-2">💬 Town Chat & Live Feed</h6>
        <div className="chat-stream">
          {feed.length === 0 && (
            <div className="text-secondary small text-center my-auto">
              Lobby ready. Invite friends or send a message below!
            </div>
          )}
          {feed.map((item) => {
            if (item.type === 'system') {
              return (
                <div key={item.id} className="chat-bubble system">
                  <strong>[{item.title}]</strong> {item.text}
                </div>
              );
            }
            if (item.type === 'vote') {
              return (
                <div key={item.id} className="chat-bubble vote">
                  {item.text}
                </div>
              );
            }
            return (
              <div key={item.id} className={`chat-bubble msg ${item.mine ? 'mine' : ''}`}>
                {!item.mine && (
                  <div className="fw-bold text-warning" style={{ fontSize: '0.74rem' }}>
                    {item.sender}
                  </div>
                )}
                <div>{item.text}</div>
              </div>
            );
          })}
          <div ref={chatEndRef} />
        </div>

        <form onSubmit={handleSendChat} className="d-flex gap-2 mt-2">
          <input
            type="text"
            className="form-control game-input"
            placeholder="Accuse, defend or discuss..."
            value={chatText}
            onChange={(e) => setChatText(e.target.value)}
          />
          <button type="submit" className="btn btn-game-green px-3">
            Send
          </button>
        </form>
      </div>
    </div>
  );
}
