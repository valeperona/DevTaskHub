using DevTaskHub.Api.Controllers;
using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DevTaskHub.Tests;

public class AuthControllerTests
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "supersecret-devtaskhub-key-change-me",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience"
        }).Build();

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailMissing()
    {
        using var context = TestUtils.CreateContext();
        var controller = new AuthController(context, new PasswordHasher<User>(), BuildConfig());

        var result = await controller.Register(new AuthController.RegisterRequest("", "123456"), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, problem.StatusCode ?? problem.StatusCode.GetValueOrDefault(400));
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenUserAlreadyExists()
    {
        var existing = TestUtils.CreateUser("taken@test.com");
        using var context = TestUtils.CreateContext();
        context.Users.Add(existing);
        context.SaveChanges();

        var controller = new AuthController(context, new PasswordHasher<User>(), BuildConfig());
        var result = await controller.Register(new AuthController.RegisterRequest(existing.Email, "123456"), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Register_CreatesUser_AndReturnsToken()
    {
        using var context = TestUtils.CreateContext();
        var passwordHasher = new PasswordHasher<User>();
        var controller = new AuthController(context, passwordHasher, BuildConfig());

        var result = await controller.Register(new AuthController.RegisterRequest("new@test.com", "abcdef"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthController.AuthResponse>(ok.Value);
        Assert.Equal("new@test.com", response.Email);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.True(await context.Users.AnyAsync(u => u.Email == "new@test.com"));
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForWrongPassword()
    {
        var passwordHasher = new PasswordHasher<User>();
        var user = TestUtils.CreateUser("auth@test.com");
        user.PasswordHash = passwordHasher.HashPassword(user, "correct");
        using var context = TestUtils.CreateContext();
        context.Users.Add(user);
        context.SaveChanges();

        var controller = new AuthController(context, passwordHasher, BuildConfig());
        var result = await controller.Login(new AuthController.LoginRequest(user.Email, "wrong"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_ReturnsToken_ForValidCredentials()
    {
        var passwordHasher = new PasswordHasher<User>();
        var user = TestUtils.CreateUser("ok@test.com");
        user.PasswordHash = passwordHasher.HashPassword(user, "secret123");
        using var context = TestUtils.CreateContext();
        context.Users.Add(user);
        context.SaveChanges();

        var controller = new AuthController(context, passwordHasher, BuildConfig());
        var result = await controller.Login(new AuthController.LoginRequest(user.Email, "secret123"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthController.AuthResponse>(ok.Value);
        Assert.Equal(user.Id, response.UserId);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }
}
