using KF.Settings.Data;
using Microsoft.EntityFrameworkCore;

namespace KF.Settings.Tests.Helpers;

internal sealed class InMemoryDbContextFactory : IDbContextFactory<KFSettingsDbContext>
{
    private readonly DbContextOptions<KFSettingsDbContext> _options;

    public InMemoryDbContextFactory(string name)
    {
        _options = new DbContextOptionsBuilder<KFSettingsDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
    }

    public KFSettingsDbContext CreateDbContext() => new(_options);

    public Task<KFSettingsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new KFSettingsDbContext(_options));
}
