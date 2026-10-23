using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FleetPulse.Infrastructure.Messaging;

public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options)
    {
        var value = options.Value;
        _factory = new ConnectionFactory
        {
            HostName = value.Host,
            Port = value.Port,
            UserName = value.User,
            Password = value.Password,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            ClientProvidedName = "fleetpulse-api"
        };
    }

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            if (_connection is not null)
                await _connection.DisposeAsync();

            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();

        _gate.Dispose();
    }
}
