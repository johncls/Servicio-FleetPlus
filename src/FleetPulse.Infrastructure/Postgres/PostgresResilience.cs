using FleetPulse.Application;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace FleetPulse.Infrastructure.Postgres;

public static class PostgresResilience
{
    public static ResiliencePipeline Create(TimeSpan timeout)
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder()
                    .Handle<TransientTelemetryException>()
                    .Handle<TimeoutRejectedException>()
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TransientTelemetryException>()
                    .Handle<TimeoutRejectedException>()
            })
            .AddTimeout(timeout)
            .Build();
    }
}
