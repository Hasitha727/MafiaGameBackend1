import React, { useEffect, useState } from 'react';
import api from '../services/api';

export default function LeaderboardPage() {
  const [players, setPlayers] = useState([]);
  const [loading, setLoading] = useState(true);

  const loadLeaderboard = async () => {
    setLoading(true);
    try {
      const { data } = await api.get('/leaderboard?top=25');
      setPlayers(data);
    } catch (err) {
      console.error('Failed to load leaderboard:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadLeaderboard();
  }, []);

  const medalFor = (idx) => (idx === 0 ? '🥇' : idx === 1 ? '🥈' : idx === 2 ? '🥉' : `#${idx + 1}`);

  return (
    <div className="d-flex flex-column gap-3">
      <div className="d-flex justify-content-between align-items-center">
        <div>
          <h4 className="font-display fw-bold mb-0">🏆 Global Rankings</h4>
          <div className="small text-secondary">Top Mafia & Town strategists</div>
        </div>
        <button className="btn btn-sm btn-game-primary px-3" onClick={loadLeaderboard}>
          Refresh
        </button>
      </div>

      <div className="game-card d-flex flex-column gap-2">
        {loading && <div className="text-center text-secondary py-4">Loading leaderboard...</div>}
        {!loading && players.length === 0 && (
          <div className="text-center text-secondary py-4">No players ranked yet. Play a match!</div>
        )}
        {!loading &&
          players.map((p, i) => (
            <div key={p.userId} className="player-tile">
              <div className="d-flex align-items-center gap-3">
                <div className="font-display fw-bold" style={{ fontSize: '1.15rem', minWidth: 32 }}>
                  {medalFor(i)}
                </div>
                <div>
                  <div className="fw-bold">{p.username}</div>
                  <div className="text-secondary" style={{ fontSize: '0.75rem' }}>
                    {p.gamesPlayed} Games • {p.gamesWon} Won • Reliability {p.reliabilityScore}%
                  </div>
                </div>
              </div>
              <span className="badge bg-info bg-opacity-10 text-info border border-info px-2 py-1">
                {p.winRate}% WR
              </span>
            </div>
          ))}
      </div>
    </div>
  );
}
