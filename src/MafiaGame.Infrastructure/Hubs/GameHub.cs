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
    private static readonly (string Name, string Personality)[] BotRoster =
    {
        ("Kasun_Bot", "Aggressive"),
        ("Nimali_Bot", "Analyst"),
        ("Sahan_Bot", "Defensive"),
        ("Kavindi_Bot", "Quiet"),
        ("Dilshan_Bot", "Aggressive"),
        ("Tharushi_Bot", "Analyst"),
        ("Ravindu_Bot", "Defensive"),
        ("Amaya_Bot", "Quiet")
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IGracePeriodManager _gracePeriodManager;
    private readonly IGameStateManager _gameState;
    private readonly IServiceScopeFactory _scopeFactory;

    public GameHub(
        ApplicationDbContext dbContext,
        IGracePeriodManager gracePeriodManager,
        IGameStateManager gameState,
        IServiceScopeFactory scopeFactory)
    {
        _dbContext = dbContext;
        _gracePeriodManager = gracePeriodManager;
        _gameState = gameState;
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
                .FirstOrDefaultAsync(rp => rp.UserId == userId && rp.Room.RoomCode == roomCode.ToUpper());

            if (roomPlayer != null)
            {
                roomPlayer.IsConnected = true;
                roomPlayer.SignalRConnectionId = Context.ConnectionId;
                roomPlayer.DisconnectedAt = null;
                await _dbContext.SaveChangesAsync();

                _gracePeriodManager.CancelGracePeriod(userId);

                await Groups.AddToGroupAsync(Context.ConnectionId, roomCode.ToUpper());
                await Clients.Group(roomCode.ToUpper()).PlayerStatusChanged(userId, roomPlayer.User.Username, true);
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
            .FirstOrDefaultAsync(rp => rp.UserId == userId && rp.IsConnected && !rp.IsBot);

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
        var code = roomCode.ToUpper();
        var userId = GetUserIdFromClaims();
        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == code);

        if (room == null) return;

        var existingPlayer = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (existingPlayer == null && room.Status == GamePhase.Lobby && room.Players.Count < 8)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user != null)
            {
                var newPlayer = new RoomPlayer
                {
                    RoomId = room.Id,
                    UserId = userId,
                    IsHost = false,
                    IsBot = false,
                    IsAlive = true,
                    IsConnected = true,
                    SignalRConnectionId = Context.ConnectionId
                };
                _dbContext.RoomPlayers.Add(newPlayer);
                await _dbContext.SaveChangesAsync();
            }
        }
        else if (existingPlayer != null)
        {
            existingPlayer.SignalRConnectionId = Context.ConnectionId;
            existingPlayer.IsConnected = true;
            existingPlayer.DisconnectedAt = null;
            await _dbContext.SaveChangesAsync();

            if (room.Status != GamePhase.Lobby && room.Status != GamePhase.Ended)
            {
                await Clients.Caller.ReceiveGameAnnouncement(
                    "SECRET IDENTITY REVEALED",
                    $"Your secret role is: [{existingPlayer.Role.ToString().ToUpper()}]",
                    "Reveal"
                );
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        await BroadcastRoomState(code);
    }

    public async Task AddBot(string roomCode)
    {
        var code = roomCode.ToUpper();
        var userId = GetUserIdFromClaims();
        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == code);

        if (room == null || room.HostUserId != userId || room.Status != GamePhase.Lobby) return;
        if (room.Players.Count >= 8)
        {
            await Clients.Caller.ReceiveGameAnnouncement("ROOM FULL", "Maximum 8 players allowed per room.", "Lobby");
            return;
        }

        var existingNames = room.Players.Select(p => p.User.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextBot = BotRoster.FirstOrDefault(b => !existingNames.Contains(b.Name));
        if (string.IsNullOrEmpty(nextBot.Name)) return;

        var botUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == nextBot.Name);
        if (botUser == null)
        {
            botUser = new User
            {
                Username = nextBot.Name,
                Email = $"{nextBot.Name.ToLower()}@ai.mafia.local",
                PasswordHash = "BOT_ACCOUNT_NO_LOGIN",
                Role = "Player",
                IsBot = true
            };
            _dbContext.Users.Add(botUser);
            await _dbContext.SaveChangesAsync();
        }

        var botPlayer = new RoomPlayer
        {
            RoomId = room.Id,
            UserId = botUser.Id,
            IsHost = false,
            IsBot = true,
            BotPersonality = nextBot.Personality,
            IsAlive = true,
            IsConnected = true
        };
        _dbContext.RoomPlayers.Add(botPlayer);
        await _dbContext.SaveChangesAsync();

        await Clients.Group(code).ReceiveGameAnnouncement("🤖 AI BOT JOINED", $"{nextBot.Name} ({nextBot.Personality}) entered the lobby!", "Lobby");
        await BroadcastRoomState(code);
    }

    public async Task RemoveBot(string roomCode, string botUserIdStr)
    {
        var code = roomCode.ToUpper();
        var userId = GetUserIdFromClaims();
        if (!Guid.TryParse(botUserIdStr, out var botUserId)) return;

        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == code);

        if (room == null || room.HostUserId != userId || room.Status != GamePhase.Lobby) return;

        var botPlayer = room.Players.FirstOrDefault(p => p.UserId == botUserId && p.IsBot);
        if (botPlayer == null) return;

        var botName = botPlayer.User.Username;
        _dbContext.RoomPlayers.Remove(botPlayer);
        await _dbContext.SaveChangesAsync();

        await Clients.Group(code).ReceiveGameAnnouncement("🤖 BOT REMOVED", $"{botName} left the lobby.", "Lobby");
        await BroadcastRoomState(code);
    }

    public async Task SetChatLanguage(string roomCode, string language)
    {
        var code = roomCode.ToUpper();
        var userId = GetUserIdFromClaims();
        var room = await _dbContext.GameRooms.FirstOrDefaultAsync(r => r.RoomCode == code);
        if (room == null || room.HostUserId != userId || room.Status != GamePhase.Lobby) return;

        room.ChatLanguage = language.Equals("English", StringComparison.OrdinalIgnoreCase) ? "English" : "Singlish";
        await _dbContext.SaveChangesAsync();

        await BroadcastRoomState(code);
    }

    public async Task StartGame(string roomCode)
    {
        var code = roomCode.ToUpper();
        var userId = GetUserIdFromClaims();
        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == code);

        if (room == null || room.HostUserId != userId || room.Status != GamePhase.Lobby) return;

        var players = room.Players.ToList();
        if (players.Count < 3)
        {
            await Clients.Caller.ReceiveGameAnnouncement(
                "NOT ENOUGH PLAYERS",
                $"At least 3 players are required to start (currently {players.Count}). Add AI Bots or invite friends!",
                "Lobby"
            );
            return;
        }

        // Balanced role assignment
        var roles = AssignRolesForCount(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            players[i].Role = roles[i];
            players[i].IsAlive = true;
        }

        room.Status = GamePhase.Night;
        room.CurrentDay = 1;
        room.WinnerTeam = string.Empty;
        room.PhaseEndTime = DateTime.UtcNow.AddSeconds(35);

        await _dbContext.SaveChangesAsync();

        // Initialize fresh match state & schedule staggered Night AI actions
        _gameState.ResetRoom(code);
        _gameState.StartNewPhase(code, players.Where(p => p.IsBot), isNight: true);

        var mafiaNames = players.Where(p => p.Role == RoleType.Mafia).Select(p => p.User.Username).ToList();

        // Notify each human client privately of their secret identity
        foreach (var p in players.Where(p => !p.IsBot))
        {
            if (!string.IsNullOrEmpty(p.SignalRConnectionId))
            {
                var roleMsg = $"Your secret role is: [{p.Role.ToString().ToUpper()}]";
                if (p.Role == RoleType.Mafia && mafiaNames.Count > 1)
                {
                    var partners = mafiaNames.Where(n => n != p.User.Username);
                    roleMsg += $" | Mafia Partner: {string.Join(", ", partners)}";
                }

                await Clients.Client(p.SignalRConnectionId).ReceiveGameAnnouncement(
                    "SECRET IDENTITY REVEALED",
                    roleMsg,
                    "Reveal"
                );
            }
        }

        bool isSinglish = room.ChatLanguage.Equals("Singlish", StringComparison.OrdinalIgnoreCase);
        await Clients.Group(code).ReceiveGameAnnouncement(
            "🌙 NIGHT 1 BEGINS",
            isSinglish
                ? "Raa una! Mafia, Doctor, Police roles thiyena aya thaman ge target eka select karanna!"
                : "Night falls on Day 1! Mafia, Doctor & Police — choose your night target now!",
            "Night"
        );

        await BroadcastRoomState(code);
    }

    public async Task PlayAgain(string roomCode)
    {
        var code = roomCode.ToUpper();
        var userId = GetUserIdFromClaims();
        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == code);

        if (room == null || room.HostUserId != userId) return;

        room.Status = GamePhase.Lobby;
        room.CurrentDay = 1;
        room.WinnerTeam = string.Empty;
        room.PhaseEndTime = null;

        foreach (var p in room.Players)
        {
            p.IsAlive = true;
            p.Role = RoleType.Civilian;
        }

        await _dbContext.SaveChangesAsync();
        _gameState.ResetRoom(code);

        await Clients.Group(code).ReceiveGameAnnouncement("🔄 NEW MATCH LOBBY", "Room reset for a rematch! Host can start whenever ready.", "Lobby");
        await BroadcastRoomState(code);
    }

    public async Task CastVote(string roomCode, string targetUserIdStr)
    {
        var code = roomCode.ToUpper();
        var voterId = GetUserIdFromClaims();

        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == code);

        if (room == null || room.Status == GamePhase.Lobby || room.Status == GamePhase.Ended) return;

        var voter = room.Players.FirstOrDefault(p => p.UserId == voterId);
        if (voter == null || !voter.IsAlive)
        {
            await Clients.Caller.ReceiveGameAnnouncement("👻 SPECTATOR MODE", "Eliminated players cannot vote or perform actions.", room.Status.ToString());
            return;
        }

        if (room.Status == GamePhase.Day)
        {
            Guid? targetId = null;
            RoomPlayer? target = null;

            if (!string.Equals(targetUserIdStr, "SKIP", StringComparison.OrdinalIgnoreCase))
            {
                if (!Guid.TryParse(targetUserIdStr, out var parsedId)) return;
                target = room.Players.FirstOrDefault(p => p.UserId == parsedId && p.IsAlive);
                if (target == null || target.UserId == voterId) return;
                targetId = target.UserId;
            }

            _gameState.RecordDayVote(code, voterId, targetId, room.Players);

            bool isBlindVoting = room.Modifiers.HasFlag(RoomModifier.BlindVoting);
            var voterDisplay = isBlindVoting ? "Anonymous" : voter.User.Username;
            var targetDisplay = target != null ? target.User.Username : "⏭️ SKIP VOTE";

            await Clients.Group(code).ReceiveVoteTrail(
                isBlindVoting ? Guid.Empty : voter.UserId,
                voterDisplay,
                targetId ?? Guid.Empty,
                targetDisplay,
                voteCount: 1
            );

            // Smart Fast-Forward if all alive players have voted
            if (_gameState.AreAllDayVotesIn(code, room.Players) && room.PhaseEndTime > DateTime.UtcNow.AddSeconds(3))
            {
                room.PhaseEndTime = DateTime.UtcNow.AddSeconds(3);
                await _dbContext.SaveChangesAsync();
            }

            await BroadcastRoomState(code);
        }
        else if (room.Status == GamePhase.Night)
        {
            if (voter.Role == RoleType.Civilian || voter.Role == RoleType.Jester || voter.Role == RoleType.Dog)
            {
                await Clients.Caller.ReceiveGameAnnouncement("🌙 SLEEPING", "Civilians sleep during the Night phase. Wait for sunrise!", "Night");
                return;
            }

            if (!Guid.TryParse(targetUserIdStr, out var parsedTargetId)) return;
            var target = room.Players.FirstOrDefault(p => p.UserId == parsedTargetId && p.IsAlive);
            if (target == null) return;

            if (voter.Role == RoleType.Mafia && target.Role == RoleType.Mafia)
            {
                await Clients.Caller.ReceiveGameAnnouncement("🔫 MAFIA RULE", "You cannot target a fellow Mafia member!", "Night");
                return;
            }

            _gameState.RecordNightAction(code, voterId, target.UserId);

            if (voter.Role == RoleType.Police)
            {
                bool isMafia = target.Role == RoleType.Mafia || target.Role == RoleType.SerialKiller;
                _gameState.RecordPoliceDiscovery(code, voterId, target.UserId, isMafia);
                await Clients.Caller.ReceiveGameAnnouncement(
                    "🔍 INVESTIGATION RESULT",
                    isMafia
                        ? $"ALERT! {target.User.Username} IS MAFIA / KILLER! 🔫 Expose them at sunrise!"
                        : $"{target.User.Username} appears INNOCENT ✅.",
                    "Night"
                );
            }
            else if (voter.Role == RoleType.Doctor)
            {
                await Clients.Caller.ReceiveGameAnnouncement(
                    "🩺 DOCTOR PROTECTION",
                    $"You are protecting {target.User.Username} tonight.",
                    "Night"
                );
            }
            else if (voter.Role == RoleType.Mafia || voter.Role == RoleType.SerialKiller)
            {
                await Clients.Caller.ReceiveGameAnnouncement(
                    "🎯 TARGET LOCKED",
                    $"Night strike locked onto {target.User.Username}.",
                    "Night"
                );
            }

            // Smart Fast-Forward if all night-role players have acted
            if (_gameState.AreAllNightActionsIn(code, room.Players) && room.PhaseEndTime > DateTime.UtcNow.AddSeconds(3))
            {
                room.PhaseEndTime = DateTime.UtcNow.AddSeconds(3);
                await _dbContext.SaveChangesAsync();
                await BroadcastRoomState(code);
            }
        }
    }

    public async Task SendChatMessage(string roomCode, string message)
    {
        var code = roomCode.ToUpper();
        var userId = GetUserIdFromClaims();
        if (string.IsNullOrWhiteSpace(message)) return;

        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == code);

        if (room == null) return;

        var player = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (player == null) return;

        // Dead players are spectators during active gameplay
        if (!player.IsAlive && room.Status != GamePhase.Lobby && room.Status != GamePhase.Ended)
        {
            await Clients.Caller.ReceiveGameAnnouncement("👻 SPECTATOR MODE", "Eliminated players cannot speak in Town Chat until the match ends.", room.Status.ToString());
            return;
        }

        var trimmed = message.Trim();
        await Clients.Group(code).ReceiveChatMessage(player.User.Username, trimmed);

        if (room.Status == GamePhase.Day)
        {
            _gameState.NotifyPlayerMentionedInChat(code, trimmed, room.Players, userId);
        }
    }

    private async Task BroadcastRoomState(string roomCode)
    {
        var room = await _dbContext.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode.ToUpper());

        if (room == null) return;

        var voteCounts = _gameState.GetDayVoteCounts(room.RoomCode)
            .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        var skipVotes = _gameState.GetSkipVoteCount(room.RoomCode);
        bool revealRoles = room.Status == GamePhase.Ended;

        var state = new
        {
            room_code = room.RoomCode,
            status = room.Status.ToString(),
            current_day = room.CurrentDay,
            phase_end_time = room.PhaseEndTime,
            winner_team = room.WinnerTeam,
            chat_language = room.ChatLanguage,
            modifiers = room.Modifiers.ToString(),
            vote_counts = voteCounts,
            skip_votes = skipVotes,
            players = room.Players.Select(p => new
            {
                user_id = p.UserId,
                username = p.User.Username,
                is_alive = p.IsAlive,
                is_host = p.IsHost,
                is_bot = p.IsBot,
                bot_personality = p.BotPersonality,
                is_connected = p.IsConnected,
                role = revealRoles ? p.Role.ToString() : null
            })
        };

        await Clients.Group(room.RoomCode).ReceiveRoomState(state);
    }

    private List<RoleType> AssignRolesForCount(int count)
    {
        var roles = new List<RoleType>();
        int mafiaCount = count >= 6 ? 2 : 1;
        for (int i = 0; i < mafiaCount; i++) roles.Add(RoleType.Mafia);

        if (count >= 4) roles.Add(RoleType.Police);
        if (count >= 5) roles.Add(RoleType.Doctor);
        if (count >= 7) roles.Add(RoleType.SerialKiller);

        while (roles.Count < count)
        {
            roles.Add(RoleType.Civilian);
        }

        return roles.OrderBy(_ => Random.Shared.Next()).ToList();
    }

    private async Task HandlePlayerAbandonmentAsync(Guid userId, Guid roomId, string roomCode)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var player = await db.RoomPlayers
            .Include(p => p.User)
            .Include(p => p.User.Stats)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.RoomId == roomId);

        if (player != null && !player.IsConnected && player.IsAlive && !player.IsBot)
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
