
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text.Json;
using UserService.Data;
using UserService.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<UserService.Services.UserService>();
builder.Services.AddSingleton<RabbitMqPublisher>();

// Database connection
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 4, 0))
    )
);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// Configure OpenAPI to support OAuth 2.0 login through Keycloak.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, context, cancellationToken) =>
        {
            // Get the public Keycloak realm URL.
            var authority = builder.Configuration[
                "Keycloak:Authority"
            ] ?? throw new InvalidOperationException(
                "Keycloak Authority is missing.");

            // Register Keycloak as an OAuth 2.0 security scheme.
            document.Components ??= new OpenApiComponents();

            document.Components.SecuritySchemes =
                new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Keycloak"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,

                        Flows = new OpenApiOAuthFlows
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                // Keycloak login endpoint.
                                AuthorizationUrl = new Uri(
                                    $"{authority}/protocol/openid-connect/auth"
                                ),

                                // Keycloak token endpoint.
                                TokenUrl = new Uri(
                                    $"{authority}/protocol/openid-connect/token"
                                ),

                                // OAuth/OpenID Connect scopes.
                                Scopes = new Dictionary<string, string>
                                {
                                    ["openid"] = "OpenID Connect",
                                    ["profile"] = "User profile",
                                    ["email"] = "User email"
                                }
                            }
                        }
                    }
                };

            // Tell Swagger which security scheme to use.
            foreach (var path in document.Paths.Values)
            {
                foreach (var operation in path.Operations.Values)
                {
                    operation.Security ??=
                        new List<OpenApiSecurityRequirement>();

                    operation.Security.Add(
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference(
                                "Keycloak", document)] =
                                new List<string>()
                        });
                }
            }

            return Task.CompletedTask;
        });
});

// Keycloak JWT Authentication
// Keycloak handles OAuth 2.0 / OpenID Connect login.
// The UserService validates JWT access tokens issued by Keycloak.
var keycloakAuthority = builder.Configuration
    ["Keycloak:Authority"]
    ?? throw new InvalidOperationException(
        "Keycloak Authority is missing.");

var keycloakAudience = builder.Configuration
    ["Keycloak:Audience"]
    ?? throw new InvalidOperationException(
        "Keycloak Audience is missing.");

// Internal Keycloak address for Docker communication.
var keycloakInternalUrl = builder.Configuration
    ["Keycloak:InternalBaseUrl"];

// Configure JWT Bearer Authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Public Keycloak realm URL.
        // This must match the issuer inside the JWT.
        options.Authority = keycloakAuthority;

        // The JWT must be intended for the UserService API.
        options.Audience = keycloakAudience;

        // Allow HTTP metadata only in development.
        options.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment();

        // Keep original Keycloak JWT claim names.
        options.MapInboundClaims = false;

        // Validate JWT signature, issuer, audience and expiration.
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                NameClaimType = "preferred_username",

                // ASP.NET Core uses this claim type for role authorization.
                RoleClaimType = ClaimTypes.Role
            };

        // Extract Keycloak realm roles from the validated JWT.
        // Keycloak stores realm roles inside "realm_access.roles".
        // Convert them into standard ASP.NET Core role claims.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var identity = context.Principal?.Identity
                    as ClaimsIdentity;

                var realmAccess = context.Principal?
                    .FindFirst("realm_access")?.Value;

                if (identity != null &&
                    !string.IsNullOrEmpty(realmAccess))
                {
                    using var json = JsonDocument.Parse(realmAccess);

                    if (json.RootElement.TryGetProperty(
                        "roles", out var roles) &&
                        roles.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var role in roles.EnumerateArray())
                        {
                            if (role.ValueKind != JsonValueKind.String)
                                continue;

                            var roleName = role.GetString();

                            if (!string.IsNullOrEmpty(roleName))
                            {
                                // Add each Keycloak role as an ASP.NET Core role claim.
                                identity.AddClaim(
                                    new Claim(ClaimTypes.Role, roleName)
                                );
                            }
                        }
                    }
                }

                return Task.CompletedTask;
            }
        };

        // Use Docker's internal network for metadata and signing keys.
        // The public issuer URL is not changed.
        if (builder.Environment.IsDevelopment() &&
            !string.IsNullOrWhiteSpace(keycloakInternalUrl))
        {
            var publicUri = new Uri(keycloakAuthority);
            var internalUri = new Uri(keycloakInternalUrl);

            options.BackchannelHttpHandler =
                new KeycloakDockerHandler(
                    publicUri,
                    internalUri
                );
        }
    });

// Enable authorization for protected API endpoints.
builder.Services.AddAuthorization(options =>
{
    // Only users with the "admin" role can access admin endpoints.
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));

    // Only users with the "user" or "admin" role can access library endpoints.
    options.AddPolicy("LibraryUser", policy =>
        policy.RequireRole("user", "admin"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Enable Swagger UI
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "UserService API");

        // Configure Swagger to use the Keycloak OAuth client.
        options.OAuthClientId("library-userservice");

        // Display the application name in the OAuth login dialog.
        options.OAuthAppName("Library UserService");

        // Use Authorization Code Flow with PKCE (S256).
        // This allows secure login without a client secret.
        options.OAuthUsePkce();

        // Request OpenID Connect scopes.
        options.OAuthScopes("openid", "profile", "email");
    });
}

app.UseHttpsRedirection();

// Authenticate JWT tokens before checking authorization rules.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Development-only HTTP handler for Docker networking.
// Rewrites Keycloak metadata and JWKS requests to the
// internal Docker address without changing the JWT issuer.
internal sealed class KeycloakDockerHandler : DelegatingHandler
{
    private readonly Uri _publicUri;
    private readonly Uri _internalUri;

    public KeycloakDockerHandler(
        Uri publicUri,
        Uri internalUri)
        : base(new HttpClientHandler())
    {
        _publicUri = publicUri;
        _internalUri = internalUri;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Check if the request is targeting the public Keycloak URL.
        // Only Keycloak requests should be redirected to the Docker network.
        if (request.RequestUri is not null &&
            request.RequestUri.Scheme == _publicUri.Scheme &&
            request.RequestUri.Host == _publicUri.Host &&
            request.RequestUri.Port == _publicUri.Port &&
            request.RequestUri.AbsolutePath.StartsWith(
                _publicUri.AbsolutePath.TrimEnd('/') + "/",
                StringComparison.Ordinal))
        {
            // Replace the public Keycloak host and port
            // with the internal Docker address.
            // Example: localhost:8081 -> library-keycloak:8080
            var uri = new UriBuilder(request.RequestUri)
            {
                Scheme = _internalUri.Scheme,
                Host = _internalUri.Host,
                Port = _internalUri.Port
            };

            // Update the request URL without changing its path.
            request.RequestUri = uri.Uri;
        }

        // Send the HTTP request to the destination.
        return base.SendAsync(request, cancellationToken);
    }
}
