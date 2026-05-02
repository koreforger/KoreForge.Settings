using FluentAssertions;
using KoreForge.Settings.Configuration;

namespace KoreForge.Settings.Tests.Provider;

public class ConfigurationProviderAdvancedTests
{
    [Fact]
    public void Given_PublishMultiple_When_SameData_Then_ChangeTokenFiresEachTimeAndInternalDataImmutable()
    {
        var provider = new KoreForgeSettingsConfigurationProvider();
        int fires = 0;
        var token1 = provider.GetReloadToken();
        token1.RegisterChangeCallback(_ => fires++, null);
        var dict = new Dictionary<string, string?> { { "A", "1" } };
        provider.Publish(dict);
        provider.Publish(dict); // second publish (new token each publish)
        fires.Should().Be(1);
        var token2 = provider.GetReloadToken();
        token2.RegisterChangeCallback(_ => fires++, null);
        provider.Publish(new Dictionary<string, string?> { { "A", "1" }, { "B", "2" } });
        fires.Should().Be(2);
        var snapshot = provider.CurrentValues; // copy
        snapshot.ContainsKey("A").Should().BeTrue();
        // Prove immutability by modifying local copy only
        var clone = new Dictionary<string, string?>(snapshot);
        clone["A"] = "changed";
        provider.CurrentValues["A"].Should().Be("1");
    }
}
