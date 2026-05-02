using KoreForge.Settings.Data;
using Microsoft.EntityFrameworkCore;

namespace KoreForge.Settings.Tests.Helpers;

internal sealed class InMemoryDbContextFactory : IDbContextFactory<KoreForgeSettingsDbContext>
{
    private readonly DbContextOptions<KoreForgeSettingsDbContext> _options;

    public InMemoryDbContextFactory(string name)
    {
        _options = new DbContextOptionsBuilder<KoreForgeSettingsDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
    }

    public KoreForgeSettingsDbContext CreateDbContext() => new(_options);

    public Task<KoreForgeSettingsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new KoreForgeSettingsDbContext(_options));
}
