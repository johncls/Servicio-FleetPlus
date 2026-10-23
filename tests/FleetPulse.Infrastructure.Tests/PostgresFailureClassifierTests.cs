using FleetPulse.Infrastructure.Postgres;
using Npgsql;

namespace FleetPulse.Infrastructure.Tests;

public sealed class PostgresFailureClassifierTests
{
    [Theory]
    [InlineData("22023")]
    [InlineData("22007")]
    [InlineData("23514")]
    public void Classifies_validation_failures_as_permanent(string sqlState)
    {
        var exception = new PostgresException("rejected", "ERROR", "ERROR", sqlState);

        Assert.True(PostgresFailureClassifier.IsPermanent(exception));
        Assert.False(PostgresFailureClassifier.IsTransient(exception));
    }

    [Fact]
    public void Classifies_serialization_failures_as_transient()
    {
        var exception = new PostgresException("serialization", "ERROR", "ERROR", "40001");

        Assert.True(PostgresFailureClassifier.IsTransient(exception));
        Assert.False(PostgresFailureClassifier.IsPermanent(exception));
    }

    [Fact]
    public void Classifies_timeouts_as_transient()
    {
        Assert.True(PostgresFailureClassifier.IsTransient(new TimeoutException()));
    }
}
