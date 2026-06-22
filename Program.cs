using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using ShippingPromiseService.Api;
using ShippingPromiseService.Application;
using ShippingPromiseService.Application.Ports;
using ShippingPromiseService.Infrastructure.Cache;
using ShippingPromiseService.Infrastructure.Clients;
using ShippingPromiseService.Infrastructure.Messaging;
using ShippingPromiseService.Infrastructure.Persistence;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Environment.ApplicationName;
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:5107";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection(KafkaOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddDbContext<ShippingPromiseDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("ShippingPromiseDb"));
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "shipping-promise:";
});

builder.Services.AddScoped<ShippingPromiseApplicationService>();
builder.Services.AddScoped<PackageCalculator>();
builder.Services.AddScoped<DeliveryDecisionEngine>();
builder.Services.AddScoped<FallbackEngine>();

builder.Services.AddScoped<IShippingPromiseCache, RedisShippingPromiseCache>();
builder.Services.AddScoped<IShippingPromiseAuditRepository, ShippingPromiseAuditRepository>();
builder.Services.AddSingleton<IShippingPromiseEventPublisher, KafkaShippingPromiseEventPublisher>();
builder.Services.AddHostedService<ShippingQuoteRequestedConsumer>();

builder.Services.AddHttpClient<IProductCatalogClient, ProductCatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ProductCatalog"]!);
    client.Timeout = TimeSpan.FromMilliseconds(500);
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient<IInventoryClient, InventoryClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Inventory"]!);
    client.Timeout = TimeSpan.FromMilliseconds(500);
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient<IFulfillmentClient, FulfillmentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Fulfillment"]!);
    client.Timeout = TimeSpan.FromMilliseconds(500);
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient<IRoutingClient, RoutingClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Routing"]!);
    client.Timeout = TimeSpan.FromMilliseconds(500);
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient<ICarrierClient, CarrierClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Carrier"]!);
    client.Timeout = TimeSpan.FromMilliseconds(500);
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient<IPricingClient, PricingClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Pricing"]!);
    client.Timeout = TimeSpan.FromMilliseconds(500);
})
.AddStandardResilienceHandler();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ShippingPromiseDbContext>();

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapShippingPromiseEndpoints();

app.Run();
