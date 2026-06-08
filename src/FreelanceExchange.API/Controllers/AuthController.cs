using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Models;
using FreelanceExchange.API.DTOs;

namespace FreelanceExchange.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private string HashPassword(string password) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    private bool VerifyPassword(string password, string hash) =>
        HashPassword(password) == hash;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Пользователь с таким email уже существует");

        // ИСПРАВЛЕНО: По умолчанию ставим роль Фрилансера (Id = 2)
        int roleId = 2; 

        // Проверяем выбор пользователя на форме (учитываем и английский, и русский текст)
        if (dto.Role == "Customer" || dto.Role == "Заказчик")
        {
            roleId = 3; // Устанавливаем роль Заказчика (Id = 3)
        }

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = HashPassword(dto.Password),
            FullName = dto.FullName,
            Phone = dto.Phone,
            RegistrationDate = DateTime.UtcNow,
            IsBlocked = false,
            RoleId = roleId
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok("Регистрация успешна");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null || !VerifyPassword(dto.Password, user.PasswordHash))
            return Unauthorized("Неверный email или пароль");

        if (user.IsBlocked)
            return Unauthorized("Ваш аккаунт заблокирован");

        var token = GenerateJwtToken(user);
        return Ok(new AuthResponseDto
        {
            Token = token,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role?.Name ?? "Unknown"
        });
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role?.Name ?? "User")
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiryMinutes"]!)),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
