namespace MafiaGame.API.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MafiaGame.Application.DTOs;
using MafiaGame.Infrastructure.Persistence;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public LeaderboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 20)
    {
        var statsList = await _db.PlayerGameStats
            .Include(s => s.User)
            .Take(top)
            .ToListAsync();

        var leaderboard = statsList
            .Select(s => new LeaderboardDto(
                s.UserId,
                s.User.Username,
                s.GamesPlayed,
                s.GamesWon,
                s.WinRate,
                s.SuccessfulMafiaVotes,
                s.BadPoints,
                s.ReliabilityScore
            ))
            .OrderByDescending(s => s.WinRate)
            .ThenByDescending(s => s.ReliabilityScore)
            .ToList();

        return Ok(leaderboard);
    }
}
