namespace MafiaGame.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;
using MafiaGame.Infrastructure.Persistence;

public class LeaderboardRepository : Repository<PlayerGameStats>, ILeaderboardRepository
{
    public LeaderboardRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<PlayerGameStats>> GetTopWithUsersAsync(int top) =>
        await _context.PlayerGameStats
            .Include(s => s.User)
            .Take(top)
            .ToListAsync();
}
