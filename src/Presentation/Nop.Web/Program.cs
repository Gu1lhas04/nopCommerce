using Autofac.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Web.Framework.Infrastructure.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nop.Web;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddJsonFile(NopConfigurationDefaults.AppSettingsFilePath, true, true);
        if (!string.IsNullOrEmpty(builder.Environment?.EnvironmentName))
        {
            var path = string.Format(NopConfigurationDefaults.AppSettingsEnvironmentFilePath, builder.Environment.EnvironmentName);
            builder.Configuration.AddJsonFile(path, true, true);
        }
        builder.Configuration.AddEnvironmentVariables();

        //load application settings
        builder.Services.ConfigureApplicationSettings(builder);

        var appSettings = Singleton<AppSettings>.Instance;
        var useAutofac = appSettings.Get<CommonConfig>().UseAutofac;

        if (useAutofac)
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        else
        {
            builder.Host.UseDefaultServiceProvider(options =>
            {
                //we don't validate the scopes, since at the app start and the initial configuration we need 
                //to resolve some services (registered as "scoped") through the root container
                options.ValidateScopes = false;
                options.ValidateOnBuild = true;
            });
        }

        //configure OpenTelemetry
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4317";
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("nopcommerce"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = ctx =>
                    {
                        var path = ctx.Request.Path.Value ?? string.Empty;
                        // exclude static assets, health checks, and the metrics scrape endpoint
                        return !path.StartsWith("/content", StringComparison.OrdinalIgnoreCase)
                            && !path.StartsWith("/scripts", StringComparison.OrdinalIgnoreCase)
                            && !path.StartsWith("/images", StringComparison.OrdinalIgnoreCase)
                            && !path.StartsWith("/fonts", StringComparison.OrdinalIgnoreCase)
                            && !path.Equals("/metrics", StringComparison.OrdinalIgnoreCase)
                            && !path.Equals("/health", StringComparison.OrdinalIgnoreCase);
                    };
                })
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource(CatalogueTelemetry.ActivitySourceName)
                .AddSource("nopcommerce.events")
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter(CatalogueTelemetry.MeterName)
                .AddPrometheusExporter());

        //add services to the application and configure service provider
        builder.Services.ConfigureApplicationServices(builder);

        var app = builder.Build();

        //configure the application HTTP request pipeline
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        app.ConfigureRequestPipeline();
        await app.PublishAppStartedEventAsync();

        await app.RunAsync();
    }
}