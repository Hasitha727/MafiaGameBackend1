namespace MafiaGame.Infrastructure.Hubs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;
using MafiaGame.Domain.Enums;
using MafiaGame.Infrastructure.Persistence;

[Authorize]
public class GameHub : Hub<IGameHubClient>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IGracePeriodManager _gracePeriodManager;
    private readonly IServiceScopeFactory _scopeFactory;

    public GameHub(
        ApplicationDbContext dbContext,
        IGracePeriodManager gracePeriodManager,
        IServiceScopeFactory scopeFactory)
    {
        _dbContext = dbContext;
        _gracePeriodManager = gracePeriodManager;
        _scopeFactory = scopeFactory;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdFromClaims();
        var httpContext = Context.GetHttpContext();
        var roomCode = httpContext?.Request.Query["roomCode"].ToString();

        if (!string.IsNullOrEmpty(roomCode))
        {
            var roomPlayer = await _dbContext.RoomPlayers
                .Include(rp => rp.Room)
                .Include(rp => rp.User)
                .FirstOrDefaultAsync(rp => rp.UserId == userId && rp.Room.RoomCode == roomCode);

            if (roomPlayer != null)
            {
                roomPlayer.IsConnected = true;
                roomPlayer.SignalRConnectionId = Context.ConnectionId;
                roomPlayer.DisconnectedAt = null;
                await _dbContext.SaveChangesAsync();

                _gracePeriodManager.CancelGracePeriod(userId);

                await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
                await Clients.Group(roomCode).PlayerStatusChanged(userId, roomPlayer.User.Username, true);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdFromClaims();

        var roomPlayer = await _dbContext.RoomPlayers
            .Include(rp => rp.Room)
            .Include(rp => rp.User)
            .FirstOrDefaultAsync(rp => rp.UserId == userId && rp.IsConnected);

        if (roomPlayer != null && roomPlayer.Room.Status != GamePhase.Ended)
        {
            roomPlayer.IsConnected = false;
            roomPlayer.DisconnectedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            var roomCode = roomPlayer.Room.RoomCode;
            var roomId = roomPlayer.RoomId;

            await Clients.Group(roomCode).PlayerStatusChanged(userId, roomPlayer.User.Username, false);

            _gracePeriodManager.StartGracePeriod(userId, TimeSpan.FromSeconds(60), async () =>
            {
                await HandlePlayerAbandonmentAsync(userId, roomId, roomCode);
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoomGroup(string roomCode)
    {
        var userId = GetUserIdFromClaims();
        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode.ToUpper());

        if (room == null) return;

        var existingPlayer = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (existingPlayer == null && room.Status == GamePhase.Lobby)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user != null)
            {
                var newPlayer = new RoomPlayer
                {
                    RoomId = room.Id,
                    UserId = userId,
                    IsHost = false,
                    IsAlive = true,
                    IsConnected = true,
                    SignalRConnectionId = Context.ConnectionId
                };
                _dbContext.RoomPlayers.Add(newPlayer);
                await _dbContext.SaveChangesAsync();
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode.ToUpper());
        await BroadcastRoomState(roomCode.ToUpper());
    }

    public async Task StartGame(string roomCode)
    {
        var userId = GetUserIdFromClaims();
        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode.ToUpper());

        if (room == null || room.HostUserId != userId || room.Status != GamePhase.Lobby) return;

        var players = room.Players.ToList();
        if (players.Count < 3) return;

        // Role assignment logic
        var roles = AssignRolesForCount(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            players[i].Role = roles[i];
        }

        room.Status = GamePhase.Night;
        room.CurrentDay = 1;
        room.PhaseEndTime = DateTime.UtcNow.AddSeconds(45);

        await _dbContext.SaveChangesAsync();

        // Notify each client of their secret identity
        foreach (var p in players)
        {
            if (!string.IsNullOrEmpty(p.SignalRConnectionId))
            {
                await Clients.Client(p.SignalRConnectionId).ReceiveGameAnnouncement(
                    "SECRET IDENTITY REVEALED",
                    $"Your secret role is: [{p.Role.ToString().ToUpper()}]",
                    "Reveal"
                );
            }
        }

        await BroadcastRoomState(roomCode.ToUpper());
    }

    public async Task BroadcastVoteTrail(string roomCode, Guid targetPlayerId)
    {
        var voterId = GetUserIdFromClaims();

        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode.ToUpper());

        if (room == null || room.Status != GamePhase.Day) return;

        var voter = room.Players.FirstOrDefault(p => p.UserId == voterId && p.IsAlive);
        var target = room.Players.FirstOrDefault(p => p.UserId == targetPlayerId && p.IsAlive);

        if (voter == null || target == null) return;

        bool isBlindVoting = room.Modifiers.HasFlag(RoomModifier.BlindVoting);

        if (!isBlindVoting)
        {
            await Clients.Group(roomCode.ToUpper()).ReceiveVoteTrail(
                voter.UserId,
                voter.User.Username,
                target.UserId,
                target.User.Username,
                voteCount: 1
            );
        }
        else
        {
            await Clients.Group(roomCode.ToUpper()).ReceiveVoteTrail(
                Guid.Empty,
                "Anonymous",
                target.UserId,
                target.User.Username,
                voteCount: 1
            );
        }
    }

    private async Task BroadcastRoomState(string roomCode)
    {
        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode.ToUpper());

        if (room == null) return;

        var state = new
        {
            room_code = room.RoomCode,
            status = room.Status.ToString(),
            current_day = room.CurrentDay,
            phase_end_time = room.PhaseEndTime,
            modifiers = room.Modifiers.ToString(),
            players = room.Players.Select(p => new
            {
                user_id = p.UserId,
                username = p.User.Username,
                is_alive = p.IsAlive,
                is_host = p.IsHost,
                is_connected = p.IsConnected
            })
        };

        await Clients.Group(roomCode.ToUpper()).ReceiveRoomState(state);
    }

    private List<RoleType> AssignRolesForCount(int count)
    {
        var roles = new List<RoleType>();
        int mafiaCount = Math.Max(1, count / 3);
        for (int i = 0; i < mafiaCount; i++) roles.Add(RoleType.Mafia);

        if (count >= 4) roles.Add(RoleType.Police);
        if (count >= 5) roles.Add(RoleType.Doctor);
        if (count >= 6) roles.Add(RoleType.Dog);
        if (count >= 7) roles.Add(RoleType.SerialKiller);

        while (roles.Count < count)
        {
            roles.Add(RoleType.Civilian);
        }

        var random = new Random();
        return roles.OrderBy(_ => random.Next()).ToList();
    }

    private async Task HandlePlayerAbandonmentAsync(Guid userId, Guid roomId, string roomCode)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var player = await db.RoomPlayers
            .Include(p => p.User)
            .Include(p => p.User.Stats)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.RoomId == roomId);

        if (player != null && !player.IsConnected && player.IsAlive)
        {
            player.IsAlive = false;

            if (player.User.Stats != null)
            {
                player.User.Stats.BadPoints += 1;
            }

            await db.SaveChangesAsync();

            await Clients.Group(roomCode).PlayerAbandonedGame(
                player.User.Username,
                "Disconnected and failed to reconnect within the 60-second grace period."
            );
        }
    }

    private Guid GetUserIdFromClaims()
    {
        var claim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
