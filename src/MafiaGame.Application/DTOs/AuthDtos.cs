namespace MafiaGame.Application.DTOs;

public record RegisterDto(string Username, string Email, string Password);

public record LoginDto(string UsernameOrEmail, string Password);

public record AuthResponseDto(Guid UserId, string Username, string Email, string Token);

public record CreateRoomDto(int Modifiers = 0);

public record LeaderboardDto(
    Guid UserId, 
    string Username, 
    int GamesPlayed, 
    int GamesWon, 
    double WinRate, 
    int SuccessfulMafiaVotes, 
    int BadPoints, 
    double ReliabilityScore
);
