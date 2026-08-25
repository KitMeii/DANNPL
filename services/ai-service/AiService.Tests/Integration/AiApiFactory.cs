using AiService.Api.AiProviders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiService.Tests.Integration;

public sealed class AiApiFactory : WebApplicationFactory<Program>
{
    // Swapped in place of the real AiProviderFactory (which would try to read GROQ_API_KEY and hit
    // the real Groq API) — AiProviderRouter itself is NOT swapped, so these tests exercise the real
    // retry/failover logic too (with exactly 1 provider, that's just "call it once" unless a test
    // injects a failure via Provider.NextException/AlwaysThrow).
    public readonly FakeAiProvider Provider = new("groq");

    // Program.cs reads Jwt:SigningKey off the default config providers (env vars,
    // appsettings.json) before ConfigureWebHost below ever runs — on a checkout without a local
    // appsettings.Development.json (gitignored; every CI run included), it throws before this
    // factory gets a chance to configure anything. Seed it as a process env var, only if not
    // already set, matching the value TestTokens.cs signs with (ai-service has no database, so
    // no ConnectionStrings entry is needed here).
    static AiApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? "dev-only-signing-key-do-not-use-in-production-min-32-chars");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiProviderFactory>();
            services.AddSingleton<IAiProviderFactory>(new ListAiProviderFactory([Provider]));
        });
    }
}
