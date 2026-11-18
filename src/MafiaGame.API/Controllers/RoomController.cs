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
    private readonly IGameStateManager _gameState;

    public RoomController(IUnitOfWork unitOfWork, IGameStateManager gameState)
    {
        _unitOfWork = unitOfWork;
        _gameState = gameState;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
    {
        var userId = GetUserId();
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null) return Unauthorized(new { error = "User session not found. Please log in again." });

        var roomCode = GenerateUniqueRoomCode();
        var language = dto.ChatLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) ? "English" : "Singlish";

        var room = new GameRoom
        {
            RoomCode = roomCode,
            HostUserId = userId,
            Status = GamePhase.Lobby,
            ChatLanguage = language,
            Modifiers = (RoomModifier)dto.Modifiers
        };

        var hostPlayer = new RoomPlayer
        {
            RoomId = room.Id,
            UserId = userId,
            IsHost = true,
            IsBot = false,
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
            chat_language = room.ChatLanguage,
            modifiers = room.Modifiers.ToString()
        });
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetRoomDetails(string code)
    {
        var room = await _unitOfWork.Rooms.GetByCodeWithPlayersAsync(code);
        if (room == null) return NotFound(new { error = "Room not found." });

        var voteCounts = _gameState.GetDayVoteCounts(room.RoomCode)
            .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        var skipVotes = _gameState.GetSkipVoteCount(room.RoomCode);
        bool revealRoles = room.Status == GamePhase.Ended;

        return Ok(new
        {
            room_id = room.Id,
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
                is_host = p.IsHost,
                is_bot = p.IsBot,
                bot_personality = p.BotPersonality,
                is_alive = p.IsAlive,
                is_connected = p.IsConnected,
                role = revealRoles ? p.Role.ToString() : null
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
