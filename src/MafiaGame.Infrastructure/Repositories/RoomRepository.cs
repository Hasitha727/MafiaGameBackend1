namespace MafiaGame.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;
using MafiaGame.Infrastructure.Persistence;

public class RoomRepository : Repository<GameRoom>, IRoomRepository
{
    public RoomRepository(ApplicationDbContext context) : base(context) { }

    public async Task<GameRoom?> GetByCodeWithPlayersAsync(string roomCode) =>
        await _context.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode.ToUpper());

    public async Task<IReadOnlyList<GameRoom>> GetAllWithPlayersAsync() =>
        await _context.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task AddPlayerAsync(RoomPlayer player) =>
        await _context.RoomPlayers.AddAsync(player);
}
