namespace MafiaGame.Domain.Entities;

using MafiaGame.Domain.Enums;

public class GameRoom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoomCode { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public GamePhase Status { get; set; } = GamePhase.Lobby;
    public int CurrentDay { get; set; } = 1;
    public DateTime? PhaseEndTime { get; set; }
    public string WinnerTeam { get; set; } = string.Empty;
    public RoomModifier Modifiers { get; set; } = RoomModifier.None;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<RoomPlayer> Players { get; set; } = new List<RoomPlayer>();
    public ICollection<GameEventLog> EventLogs { get; set; } = new List<GameEventLog>();
}
