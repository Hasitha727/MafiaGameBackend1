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
    private readonly ILogger<GameLoopBackgroundService> _logger;

    public GameLoopBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<GameHub, IGameHubClient> hubContext,
        ILogger<GameLoopBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
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
                _logger.LogWarning("Game loop tick waiting for database initialization: {Message}", ex.Message);
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

        var expiredRooms = await db.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .Where(r => r.Status != GamePhase.Lobby && r.Status != GamePhase.Ended && r.PhaseEndTime <= now)
            .ToListAsync();

        foreach (var room in expiredRooms)
        {
            if (room.Status == GamePhase.Day)
            {
                await ResolveDayExecutionAndAdvanceAsync(db, room);
            }
            else if (room.Status == GamePhase.Night)
            {
                await ResolveNightPhaseAndAdvanceAsync(db, room);
            }
        }
    }

    private async Task ResolveDayExecutionAndAdvanceAsync(ApplicationDbContext db, GameRoom room)
    {
        room.Status = GamePhase.Night;
        room.PhaseEndTime = DateTime.UtcNow.AddSeconds(45);

        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(room.RoomCode).ReceiveGameAnnouncement(
            "🌅 MIDNIGHT EXECUTION",
            $"The town has spoken! Night falls on Day {room.CurrentDay}. Special roles, perform your actions!",
            "Night"
        );
    }

    private async Task ResolveNightPhaseAndAdvanceAsync(ApplicationDbContext db, GameRoom room)
    {
        room.CurrentDay += 1;
        room.Status = GamePhase.Day;
        room.PhaseEndTime = DateTime.UtcNow.AddSeconds(60);

        await db.SaveChangesAsync();

        await _hubContext.Clients.Group(room.RoomCode).ReceiveGameAnnouncement(
            "🌅 SUNRISE",
            $"Sunrise arrives on Day {room.CurrentDay}. Town discussion phase initiated!",
            "Day"
        );
    }
}
