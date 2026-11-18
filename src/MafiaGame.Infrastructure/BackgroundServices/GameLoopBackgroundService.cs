namespace MafiaGame.Infrastructure.BackgroundServices;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;
using MafiaGame.Domain.Enums;
using MafiaGame.Infrastructure.Hubs;
using MafiaGame.Infrastructure.Persistence;

public class GameLoopBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GameHub, IGameHubClient> _hubContext;
    private readonly IGameStateManager _gameState;
    private readonly ILogger<GameLoopBackgroundService> _logger;

    public GameLoopBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<GameHub, IGameHubClient> hubContext,
        IGameStateManager gameState,
        ILogger<GameLoopBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _gameState = gameState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessActiveRoomsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Game loop tick warning: {Message}", ex.Message);
            }
        }
    }

    private async Task ProcessActiveRoomsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await db.Database.CanConnectAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        var activeRooms = await db.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .ThenInclude(u => u.Stats)
            .Where(r => r.Status == GamePhase.Day || r.Status == GamePhase.Night)
            .ToListAsync();

        foreach (var room in activeRooms)
        {
            // 1. Run staggered AI Bot actions & chat
            await ProcessAiBotsForRoomAsync(db, room);

            // 2. Resolve phase if timer expired
            if (room.PhaseEndTime.HasValue && room.PhaseEndTime.Value <= now)
            {
                if (room.Status == GamePhase.Night)
                {
                    await ResolveNightPhaseAndAdvanceAsync(db, room);
                }
                else if (room.Status == GamePhase.Day)
                {
                    await ResolveDayExecutionAndAdvanceAsync(db, room);
                }
            }
        }
    }

    private async Task ProcessAiBotsForRoomAsync(ApplicationDbContext db, GameRoom room)
    {
        var aliveBots = room.Players.Where(p => p.IsBot && p.IsAlive).ToList();
        if (aliveBots.Count == 0) return;

        bool stateUpdated = false;

        if (room.Status == GamePhase.Night)
        {
            foreach (var bot in aliveBots)
            {
                if (bot.Role == RoleType.Civilian || bot.Role == RoleType.Jester || bot.Role == RoleType.Dog)
                    continue;

                if (_gameState.HasActedNight(room.RoomCode, bot.UserId))
                    continue;

                if (!_gameState.IsBotReadyToAct(room.RoomCode, bot.UserId))
                    continue;

                var targetId = _gameState.ChooseBotNightTarget(room.RoomCode, bot, room.Players);
                if (targetId.HasValue)
                {
                    _gameState.RecordNightAction(room.RoomCode, bot.UserId, targetId.Value);

                    if (bot.Role == RoleType.Police)
                    {
                        var targetPlayer = room.Players.FirstOrDefault(p => p.UserId == targetId.Value);
                        if (targetPlayer != null)
                        {
                            bool isMafia = targetPlayer.Role == RoleType.Mafia || targetPlayer.Role == RoleType.SerialKiller;
                            _gameState.RecordPoliceDiscovery(room.RoomCode, bot.UserId, targetPlayer.UserId, isMafia);
                        }
                    }
                }
            }

            // Smart Fast-Forward once every alive night-role player has acted
            if (_gameState.AreAllNightActionsIn(room.RoomCode, room.Players) &&
                room.PhaseEndTime.HasValue && room.PhaseEndTime.Value > DateTime.UtcNow.AddSeconds(3))
            {
                room.PhaseEndTime = DateTime.UtcNow.AddSeconds(3);
                await db.SaveChangesAsync();
                stateUpdated = true;
            }
        }
        else if (room.Status == GamePhase.Day)
        {
            foreach (var bot in aliveBots)
            {
                // Step A: Natural staggered Singlish/English discussion chat
                if (_gameState.IsBotReadyToChat(room.RoomCode, bot.UserId))
                {
                    _gameState.MarkBotChatted(room.RoomCode, bot.UserId);
                    var msg = _gameState.GenerateBotChatMessage(
                        room.RoomCode, bot, room.Players, room.ChatLanguage, room.CurrentDay);
                    await _hubContext.Clients.Group(room.RoomCode).ReceiveChatMessage(bot.User.Username, msg);
                }

                // Step B: Suspicion + Majority Consensus Day Vote
                if (!_gameState.HasVotedDay(room.RoomCode, bot.UserId) &&
                    _gameState.IsBotReadyToAct(room.RoomCode, bot.UserId))
                {
                    var chosenTargetId = _gameState.ChooseBotDayVote(room.RoomCode, bot, room.Players);
                    _gameState.RecordDayVote(room.RoomCode, bot.UserId, chosenTargetId, room.Players);

                    var targetPlayer = chosenTargetId.HasValue
                        ? room.Players.FirstOrDefault(p => p.UserId == chosenTargetId.Value)
                        : null;

                    await _hubContext.Clients.Group(room.RoomCode).ReceiveVoteTrail(
                        bot.UserId,
                        bot.User.Username,
                        chosenTargetId ?? Guid.Empty,
                        targetPlayer != null ? targetPlayer.User.Username : "⏭️ SKIP VOTE",
                        voteCount: 1
                    );
                    stateUpdated = true;
                }
            }

            // Smart Fast-Forward once every alive player (humans + bots) has voted
            if (_gameState.AreAllDayVotesIn(room.RoomCode, room.Players) &&
                room.PhaseEndTime.HasValue && room.PhaseEndTime.Value > DateTime.UtcNow.AddSeconds(3))
            {
                room.PhaseEndTime = DateTime.UtcNow.AddSeconds(3);
                await db.SaveChangesAsync();
                stateUpdated = true;
            }
        }

        if (stateUpdated)
        {
            await BroadcastRoomStateAsync(room);
        }
    }

    private async Task ResolveNightPhaseAndAdvanceAsync(ApplicationDbContext db, GameRoom room)
    {
        bool isSinglish = room.ChatLanguage.Equals("Singlish", StringComparison.OrdinalIgnoreCase);
        var nightResult = _gameState.ResolveNightActions(room.RoomCode, room.Players);

        var killedNames = new List<string>();
        foreach (var killedId in nightResult.KilledUserIds)
        {
            var victim = room.Players.FirstOrDefault(p => p.UserId == killedId && p.IsAlive);
            if (victim != null)
            {
                victim.IsAlive = false;
                killedNames.Add(victim.User.Username);
            }
        }

        _gameState.SetLastNightOutcome(room.RoomCode, killedNames.FirstOrDefault(), nightResult.SavedByDoctor);

        string morningMsg;
        if (killedNames.Count > 0)
        {
            var names = string.Join(", ", killedNames);
            morningMsg = isSinglish
                ? $"💀 Eliya weddi {names} wa Mafia marala! Kageda wade kiyala hoyala vote karamu!"
                : $"💀 Dawn breaks — {names} was eliminated during the night! Discuss and vote out the Mafia!";
        }
        else if (nightResult.SavedByDoctor)
        {
            morningMsg = isSinglish
                ? "🩺 Puduuma berillak! Doctor eeye raa Mafia attack eka nawaththuwa — kawuruth marune na!"
                : "🩺 Miracle at dawn! The Doctor saved the Mafia's target — nobody died last night!";
        }
        else
        {
            morningMsg = isSinglish
                ? "🌅 Udema hamoma hondin innawa — eeye raa kawuruth marune na!"
                : "🌅 A quiet sunrise — nobody was eliminated last night!";
        }

        await _hubContext.Clients.Group(room.RoomCode).ReceiveGameAnnouncement(
            $"☀️ DAY {room.CurrentDay} SUNRISE",
            morningMsg,
            "Day"
        );

        // Check Win Conditions before advancing to Day
        if (await CheckAndHandleWinConditionAsync(db, room))
        {
            return;
        }

        room.Status = GamePhase.Day;
        room.PhaseEndTime = DateTime.UtcNow.AddSeconds(55);
        await db.SaveChangesAsync();

        _gameState.StartNewPhase(room.RoomCode, room.Players.Where(p => p.IsBot), isNight: false);
        await BroadcastRoomStateAsync(room);
    }

    private async Task ResolveDayExecutionAndAdvanceAsync(ApplicationDbContext db, GameRoom room)
    {
        bool isSinglish = room.ChatLanguage.Equals("Singlish", StringComparison.OrdinalIgnoreCase);
        var voteResult = _gameState.ResolveDayVotes(room.RoomCode);

        if (voteResult.ExecutedUserId.HasValue)
        {
            var executed = room.Players.FirstOrDefault(p => p.UserId == voteResult.ExecutedUserId.Value && p.IsAlive);
            if (executed != null)
            {
                executed.IsAlive = false;
                var roleStr = executed.Role.ToString().ToUpper();
                var msg = isSinglish
                    ? $"⚖️ Wadi manaapen {executed.User.Username} wa eliyata damma! Eyaage role eka: [{roleStr}]!"
                    : $"⚖️ The town voted out {executed.User.Username} ({voteResult.TopVotes} votes)! Their role was [{roleStr}]!";

                await _hubContext.Clients.Group(room.RoomCode).ReceiveGameAnnouncement(
                    "⚖️ TOWN EXECUTION",
                    msg,
                    "Execution"
                );
            }
        }
        else
        {
            var reasonMsg = voteResult.IsTie
                ? (isSinglish ? "🤝 Votes samaana una! Ada kawuruth eliminate une na." : "🤝 The vote ended in a tie! Nobody was executed today.")
                : (isSinglish ? "⏭️ Town eka vote skip kala! Ada kawuruth eliminate une na." : "⏭️ The town chose to skip execution today.");

            await _hubContext.Clients.Group(room.RoomCode).ReceiveGameAnnouncement(
                "🤝 NO EXECUTION",
                reasonMsg,
                "Execution"
            );
        }

        // Check Win Conditions before advancing to Night
        if (await CheckAndHandleWinConditionAsync(db, room))
        {
            return;
        }

        room.CurrentDay += 1;
        room.Status = GamePhase.Night;
        room.PhaseEndTime = DateTime.UtcNow.AddSeconds(35);
        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(room.RoomCode).ReceiveGameAnnouncement(
            $"🌙 NIGHT {room.CurrentDay}",
            isSinglish
                ? $"Day {room.CurrentDay} raa una! Mafia, Doctor, Police roles thiyena aya action eka submit karanna!"
                : $"Night falls on Day {room.CurrentDay}. Special roles, lock in your night targets!",
            "Night"
        );

        _gameState.StartNewPhase(room.RoomCode, room.Players.Where(p => p.IsBot), isNight: true);
        await BroadcastRoomStateAsync(room);
    }

    private async Task<bool> CheckAndHandleWinConditionAsync(ApplicationDbContext db, GameRoom room)
    {
        var alive = room.Players.Where(p => p.IsAlive).ToList();
        int aliveMafia = alive.Count(p => p.Role == RoleType.Mafia);
        int aliveTown = alive.Count(p => p.Role != RoleType.Mafia && p.Role != RoleType.SerialKiller);
        int aliveSk = alive.Count(p => p.Role == RoleType.SerialKiller);

        string winner = string.Empty;
        if (aliveMafia == 0 && aliveSk == 0)
        {
            winner = "Town";
        }
        else if (aliveMafia > 0 && aliveMafia >= (aliveTown + aliveSk))
        {
            winner = "Mafia";
        }
        else if (aliveSk > 0 && alive.Count <= 2 && aliveMafia == 0)
        {
            winner = "SerialKiller";
        }

        if (string.IsNullOrEmpty(winner)) return false;

        room.Status = GamePhase.Ended;
        room.WinnerTeam = winner;
        room.PhaseEndTime = null;

        // Update Global Leaderboard stats for Human players ONLY (never pollute with bots)
        foreach (var player in room.Players.Where(p => !p.IsBot))
        {
            if (player.User.Stats != null)
            {
                player.User.Stats.GamesPlayed += 1;
                bool won = (winner == "Mafia" && player.Role == RoleType.Mafia)
                        || (winner == "SerialKiller" && player.Role == RoleType.SerialKiller)
                        || (winner == "Town" && player.Role != RoleType.Mafia && player.Role != RoleType.SerialKiller);

                if (won)
                {
                    player.User.Stats.GamesWon += 1;
                }
            }
        }

        await db.SaveChangesAsync();

        bool isSinglish = room.ChatLanguage.Equals("Singlish", StringComparison.OrdinalIgnoreCase);
        string title = winner == "Town" ? "🏆 TOWN WINS!" : winner == "Mafia" ? "🩸 MAFIA WINS!" : "🔪 SERIAL KILLER WINS!";
        string body = winner == "Town"
            ? (isSinglish ? "Jayawewa! Mafia okkoma allagaththa — Town eka dinna! 🎉" : "Victory! All Mafia have been eliminated — Town wins! 🎉")
            : (isSinglish ? "Mafia town eka allagaththa! Mafia dinna! 🩸" : "The Mafia have taken over the town! Mafia wins! 🩸");

        await _hubContext.Clients.Group(room.RoomCode).ReceiveGameAnnouncement(title, body, "Ended");
        await BroadcastRoomStateAsync(room);
        return true;
    }

    private async Task BroadcastRoomStateAsync(GameRoom room)
    {
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

        await _hubContext.Clients.Group(room.RoomCode).ReceiveRoomState(state);
    }
}
