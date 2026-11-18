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
}
