namespace MafiaGame.Domain.Entities;

public class PlayerGameStats
{
    public Guid UserId { get; set; }
    public int GamesPlayed { get; set; } = 0;
    public int GamesWon { get; set; } = 0;
    public int SuccessfulMafiaVotes { get; set; } = 0;
    public int BadPoints { get; set; } = 0;

    // Computed Leaderboard Metric Properties
    public double WinRate => GamesPlayed == 0 ? 0 : Math.Round((double)GamesWon / GamesPlayed * 100, 2);
    public double ReliabilityScore => GamesPlayed == 0 ? 100 : Math.Max(0, Math.Round(100 - ((double)BadPoints / GamesPlayed * 20), 2));

    // Navigation Property
    public User User { get; set; } = null!;
}
