namespace Patterns.DDD.Application;

/// <summary>
/// A fact published outside the bounded context. Unlike a domain event, it is a contract: other
/// services depend on its name and shape, so it changes by adding a new version, never in place.
/// Every implementation needs a <see cref="MessageTypeAttribute"/>.
/// </summary>
public interface IIntegrationEvent;
