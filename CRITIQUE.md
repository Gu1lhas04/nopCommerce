# Critique — Observability in nopCommerce

## What in nopCommerce's design helped or hindered your instrumentation work?

nopCommerce's layered architecture (Core → Data → Services → Presentation) was an advantage: placing `CatalogueTelemetry` in `Nop.Services` kept observability code out of the domain model while remaining reachable from the controllers. ASP.NET Core's native OpenTelemetry support also helped — `AddAspNetCoreInstrumentation` and `AddEntityFrameworkCoreInstrumentation` provided HTTP and database spans for free, establishing a meaningful baseline before any custom code was written.

The main hindrance is that the real work is buried inside factory methods. `PrepareSearchModelAsync` and `PrepareProductDetailsModelAsync` each fan out into dozens of service calls — pricing, stock, media, localisation — none of which propagate or create child spans. A trace shows one large opaque span with no decomposition of where the latency actually comes from. `IEventPublisher` compounded this: event handlers run without activity context forwarding, so any span created inside a handler is detached from the originating request trace. On the sensitive data side, raw search keywords were deliberately excluded from spans and metrics — only derived values (keyword length, result count) are recorded, avoiding PII export to Jaeger and Prometheus.

## If you were making architectural decisions on this project going forward, what would you change to make it more observable — and at what cost?

The highest-value change would be an Autofac interceptor on `ICatalogModelFactory` and `IProductService` that automatically starts and stops child spans around every service call — without touching any business logic. This would turn opaque spans into actionable decompositions ("pricing: 280 ms, stock: 90 ms"). The cost is real: Castle DynamicProxy is required, and correctly propagating `Activity` across `async` method boundaries is error-prone. It is only worth the investment as a platform-wide change, not for a single flow.

A cheaper fix with high return is making `IEventPublisher` activity-aware: capture `Activity.Current` at publish time and restore it before invoking each handler. The change is confined to one class, requires no interface changes, and would automatically link event-driven work to the originating request trace across the entire codebase.

## Where did you have to make a surgical change to the existing code? Why was it necessary, and how did you minimise the impact?

Two files were modified: `CatalogController.cs` and `ProductController.cs`. Controllers are not the ideal instrumentation point — they already carry HTTP binding and response concerns — but the service and factory layers have no ambient hooks, and introducing them would have required cascading interface changes across the Autofac registration graph. Each action method received one `using var activity` statement and one metric call at a point where the required value was already a local variable. No method signatures changed and no existing logic was altered; the diff for each file is under ten lines.

One honest limitation: in `ProductController`, the trace span opens *after* `PrepareProductDetailsModelAsync` completes, because the category name needed as a tag is only available once the model is built. The span is therefore useless as a latency signal — it reflects negligible post-processing time, not the expensive model preparation. The Stopwatch-driven histogram metric does record the correct elapsed time. Fixing the span properly would require the factory interface to accept and propagate an `Activity`, a change disproportionate to its benefit here.
