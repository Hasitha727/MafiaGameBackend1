namespace MafiaGame.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IRoomRepository Rooms { get; }
    ILeaderboardRepository Leaderboard { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
