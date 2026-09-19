namespace MafiaGame.Tests;

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
}
