namespace MafiaGame.Application.Interfaces;

using MafiaGame.Domain.Entities;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail);
    Task<bool> ExistsByUsernameOrEmailAsync(string username, string email);
    Task<int> CountAsync();
    Task<IReadOnlyList<User>> GetAllWithStatsAsync();
}
