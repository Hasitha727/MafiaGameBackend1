namespace MafiaGame.Infrastructure.Repositories;

using MafiaGame.Application.Interfaces;
using MafiaGame.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public IUserRepository Users { get; }
    public IRoomRepository Rooms { get; }
    public ILeaderboardRepository Leaderboard { get; }

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Users = new UserRepository(_context);
        Rooms = new RoomRepository(_context);
        Leaderboard = new LeaderboardRepository(_context);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);

    public void Dispose() => _context.Dispose();
}
