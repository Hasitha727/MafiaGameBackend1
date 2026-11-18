namespace MafiaGame.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Xunit;
using MafiaGame.Domain.Entities;
using MafiaGame.Domain.Enums;
using MafiaGame.Infrastructure.Services;

public class GameLogicTests
{
    [Fact]
    public void PlayerGameStats_Calculates_WinRate_Correctly()
    {
        // Arrange
        var stats = new PlayerGameStats
        {
            GamesPlayed = 10,
            GamesWon = 7,
            BadPoints = 1
        };

        // Act
        var winRate = stats.WinRate;
        var reliability = stats.ReliabilityScore;

        // Assert
        Assert.Equal(70.0, winRate);
        Assert.Equal(98.0, reliability);
    }

    [Fact]
    public void PlayerGameStats_Zero_GamesPlayed_Returns_Default_Metrics()
    {
        // Arrange
        var stats = new PlayerGameStats
        {
            GamesPlayed = 0,
            GamesWon = 0,
            BadPoints = 0
        };

        // Assert
        Assert.Equal(0.0, stats.WinRate);
        Assert.Equal(100.0, stats.ReliabilityScore);
    }

    [Fact]
    public async Task GracePeriodManager_Cancels_Timer_On_Reconnection()
    {
        // Arrange
        var manager = new GracePeriodManager();
        var userId = Guid.NewGuid();
        bool isExpiredExecuted = false;

        // Act
        manager.StartGracePeriod(userId, TimeSpan.FromMilliseconds(500), async () =>
        {
            isExpiredExecuted = true;
            await Task.CompletedTask;
        });

        // Simulate reconnection within 100ms
        await Task.Delay(100);
        manager.CancelGracePeriod(userId);

        // Wait past original timer duration
        await Task.Delay(600);

        // Assert
        Assert.False(isExpiredExecuted);
    }

    [Fact]
    public void JwtService_Includes_RoleClaim_For_RBAC()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "UNIT_TEST_SECRET_KEY_FOR_RBAC_VERIFICATION_123456789",
                ["Jwt:Issuer"] = "MafiaGameBackend"
            })
            .Build();
        var jwtService = new JwtService(config);
        var user = new User { Username = "AdminUser", Email = "admin@test.com", Role = "Admin" };

        // Act
        var tokenStr = jwtService.GenerateToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenStr);
        var roleClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);

        // Assert
        Assert.NotNull(roleClaim);
        Assert.Equal("Admin", roleClaim.Value);
    }

    [Fact]
    public void GameStateManager_DoctorSave_Blocks_MafiaKill()
    {
        // Arrange
        var manager = new GameStateManager();
        var mafia = new RoomPlayer { UserId = Guid.NewGuid(), Role = RoleType.Mafia, IsAlive = true, User = new User { Username = "MafiaBot" } };
        var doctor = new RoomPlayer { UserId = Guid.NewGuid(), Role = RoleType.Doctor, IsAlive = true, User = new User { Username = "DocBot" } };
        var civilian = new RoomPlayer { UserId = Guid.NewGuid(), Role = RoleType.Civilian, IsAlive = true, User = new User { Username = "CivPlayer" } };
        var players = new[] { mafia, doctor, civilian };

        // Act
        manager.RecordNightAction("TEST01", mafia.UserId, civilian.UserId);
        manager.RecordNightAction("TEST01", doctor.UserId, civilian.UserId);
        var result = manager.ResolveNightActions("TEST01", players);

        // Assert
        Assert.True(result.SavedByDoctor);
        Assert.Empty(result.KilledUserIds);
    }

    [Fact]
    public void GameStateManager_Resolves_DayMajority_And_Detects_Tie()
    {
        // Arrange
        var manager = new GameStateManager();
        var p1 = new RoomPlayer { UserId = Guid.NewGuid(), IsAlive = true, User = new User { Username = "P1" } };
        var p2 = new RoomPlayer { UserId = Guid.NewGuid(), IsAlive = true, User = new User { Username = "P2" } };
        var p3 = new RoomPlayer { UserId = Guid.NewGuid(), IsAlive = true, User = new User { Username = "P3" } };
        var players = new[] { p1, p2, p3 };

        // Majority vote on P3
        manager.RecordDayVote("TEST02", p1.UserId, p3.UserId, players);
        manager.RecordDayVote("TEST02", p2.UserId, p3.UserId, players);
        var majorityRes = manager.ResolveDayVotes("TEST02");

        Assert.Equal(p3.UserId, majorityRes.ExecutedUserId);
        Assert.Equal(2, majorityRes.TopVotes);
        Assert.False(majorityRes.IsTie);

        // Tie vote between P2 and P3
        manager.ResetRoom("TEST03");
        manager.RecordDayVote("TEST03", p1.UserId, p2.UserId, players);
        manager.RecordDayVote("TEST03", p2.UserId, p3.UserId, players);
        var tieRes = manager.ResolveDayVotes("TEST03");

        Assert.Null(tieRes.ExecutedUserId);
        Assert.True(tieRes.IsTie);
    }
}
