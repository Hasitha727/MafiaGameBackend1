namespace MafiaGame.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;
using MafiaGame.Infrastructure.Persistence;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail) =>
        await _context.Users
            .Include(u => u.Stats)
            .FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail);

    public async Task<bool> ExistsByUsernameOrEmailAsync(string username, string email) =>
        await _context.Users.AnyAsync(u => u.Username == username || u.Email == email);

    public async Task<int> CountAsync() => await _context.Users.CountAsync();

    public async Task<IReadOnlyList<User>> GetAllWithStatsAsync() =>
        await _context.Users
            .Include(u => u.Stats)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
}
