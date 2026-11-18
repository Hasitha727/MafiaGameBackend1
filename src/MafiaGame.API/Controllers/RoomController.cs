namespace MafiaGame.API.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MafiaGame.Application.DTOs;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;
using MafiaGame.Domain.Enums;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RoomController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public RoomController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
    {
        var userId = GetUserId();
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null) return Unauthorized(new { error = "User session not found. Please log in again." });

        var roomCode = GenerateUniqueRoomCode();

        var room = new GameRoom
        {
            RoomCode = roomCode,
            HostUserId = userId,
            Status = GamePhase.Lobby,
            Modifiers = (RoomModifier)dto.Modifiers
        };

        var hostPlayer = new RoomPlayer
        {
            RoomId = room.Id,
            UserId = userId,
            IsHost = true,
            IsAlive = true,
            IsConnected = true
        };

        await _unitOfWork.Rooms.AddAsync(room);
        await _unitOfWork.Rooms.AddPlayerAsync(hostPlayer);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new
        {
            room_id = room.Id,
            room_code = room.RoomCode,
            host_username = user.Username,
            status = room.Status.ToString(),
            modifiers = room.Modifiers.ToString()
        });
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetRoomDetails(string code)
    {
        var room = await _unitOfWork.Rooms.GetByCodeWithPlayersAsync(code);
        if (room == null) return NotFound(new { error = "Room not found." });

        return Ok(new
        {
            room_id = room.Id,
            room_code = room.RoomCode,
            status = room.Status.ToString(),
            current_day = room.CurrentDay,
            phase_end_time = room.PhaseEndTime,
            winner_team = room.WinnerTeam,
            modifiers = room.Modifiers.ToString(),
            players = room.Players.Select(p => new
            {
                user_id = p.UserId,
                username = p.User.Username,
                is_host = p.IsHost,
                is_alive = p.IsAlive,
                is_connected = p.IsConnected
            })
        });
    }

    private string GenerateUniqueRoomCode()
    {
        const string chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}
