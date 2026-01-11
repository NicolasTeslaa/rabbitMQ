using Microsoft.Extensions.Options;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// ===== Services =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bind config: "Rabbit": { HostName, Port, UserName, Password, QueueName }
builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("Rabbit"));
builder.Services.AddSingleton<RabbitMqService>();

var app = builder.Build();

// ===== Middleware =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ===== Endpoints =====

// Publish message
app.MapPost("/rabbit/publish", async (PublishRequest req, RabbitMqService rabbit, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest("Message is required.");

    await rabbit.PublishAsync(req.Message, ct);
    return Results.Ok(new { sent = req.Message });
})
.WithName("RabbitPublish");

// Pull one message (returns 204 if queue empty)
app.MapGet("/rabbit/pull", async (RabbitMqService rabbit, CancellationToken ct) =>
{
    var msg = await rabbit.PullOneAsync(ct);
    return msg is null ? Results.NoContent() : Results.Ok(new { message = msg });
})
.WithName("RabbitPullOne");

app.Run();

// ===== DTOs / Options =====

public sealed record PublishRequest(string Message);

public sealed class RabbitOptions
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string QueueName { get; init; } = "myQueue";
}
