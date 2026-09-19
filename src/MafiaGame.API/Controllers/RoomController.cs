namespace MafiaGame.API.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MafiaGame.Application.DTOs;
using MafiaGame.Domain.Entities;
using MafiaGame.Domain.Enums;
using MafiaGame.Infrastructure.Persistence;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RoomController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public RoomController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

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

        _db.GameRooms.Add(room);
        _db.RoomPlayers.Add(hostPlayer);
        await _db.SaveChangesAsync();

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
        var room = await _db.GameRooms
            .Include(r => r.Players)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.RoomCode == code.ToUpper());

        if (room == null) return NotFound(new { error = "Room not found." });

        return Ok(new
        {
            room_id = room.Id,
            room_code = room.RoomCode,
            status = room.Status.ToString(),
            current_day = room.CurrentDay,
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
