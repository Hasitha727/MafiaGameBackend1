namespace MafiaGame.Domain.Entities;

using MafiaGame.Domain.Enums;

public class GameEventLog
{
    public long Id { get; set; }
    public Guid RoomId { get; set; }
    public int DayNumber { get; set; }
    public GamePhase Phase { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GameRoom Room { get; set; } = null!;
}
