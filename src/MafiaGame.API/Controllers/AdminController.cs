namespace MafiaGame.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MafiaGame.Application.Interfaces;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetSystemOverview()
    {
        var users = await _unitOfWork.Users.GetAllWithStatsAsync();
        var rooms = await _unitOfWork.Rooms.GetAllWithPlayersAsync();

        return Ok(new
        {
            totalUsers = users.Count,
            totalRooms = rooms.Count,
            activeRooms = rooms.Count(r => r.Status != Domain.Enums.GamePhase.Ended),
            users = users.Select(u => new
            {
                id = u.Id,
                username = u.Username,
                email = u.Email,
                role = u.Role,
                createdAt = u.CreatedAt
            }),
            rooms = rooms.Select(r => new
            {
                roomCode = r.RoomCode,
                status = r.Status.ToString(),
                playerCount = r.Players.Count,
                createdAt = r.CreatedAt
            })
        });
    }

    [HttpDelete("rooms/{code}")]
    public async Task<IActionResult> DeleteRoom(string code)
    {
        var room = await _unitOfWork.Rooms.GetByCodeWithPlayersAsync(code);
        if (room == null) return NotFound(new { error = "Room not found." });

        _unitOfWork.Rooms.Remove(room);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new { message = $"Room {code.ToUpper()} closed by Admin." });
    }
}
