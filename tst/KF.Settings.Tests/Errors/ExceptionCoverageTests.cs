using FluentAssertions;
using KF.Settings.Errors;

namespace KF.Settings.Tests.Errors;

public class ExceptionCoverageTests
{
    [Fact]
    public void Given_DomainExceptions_When_Instantiated_Then_CodeAndMessageValid()
    {
        var all = new DomainException[]
        {
            new MissingRowVersionException("Key", null, null),
            new ConcurrencyConflictException("Key", null, null, new byte[]{1}, new byte[]{2}),
            new DuplicateKeyException("Key", null, null),
            new RollbackConflictException("Key", null, null, new byte[]{1}, new byte[]{2}),
            new ValidationFailureException("Ctx", new[]{"Err"}),
            new NotFoundException("id", "k", "scope")
        };
        foreach (var e in all)
        {
            ((int)e.Code).Should().BeGreaterOrEqualTo(0);
            e.Message.Should().NotBeNullOrEmpty();
        }
    }
}
