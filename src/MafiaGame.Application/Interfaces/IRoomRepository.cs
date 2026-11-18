namespace MafiaGame.Application.Interfaces;

using MafiaGame.Domain.Entities;

public interface IRoomRepository : IRepository<GameRoom>
{
    Task<GameRoom?> GetByCodeWithPlayersAsync(string roomCode);
    Task<IReadOnlyList<GameRoom>> GetAllWithPlayersAsync();
    Task AddPlayerAsync(RoomPlayer player);
}
