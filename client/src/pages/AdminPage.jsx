import React, { useEffect, useState } from 'react';
import api from '../services/api';

export default function AdminPage() {
  const [overview, setOverview] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const loadOverview = async () => {
    setLoading(true);
    setError('');
    try {
      const { data } = await api.get('/admin/overview');
      setOverview(data);
    } catch (err) {
      setError(err.response?.status === 403 ? 'Access Denied: Admin role required (RBAC).' : 'Failed to load admin dashboard.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadOverview();
  }, []);

  const handleCloseRoom = async (roomCode) => {
    try {
      await api.delete(`/admin/rooms/${roomCode}`);
      loadOverview();
    } catch (err) {
      alert(err.response?.data?.error || 'Could not close room.');
    }
  };

  return (
    <div className="d-flex flex-column gap-3">
      <div className="d-flex justify-content-between align-items-center">
        <div>
          <h4 className="font-display fw-bold mb-0">🛡️ Admin Console</h4>
          <div className="small text-secondary">Role-Based Access Control (RBAC)</div>
        </div>
        <button className="btn btn-sm btn-game-primary px-3" onClick={loadOverview}>
          Refresh
        </button>
      </div>

      {error && <div className="alert alert-danger small rounded-3">{error}</div>}
      {loading && <div className="text-center text-secondary py-4">Loading server metrics...</div>}

      {!loading && overview && (
        <>
          <div className="row g-2">
            <div className="col-4">
              <div className="game-card text-center py-3">
                <div className="font-display fw-bold text-info fs-3">{overview.totalUsers}</div>
                <div className="text-secondary" style={{ fontSize: '0.72rem' }}>PLAYERS</div>
              </div>
            </div>
            <div className="col-4">
              <div className="game-card text-center py-3">
                <div className="font-display fw-bold text-success fs-3">{overview.activeRooms}</div>
                <div className="text-secondary" style={{ fontSize: '0.72rem' }}>ACTIVE ROOMS</div>
              </div>
            </div>
            <div className="col-4">
              <div className="game-card text-center py-3">
                <div className="font-display fw-bold text-warning fs-3">{overview.totalRooms}</div>
                <div className="text-secondary" style={{ fontSize: '0.72rem' }}>TOTAL ROOMS</div>
              </div>
            </div>
          </div>

          <div className="game-card">
            <h6 className="font-display fw-bold mb-3">🎮 Active & Recent Rooms</h6>
            {overview.rooms.length === 0 ? (
              <div className="text-secondary small">No rooms created yet.</div>
            ) : (
              <div className="d-flex flex-column gap-2">
                {overview.rooms.map((r) => (
                  <div key={r.roomCode} className="player-tile">
                    <div>
                      <div className="fw-bold text-info">{r.roomCode}</div>
                      <div className="text-secondary" style={{ fontSize: '0.75rem' }}>
                        Phase: {r.status} • {r.playerCount} Players
                      </div>
                    </div>
                    <button
                      type="button"
                      className="btn btn-sm btn-outline-danger"
                      onClick={() => handleCloseRoom(r.roomCode)}
                    >
                      Close
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
