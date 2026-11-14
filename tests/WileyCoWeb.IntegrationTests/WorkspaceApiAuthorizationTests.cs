using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WileyCoWeb.Api;
using WileyCoWeb.Api.Configuration;
using WileyWidget.Data;
using Xunit;

namespace WileyCoWeb.IntegrationTests;

[Trait("Category", "HighRisk")]
public sealed class WorkspaceApiAuthorizationTests
{
    [Theory]
    [InlineData(new string[] { }, true)]
    [InlineData(new[] { "Clerk" }, true)]
    [InlineData(new[] { "admin" }, true)]
    [InlineData(new[] { "Driver", "Clerk" }, true)]
    [InlineData(new[] { "Driver" }, false)]
    [InlineData(new[] { "[\"Driver\"]" }, false)]
    public void HasApiAccess_RequiresClerkOrAdminWhenGroupsArePresent(string[] groups, bool expected)
    {
        var claims = groups.Select(group => new Claim("cognito:groups", group)).ToArray();
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        Assert.Equal(expected, StaffApiAccessHandler.HasApiAccess(new ClaimsPrincipal(identity)));
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WhenJwtEnabled_AndNoToken()
    {
        await using var factory = new GroupProbeApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/ai/health")).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns403_ForDriverGroup()
    {
        await using var factory = new GroupProbeApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "driver");

        var response = await client.GetAsync("/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_AllowsClerkGroup()
    {
        await using var factory = new GroupProbeApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await EnsureDatabaseAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "clerk");

        var response = await client.GetAsync("/api/workspace/snapshot?enterprise=Water%20Utility&fiscalYear=2026");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task EnsureDatabaseAsync(GroupProbeApiFactory factory)
    {
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    private sealed class GroupProbeApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTest");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Jwt:Enabled"] = "true",
                    ["Authentication:Jwt:Authority"] = "https://example.invalid/",
                    ["Authentication:Jwt:Audience"] = "test-audience",
                    ["Authentication:Jwt:RequireHttpsMetadata"] = "false",
                    ["XAI_API_KEY"] = "test-xai-key-integration-7f3a9b2c1d4e5f6a7b8c9d0e",
                    ["XAI:ApiKey"] = "test-xai-key-integration-7f3a9b2c1d4e5f6a7b8c9d0e"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextFactory<AppDbContext>>();

                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase($"GroupProbe-{Guid.NewGuid():N}")
                    .Options;
                services.AddSingleton(options);
                services.AddScoped(_ => new AppDbContext(options));
                services.AddSingleton<IDbContextFactory<AppDbContext>>(_ => new LocalAppDbContextFactory(options));

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, GroupProbeAuthHandler>(GroupProbeAuthHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(auth =>
                {
                    auth.DefaultAuthenticateScheme = GroupProbeAuthHandler.SchemeName;
                    auth.DefaultChallengeScheme = GroupProbeAuthHandler.SchemeName;
                    auth.DefaultForbidScheme = GroupProbeAuthHandler.SchemeName;
                });

                // The test host applies Authentication:Jwt:Enabled after the app registers policies.
                // Endpoint metadata still names WorkspaceRead, so register the production staff rules here.
                services.AddSingleton<IAuthorizationHandler, StaffApiAccessHandler>();
                services.AddAuthorizationBuilder()
                    .AddPolicy(JwtAuthenticationExtensions.WorkspaceReadPolicy, policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.AddRequirements(new StaffApiAccessRequirement());
                    })
                    .AddPolicy(JwtAuthenticationExtensions.WorkspaceMutatingPolicy, policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.AddRequirements(new StaffApiAccessRequirement());
                    });
            });
        }
    }

    private sealed class LocalAppDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public LocalAppDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext() => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class GroupProbeAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "GroupProbe";

        public GroupProbeAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var header = headerValues.ToString();
            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("A bearer token is required."));
            }

            var token = header[prefix.Length..].Trim();
            var group = token switch
            {
                "driver" => "Driver",
                "clerk" => "Clerk",
                "admin" => "admin",
                _ => null
            };

            if (group is null)
            {
                return Task.FromResult(AuthenticateResult.Fail("Unrecognized probe token."));
            }

            var identity = new ClaimsIdentity(
                [new Claim("cognito:groups", group), new Claim(ClaimTypes.Name, group)],
                authenticationType: SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
