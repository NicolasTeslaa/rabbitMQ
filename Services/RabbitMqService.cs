using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

public sealed class RabbitMqService : IAsyncDisposable
{
    private readonly RabbitOptions _opt;
    private readonly Task<IConnection> _connectionTask;

    public RabbitMqService(IOptions<RabbitOptions> opt)
    {
        _opt = opt.Value;

        var factory = new ConnectionFactory
        {
            HostName = _opt.HostName,
            Port = _opt.Port,
            UserName = _opt.UserName,
            Password = _opt.Password,
        };

        // RabbitMQ.Client v7+: async API
        _connectionTask = factory.CreateConnectionAsync();
    }

    public async Task PublishAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var connection = await _connectionTask.ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

        // Ensure queue exists
        await channel.QueueDeclareAsync(
            queue: _opt.QueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct
        ).ConfigureAwait(false);

        var body = Encoding.UTF8.GetBytes(message);
        var props = new BasicProperties
        {
            ContentType = "text/plain",
            DeliveryMode = DeliveryModes.Transient // 1 = non-persistent, 2 = persistent
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _opt.QueueName,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct
        ).ConfigureAwait(false);
    }

    public async Task<string?> PullOneAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var connection = await _connectionTask.ConfigureAwait(false);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            queue: _opt.QueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct
        ).ConfigureAwait(false);

        // BasicGet-style: pull one message
        var result = await channel.BasicGetAsync(
            queue: _opt.QueueName,
            autoAck: true,
            cancellationToken: ct
        ).ConfigureAwait(false);

        if (result is null)
            return null;

        return Encoding.UTF8.GetString(result.Body.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_connectionTask.IsCompletedSuccessfully)
            {
                var conn = _connectionTask.Result;
                await conn.CloseAsync().ConfigureAwait(false);
                await conn.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // ignore dispose exceptions
        }
    }
}
