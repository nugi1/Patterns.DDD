namespace Patterns.DDD.Application.Tests;

public class IntegrationEventMapTests
{
    private sealed record BlockConnected(string Hash, int Height, bool IsReorg) : IDomainEvent;
    private sealed record InternalOnly : IDomainEvent;

    [MessageType("test.block-connected.v1")]
    private sealed record BlockConnectedV1(string Hash, int Height) : IIntegrationEvent;

    [MessageType("test.tip-changed.v1")]
    private sealed record TipChangedV1(string Hash) : IIntegrationEvent;

    private sealed record Unnamed : IIntegrationEvent;

    [Fact]
    public void Translates_mapped_events_in_registration_order()
    {
        var map = new IntegrationEventMap()
            .Map((BlockConnected e) => new BlockConnectedV1(e.Hash, e.Height))
            .Map((BlockConnected e) => new TipChangedV1(e.Hash));

        var events = map.Translate(new BlockConnected("00ab", 101, IsReorg: false));

        Assert.Collection(events,
            e => Assert.Equal(new BlockConnectedV1("00ab", 101), e),
            e => Assert.Equal(new TipChangedV1("00ab"), e));
    }

    [Fact]
    public void Unmapped_domain_events_stay_inside_the_context() =>
        Assert.Empty(new IntegrationEventMap().Translate(new InternalOnly()));

    [Fact]
    public void Null_from_a_map_skips_that_event()
    {
        var map = new IntegrationEventMap()
            .Map((BlockConnected e) => e.IsReorg ? null : new BlockConnectedV1(e.Hash, e.Height));

        Assert.Empty(map.Translate(new BlockConnected("00ab", 101, IsReorg: true)));
    }

    [Fact]
    public void Unnamed_integration_events_are_rejected_at_registration() =>
        Assert.Throws<InvalidOperationException>(() =>
            new IntegrationEventMap().Map((BlockConnected _) => new Unnamed()));

    [Fact]
    public void Map_cannot_change_once_in_use()
    {
        var map = new IntegrationEventMap();
        map.Translate(new InternalOnly());

        Assert.Throws<InvalidOperationException>(() =>
            map.Map((BlockConnected e) => new TipChangedV1(e.Hash)));
    }

    [Fact]
    public void Wire_name_comes_from_the_attribute() =>
        Assert.Equal("test.block-connected.v1", MessageTypes.NameOf<BlockConnectedV1>());

    [Fact]
    public void Envelope_requires_an_id() =>
        Assert.Throws<ArgumentException>(() =>
            new MessageEnvelope(" ", "test.tip-changed.v1", "{}", DateTimeOffset.UtcNow));
}
