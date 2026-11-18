namespace MafiaGame.Application.Interfaces;

using MafiaGame.Domain.Entities;

public interface ILeaderboardRepository : IRepository<PlayerGameStats>
{
    Task<IReadOnlyList<PlayerGameStats>> GetTopWithUsersAsync(int top);
}
