import React, { useEffect, useState, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import api from '../services/api';
import { createGameHubConnection } from '../services/signalrService';

const QUICK_CHIPS = {
  Singlish: [
    'Mama nemei mafia! 😅',
    'Saka hithenawa 👀',
    'Wadi manaapeta ekagai 🗳️',
    'Skip karamu ⏭️'
  ],
  English: [
    "I'm innocent! 😅",
    'Super sus! 👀',
    'I agree, vote! 🗳️',
    "Let's skip ⏭️"
  ]
};

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
  const [mySelectedTarget, setMySelectedTarget] = useState(null);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState('');

  const hubRef = useRef(null);
  const chatEndRef = useRef(null);
  const lastPhaseKeyRef = useRef('');

  const pushFeed = (entry) => {
    setFeed((prev) => [...prev, { id: Date.now() + Math.random(), ...entry }]);
  };

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [feed]);

  // Reset selected target whenever phase or day changes
  useEffect(() => {
    const key = `${roomState?.status}-${roomState?.current_day}`;
    if (key !== lastPhaseKeyRef.current) {
      lastPhaseKeyRef.current = key;
      setMySelectedTarget(null);
      if (roomState?.status === 'Lobby') {
        setSecretRole('');
      }
    }
  }, [roomState?.status, roomState?.current_day]);

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
      pushFeed({ type: 'vote', text: `🗳️ ${voterName} voted → ${targetName}` });
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
  const amAlive = myPlayer ? myPlayer.is_alive : true;
  const chatLanguage = roomState?.chat_language || 'Singlish';
  const voteCounts = roomState?.vote_counts || {};
  const skipVotes = roomState?.skip_votes || 0;
  const playerCount = roomState?.players?.length || 0;

  const isSpectator = !amAlive && phase !== 'Lobby' && phase !== 'Ended';

  const handleAddBot = async () => {
    try {
      await hubRef.current?.invoke('AddBot', roomCode);
    } catch (e) {
      console.error(e);
    }
  };

  const handleRemoveBot = async (botUserId) => {
    try {
      await hubRef.current?.invoke('RemoveBot', roomCode, botUserId);
    } catch (e) {
      console.error(e);
    }
  };

  const handleToggleLanguage = async (lang) => {
    try {
      await hubRef.current?.invoke('SetChatLanguage', roomCode, lang);
    } catch (e) {
      console.error(e);
    }
  };

  const handleStartGame = async () => {
    try {
      await hubRef.current?.invoke('StartGame', roomCode);
    } catch (e) {
      pushFeed({ type: 'system', title: 'ERROR', text: e.message });
    }
  };

  const handlePlayAgain = async () => {
    try {
      await hubRef.current?.invoke('PlayAgain', roomCode);
    } catch (e) {
      console.error(e);
    }
  };

  const handleCastAction = async (targetUserId) => {
    if (isSpectator) return;
    try {
      setMySelectedTarget(targetUserId);
      await hubRef.current?.invoke('CastVote', roomCode, targetUserId);
    } catch (e) {
      console.error(e);
    }
  };

  const sendMessageText = async (text) => {
    const trimmed = text.trim();
    if (!trimmed || isSpectator) return;
    try {
      await hubRef.current?.invoke('SendChatMessage', roomCode, trimmed);
    } catch (err) {
      console.error(err);
    }
  };

  const handleSendChat = async (e) => {
    e.preventDefault();
    if (!chatText.trim()) return;
    const msg = chatText;
    setChatText('');
    await sendMessageText(msg);
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

  const roleBadgeFor = (role) => {
    switch (role) {
      case 'Mafia': return <span className="badge bg-danger">🔫 MAFIA</span>;
      case 'Doctor': return <span className="badge bg-success">🩺 DOCTOR</span>;
      case 'Police': return <span className="badge bg-info text-dark">🔍 POLICE</span>;
      case 'SerialKiller': return <span className="badge bg-warning text-dark">🔪 KILLER</span>;
      default: return <span className="badge bg-secondary">🧑 CIVILIAN</span>;
    }
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
  const phaseEmoji =
    phase === 'Night' ? '🌙' : phase === 'Day' ? '☀️' : phase === 'Ended' ? '🏁' : '⏳';

  return (
    <div className="d-flex flex-column gap-3">
      {/* Room Header & Share Bar */}
      <div className="game-card d-flex justify-content-between align-items-center py-3">
        <div>
          <div className="text-secondary" style={{ fontSize: '0.72rem', letterSpacing: '1px' }}>
            ROOM CODE • {chatLanguage === 'Singlish' ? '🇱🇰 Singlish' : '🇬🇧 English'}
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
            {phaseEmoji} Phase: {phase} {roomState?.current_day > 0 && phase !== 'Lobby' ? `(Day ${roomState.current_day})` : ''}
          </div>
          <div className="small text-light opacity-75">
            {connStatus === 'Connected' ? '🟢 Live SignalR Sync' : `🟡 ${connStatus}`}
          </div>
        </div>
        {secondsLeft !== null && phase !== 'Lobby' && phase !== 'Ended' && (
          <div className="text-end">
            <div className="font-display fw-bold text-info" style={{ fontSize: '1.45rem', lineHeight: 1 }}>
              {secondsLeft}s
            </div>
            <div style={{ fontSize: '0.7rem' }} className="text-secondary">
              LEFT
            </div>
          </div>
        )}
      </div>

      {/* Game Over Victory Card */}
      {phase === 'Ended' && (
        <div
          className="game-card text-center py-4"
          style={{
            background:
              roomState?.winner_team === 'Town'
                ? 'linear-gradient(135deg, rgba(16, 185, 129, 0.28), rgba(0, 240, 255, 0.16))'
                : 'linear-gradient(135deg, rgba(244, 63, 94, 0.3), rgba(88, 28, 135, 0.25))',
            borderColor: roomState?.winner_team === 'Town' ? '#10B981' : '#F43F5E'
          }}
        >
          <div style={{ fontSize: '2.6rem' }}>
            {roomState?.winner_team === 'Town' ? '🏆' : '🩸'}
          </div>
          <h3 className="font-display fw-bold mt-1 mb-1">
            {roomState?.winner_team === 'Town' ? 'TOWN WINS!' : 'MAFIA WINS!'}
          </h3>
          <p className="small text-light opacity-75 mb-3">
            All secret roles are revealed below. Leaderboard updated!
          </p>
          {isHost && (
            <button type="button" className="btn btn-game-green px-4" onClick={handlePlayAgain}>
              🔄 PLAY AGAIN (REMATCH)
            </button>
          )}
        </div>
      )}

      {/* Spectator Notice */}
      {isSpectator && (
        <div className="alert alert-secondary py-2 px-3 small rounded-3 mb-0 text-center">
          👻 <strong>You were eliminated!</strong> You are spectating until the match ends.
        </div>
      )}

      {/* Secret Role Card (Tap to Hide/Show) */}
      {secretRole && phase !== 'Ended' && (
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

      {/* Host Lobby Controls: Add AI Bots, Language Switch, Start Game */}
      {isHost && phase === 'Lobby' && (
        <div className="game-card d-flex flex-column gap-2">
          <div className="d-flex justify-content-between align-items-center mb-1">
            <span className="small fw-semibold text-secondary">Host Lobby Setup ({playerCount}/8)</span>
            <div className="btn-group btn-group-sm">
              <button
                type="button"
                className={`btn ${chatLanguage === 'Singlish' ? 'btn-info text-dark fw-bold' : 'btn-outline-secondary'}`}
                onClick={() => handleToggleLanguage('Singlish')}
              >
                🇱🇰 Singlish
              </button>
              <button
                type="button"
                className={`btn ${chatLanguage === 'English' ? 'btn-info text-dark fw-bold' : 'btn-outline-secondary'}`}
                onClick={() => handleToggleLanguage('English')}
              >
                🇬🇧 English
              </button>
            </div>
          </div>

          <div className="d-flex gap-2">
            <button
              type="button"
              className="btn btn-game-primary flex-fill"
              disabled={playerCount >= 8}
              onClick={handleAddBot}
            >
              🤖 + Add AI Bot
            </button>
            <button
              type="button"
              className="btn btn-game-green flex-fill"
              disabled={playerCount < 3}
              onClick={handleStartGame}
            >
              ▶️ START ({playerCount}/3+)
            </button>
          </div>
        </div>
      )}

      {/* Players Roster */}
      <div className="game-card">
        <div className="d-flex justify-content-between align-items-center mb-2">
          <h6 className="font-display fw-bold mb-0">
            👥 Players ({playerCount})
          </h6>
          <span className="small text-secondary">
            {phase === 'Day'
              ? 'Tap Vote to eliminate'
              : phase === 'Night'
              ? 'Special roles: Tap Target'
              : phase === 'Ended'
              ? 'Final Roles Revealed'
              : 'Waiting in Lobby'}
          </span>
        </div>

        <div className="d-flex flex-column gap-2 mt-2">
          {roomState?.players?.map((p) => {
            const isMe = p.username?.toLowerCase() === user?.username?.toLowerCase();
            const votesOnPlayer = voteCounts[p.user_id] || 0;
            const isSelected = mySelectedTarget === p.user_id;

            return (
              <div key={p.user_id} className={`player-tile ${p.is_alive ? '' : 'dead'}`}>
                <div>
                  <div className="fw-semibold d-flex align-items-center gap-1 flex-wrap">
                    <span>{p.is_bot ? '🤖' : '👤'} {p.username}</span>
                    {p.is_host && <span title="Host">👑</span>}
                    {isMe && <span className="text-info small">(You)</span>}
                    {phase === 'Ended' && p.role && roleBadgeFor(p.role)}
                  </div>
                  <div style={{ fontSize: '0.74rem' }} className="text-secondary">
                    {p.is_alive ? '💚 Alive' : '💀 Eliminated'}
                    {p.is_bot && p.bot_personality ? ` • AI (${p.bot_personality})` : ` • ${p.is_connected ? 'Online' : 'Offline'}`}
                  </div>
                </div>

                <div className="d-flex gap-2 align-items-center">
                  {/* Live Day Vote Tally Badge */}
                  {phase === 'Day' && votesOnPlayer > 0 && (
                    <span className="badge bg-warning text-dark fw-bold px-2 py-1">
                      🗳️ {votesOnPlayer}
                    </span>
                  )}

                  {/* Lobby Remove Bot Button */}
                  {isHost && phase === 'Lobby' && p.is_bot && (
                    <button
                      type="button"
                      className="btn btn-sm btn-outline-danger py-0 px-2"
                      onClick={() => handleRemoveBot(p.user_id)}
                      title="Remove Bot"
                    >
                      ✖
                    </button>
                  )}

                  {/* Day Phase Vote Button */}
                  {!isMe && p.is_alive && amAlive && phase === 'Day' && (
                    <button
                      type="button"
                      className={`btn btn-sm px-3 ${isSelected ? 'btn-success fw-bold' : 'btn-game-amber'}`}
                      onClick={() => handleCastAction(p.user_id)}
                    >
                      {isSelected ? '✓ Voted' : '🗳️ Vote'}
                    </button>
                  )}

                  {/* Night Phase Target Button */}
                  {p.is_alive && amAlive && phase === 'Night' && (
                    <button
                      type="button"
                      className={`btn btn-sm px-3 fw-bold ${isSelected ? 'btn-info text-dark' : 'btn-danger'}`}
                      onClick={() => handleCastAction(p.user_id)}
                    >
                      {isSelected ? '✓ Locked' : '🎯 Target'}
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>

        {/* Day Phase Skip Vote Bar */}
        {phase === 'Day' && amAlive && (
          <button
            type="button"
            className={`btn btn-sm w-100 mt-3 ${
              mySelectedTarget === 'SKIP' ? 'btn-info text-dark fw-bold' : 'btn-outline-secondary text-light'
            }`}
            onClick={() => handleCastAction('SKIP')}
          >
            {mySelectedTarget === 'SKIP' ? `✓ You Voted to Skip (${skipVotes})` : `⏭️ Skip Vote (${skipVotes})`}
          </button>
        )}
      </div>

      {/* Town Discussion & Real-Time Feed */}
      <div className="game-card">
        <h6 className="font-display fw-bold mb-2">💬 Town Chat & Live Feed</h6>
        <div className="chat-stream">
          {feed.length === 0 && (
            <div className="text-secondary small text-center my-auto">
              Lobby ready. Add AI Bots or invite friends to begin!
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

        {/* 1-Tap Quick Reply Chips for Mobile */}
        {!isSpectator && (phase === 'Day' || phase === 'Lobby') && (
          <div className="d-flex flex-wrap gap-1 mt-2">
            {(QUICK_CHIPS[chatLanguage] || QUICK_CHIPS.Singlish).map((chip) => (
              <button
                key={chip}
                type="button"
                className="btn btn-sm rounded-pill text-light px-2 py-1"
                style={{
                  fontSize: '0.72rem',
                  background: 'rgba(255,255,255,0.07)',
                  border: '1px solid rgba(255,255,255,0.12)'
                }}
                onClick={() => sendMessageText(chip)}
              >
                {chip}
              </button>
            ))}
          </div>
        )}

        <form onSubmit={handleSendChat} className="d-flex gap-2 mt-2">
          <input
            type="text"
            className="form-control game-input"
            disabled={isSpectator}
            placeholder={
              isSpectator
                ? 'Spectator mode (eliminated)...'
                : chatLanguage === 'Singlish'
                ? 'Machan saka kawuda? Type karanna...'
                : 'Accuse, defend or discuss...'
            }
            value={chatText}
            onChange={(e) => setChatText(e.target.value)}
          />
          <button type="submit" disabled={isSpectator} className="btn btn-game-green px-3">
            Send
          </button>
        </form>
      </div>
    </div>
  );
}
