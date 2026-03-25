using System.Diagnostics;

namespace Nop.Services.Events;

/// <summary>
/// Wraps EventPublisher to add an OpenTelemetry span for every published event,
/// propagating the current trace context into each handler invocation.
/// </summary>
public class InstrumentedEventPublisher : EventPublisher
{
    private static readonly ActivitySource _activitySource = new("nopcommerce.events");

    public override async Task PublishAsync<TEvent>(TEvent @event)
    {
        using var activity = _activitySource.StartActivity($"event.{typeof(TEvent).Name}");
        activity?.SetTag("event.type", typeof(TEvent).Name);
        await base.PublishAsync(@event);
    }
}
