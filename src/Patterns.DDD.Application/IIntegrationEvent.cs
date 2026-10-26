namespace Patterns.DDD.Application;

/// <summary>
/// A fact published outside the bounded context. Unlike a domain event it is a contract: other
/// services depend on its name and shape, so it changes by adding a new version (e.g.
/// <c>BlockConnectedV2</c>), never in place. Give it a stable wire name with your transport's
/// mechanism, e.g. MassTransit's <c>[MessageUrn]</c>.
/// </summary>
public interface IIntegrationEvent;
