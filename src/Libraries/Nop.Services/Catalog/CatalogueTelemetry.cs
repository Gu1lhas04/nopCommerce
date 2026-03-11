using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Nop.Services.Catalog;

/// <summary>
/// OpenTelemetry instrumentation for the catalogue search and product view flow.
/// </summary>
public static class CatalogueTelemetry
{
    public const string ActivitySourceName = "nopcommerce.catalogue";
    public const string MeterName = "nopcommerce.catalogue";

    private static readonly ActivitySource _activitySource = new(ActivitySourceName);
    private static readonly Meter _meter = new(MeterName);

    // Metric 1: histogram of product page load duration per category.
    // Justification: a spike in a specific category signals slow queries for that category
    // (e.g. too many variants, missing index) before users start reporting slowness.
    public static readonly Histogram<double> ProductPageDuration =
        _meter.CreateHistogram<double>(
            "catalogue.product_page.duration",
            unit: "ms",
            description: "Time to load a product detail page, tagged by category.");

    // Metric 2: counter of searches that returned zero results.
    // Justification: a rising zero-results rate indicates catalogue/indexing problems
    // (out-of-stock, missing products, broken search) that degrade conversion before
    // any error appears in logs.
    public static readonly Counter<long> SearchZeroResults =
        _meter.CreateCounter<long>(
            "catalogue.search.zero_results",
            unit: "{searches}",
            description: "Number of searches that returned zero results.");

    public static Activity? StartSearchActivity(string keyword, int categoryId)
    {
        var activity = _activitySource.StartActivity("catalogue.search");
        activity?.SetTag("search.query_length", keyword?.Length ?? 0);
        activity?.SetTag("search.category_id", categoryId);
        return activity;
    }

    public static Activity? StartProductPageActivity(int productId, string categoryName)
    {
        var activity = _activitySource.StartActivity("catalogue.product_page");
        activity?.SetTag("product.id", productId);
        // use category name (not customer data) – safe to export
        activity?.SetTag("product.category", categoryName ?? "unknown");
        return activity;
    }
}
