namespace MafiaGame.Application.Interfaces;

using MafiaGame.Domain.Entities;

public interface IJwtService
{
    string GenerateToken(User user);
}
