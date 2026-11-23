using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using UserService.Data;
using UserService.Messaging;
using UserService.Services;

namespace UserService.SecurityTests;

[TestClass]
public class SecurityTests
{
    private const string Issuer = "http://localhost:8081/realms/Library";
    private const string Audience = "library-userservice";

    private readonly SymmetricSecurityKey _key =
        new(RandomNumberGenerator.GetBytes(32));

    private WebApplicationFactory<KeycloakUserClient> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _factory = new WebApplicationFactory<KeycloakUserClient>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureTestServices(services =>
                {
                    // Replace MySQL with an in-memory database.
                    services.RemoveAll<UserDbContext>();
                    services.RemoveAll<DbContextOptions<UserDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<UserDbContext>>();

                    services.AddDbContext<UserDbContext>(options =>
                        options.UseInMemoryDatabase("SecurityTestDb"));

                    // Disable the background worker.
                    var worker = services.FirstOrDefault(s =>
                        s.ServiceType == typeof(IHostedService) &&
                        s.ImplementationType == typeof(UserOutboxWorker));

                    if (worker != null)
                        services.Remove(worker);

                    // Configure JWT validation for testing.
                    services.Configure<JwtBearerOptions>("Bearer", options =>
                    {
                        options.RequireHttpsMetadata = false;

                        var configuration = new OpenIdConnectConfiguration
                        {
                            Issuer = Issuer
                        };

                        configuration.SigningKeys.Add(_key);

                        options.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(
                                configuration);
                    });
                });
            });

        _client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }

    // Generate a JWT with the specified role and expiration.
    private string CreateToken(string role, bool expired = false)
    {
        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("realm_access",
                JsonSerializer.Serialize(new { roles = new[] { role } }),
                JsonClaimValueTypes.Json)
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expired ? -10 : 5),
            signingCredentials: new SigningCredentials(
                _key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Send a request with or without a JWT.
    private async Task<HttpStatusCode> GetUsersAsync(string? token = null)
    {
        _client.DefaultRequestHeaders.Authorization = token == null
            ? null
            : new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.GetAsync("/api/users");

        return response.StatusCode;
    }

    // Authentication

    [TestMethod]
    public async Task WithoutToken_ShouldReturnUnauthorized()
    {
        // Act
        var status = await GetUsersAsync();

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, status);
    }

    [TestMethod]
    public async Task InvalidToken_ShouldReturnUnauthorized()
    {
        // Act
        var status = await GetUsersAsync("invalid-token");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, status);
    }

    [TestMethod]
    public async Task ExpiredToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var token = CreateToken("user", expired: true);

        // Act
        var status = await GetUsersAsync(token);

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, status);
    }

    // Authorization

    [TestMethod]
    public async Task RegularUser_ShouldReturnForbidden()
    {
        // Arrange
        var token = CreateToken("user");

        // Act
        var status = await GetUsersAsync(token);

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, status);
    }

    [TestMethod]
    public async Task Admin_ShouldReturnOk()
    {
        // Arrange
        var token = CreateToken("admin");

        // Act
        var status = await GetUsersAsync(token);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, status);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}