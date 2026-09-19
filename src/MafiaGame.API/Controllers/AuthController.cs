namespace MafiaGame.API.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MafiaGame.Application.DTOs;
using MafiaGame.Application.Interfaces;
using MafiaGame.Domain.Entities;
using MafiaGame.Infrastructure.Persistence;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IJwtService _jwtService;

    public AuthController(ApplicationDbContext db, IJwtService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new { error = "Username, Email, and Password are required." });
        }

        if (await _db.Users.AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email))
        {
            return BadRequest(new { error = "Username or Email is already taken." });
        }

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        var stats = new PlayerGameStats
        {
            UserId = user.Id,
            User = user
        };

        _db.Users.Add(user);
        _db.PlayerGameStats.Add(stats);
        await _db.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponseDto(user.Id, user.Username, user.Email, token));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.UsernameOrEmail || u.Email == dto.UsernameOrEmail);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponseDto(user.Id, user.Username, user.Email, token));
    }
}
