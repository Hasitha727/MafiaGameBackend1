namespace MafiaGame.API.Controllers;

using Microsoft.AspNetCore.Mvc;
using MafiaGame.Application.DTOs;
using MafiaGame.Application.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public LeaderboardController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 20)
    {
        var statsList = await _unitOfWork.Leaderboard.GetTopWithUsersAsync(top);

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
