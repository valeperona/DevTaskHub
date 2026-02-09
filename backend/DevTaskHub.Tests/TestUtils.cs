using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace DevTaskHub.Tests;

internal static class TestUtils
{
    public static DevTaskHubContext CreateContext([CallerMemberName] string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<DevTaskHubContext>()
            .UseInMemoryDatabase($"DevTaskHubTests_{dbName}_{Guid.NewGuid()}")
            .Options;
        return new DevTaskHubContext(options);
    }

    public static T WithUser<T>(this T controller, Guid userId) where T : ControllerBase
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    public static User CreateUser(string email, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
}
