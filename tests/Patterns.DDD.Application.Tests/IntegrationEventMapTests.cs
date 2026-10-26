namespace Patterns.DDD.Application.Tests;

public class IntegrationEventMapTests
{
    private sealed record BlockConnected(string Hash, int Height, bool IsReorg) : IDomainEvent;
    private sealed record InternalOnly : IDomainEvent;
    private sealed record BlockConnectedV1(string Hash, int Height) : IIntegrationEvent;
    private sealed record TipChangedV1(string Hash) : IIntegrationEvent;

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
    public void Map_cannot_change_once_in_use()
    {
        var map = new IntegrationEventMap();
        map.Translate(new InternalOnly());

        Assert.Throws<InvalidOperationException>(() =>
            map.Map((BlockConnected e) => new TipChangedV1(e.Hash)));
    }
}
