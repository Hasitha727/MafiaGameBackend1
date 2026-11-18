namespace MafiaGame.API.Controllers;

using Microsoft.AspNetCore.Mvc;
using MafiaGame.Application.DTOs;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;

    public AuthController(IUnitOfWork unitOfWork, IJwtService jwtService)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new { error = "Username, Email, and Password are required." });
        }

        if (await _unitOfWork.Users.ExistsByUsernameOrEmailAsync(dto.Username, dto.Email))
        {
            return BadRequest(new { error = "Username or Email is already taken." });
        }

        // First registered user becomes Admin for RBAC management; subsequent users default to Player
        var isFirstUser = await _unitOfWork.Users.CountAsync() == 0;
        var assignedRole = isFirstUser ? "Admin" : "Player";

        var user = new User
        {
            Username = dto.Username.Trim(),
            Email = dto.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = assignedRole
        };

        var stats = new PlayerGameStats
        {
            UserId = user.Id,
            User = user
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.Leaderboard.AddAsync(stats);
        await _unitOfWork.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponseDto(user.Id, user.Username, user.Email, user.Role, token));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _unitOfWork.Users.GetByUsernameOrEmailAsync(dto.UsernameOrEmail);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponseDto(user.Id, user.Username, user.Email, user.Role, token));
    }
}
