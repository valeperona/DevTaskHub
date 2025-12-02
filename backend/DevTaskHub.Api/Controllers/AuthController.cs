using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DevTaskHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DevTaskHubContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthController(DevTaskHubContext context, IPasswordHasher<User> passwordHasher, IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(nameof(request.Email), "El email es obligatorio");
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            ModelState.AddModelError(nameof(request.Password), "La contraseña debe tener al menos 6 caracteres");
            return ValidationProblem(ModelState);
        }

        var exists = await _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "El usuario ya existe" });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var token = GenerateToken(user);
        return Ok(new AuthResponse(user.Id, user.Email, token));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email y contraseña son obligatorios" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { message = "Credenciales inválidas" });
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Credenciales inválidas" });
        }

        var token = GenerateToken(user);
        return Ok(new AuthResponse(user.Id, user.Email, token));
    }

    private string GenerateToken(User user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var secretKey = jwtSection["SecretKey"] ?? "supersecret-devtaskhub-key-change-me";
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public record RegisterRequest(string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record AuthResponse(Guid UserId, string Email, string Token);
}
