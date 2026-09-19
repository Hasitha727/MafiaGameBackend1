namespace MafiaGame.Domain.Entities;

using MafiaGame.Domain.Enums;

public class RoomPlayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public RoleType Role { get; set; } = RoleType.Civilian;
    public bool IsAlive { get; set; } = true;
    public bool IsHost { get; set; } = false;
    public bool IsConnected { get; set; } = true;
    public string? SignalRConnectionId { get; set; }
    public DateTime? DisconnectedAt { get; set; }

    // Navigation Properties
    public GameRoom Room { get; set; } = null!;
    public User User { get; set; } = null!;
}
