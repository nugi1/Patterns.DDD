using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Patterns.DDD.MassTransit.Tests;

[Collection(InfrastructureCollection.Name)]
public sealed class InboxTests(Infrastructure infrastructure) : IAsyncLifetime
{
    public Task InitializeAsync() => infrastructure.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_notification_delivered_twice_is_handled_once_and_its_events_go_out_once()
    {
        await using var app = await infrastructure.StartHostAsync(nameof(A_notification_delivered_twice_is_handled_once_and_its_events_go_out_once));
        var notification = new BlockNotified("00ab", Guid.NewGuid());
        var messageId = MessageIds.FromKey($"block:{notification.Hash}");

        // Like ZMQ resending after a reconnect: same block, same natural key, same MessageId.
        var bus = app.Services.GetRequiredService<IBus>();
        for (var i = 0; i < 3; i++)
            await bus.Publish(notification, ctx => ctx.MessageId = messageId);

        await TestHost.EventuallyAsync(() => app.Received.Deposits.Count >= 1);
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Single(app.Received.Blocks);   // the inbox skipped the duplicates
        Assert.Single(app.Received.Deposits); // the event raised in the consumer went out via its outbox
    }

    [Fact]
    public void Message_ids_are_deterministic_version_5_uuids()
    {
        var a = MessageIds.FromKey("block:00ab");

        Assert.Equal(a, MessageIds.FromKey("block:00ab"));
        Assert.NotEqual(a, MessageIds.FromKey("tx:00ab"));
        Assert.Equal('5', a.ToString()[14]); // version nibble
    }
}
