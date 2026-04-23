using Application.Common.Interfaces;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tests.E2e.Helpers;

/// <summary>
/// Shared WebApplicationFactory for E2E tests.
/// Each instance gets its own in-memory database, so test classes are fully isolated.
///
/// Strategy: AddInfrastructure skips Npgsql registration when Environment == "Testing",
/// so ConfigureServices here is the ONLY place that registers ApplicationDbContext.
/// </summary>
public class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"e2e_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" causes AddInfrastructure to skip Npgsql — see Infrastructure/DependencyInjection.cs
        builder.UseEnvironment("Testing");
        // JWT settings come from appsettings.json as-is.
        // Do NOT override Jwt:Key here — Program.cs reads it at startup for the validation
        // middleware, before any ConfigureAppConfiguration override could take effect.
        // JwtService reads at runtime from the same IConfiguration, so they must stay aligned.

        builder.ConfigureServices(services =>
        {
            // ── InMemory database ─────────────────────────────────────────
            // AddInfrastructure skipped Npgsql, so this is the first (and only) registration.
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName));

            services.AddScoped<IApplicationDbContext>(sp =>
                sp.GetRequiredService<ApplicationDbContext>());

            // ── No-op Telegram so tests never make real HTTP calls ────────
            services.RemoveAll<ITelegramService>();
            services.AddScoped<ITelegramService, NoOpTelegramService>();
        });
    }
}

/// <summary>No-op implementation that satisfies ITelegramService in tests.</summary>
internal sealed class NoOpTelegramService : ITelegramService
{
    public Task SendMessageAsync(string telegramId, string message,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> SendOtpAsync(string telegramId, string otp,
        CancellationToken cancellationToken = default) => Task.FromResult(true);
}
