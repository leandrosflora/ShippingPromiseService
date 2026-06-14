using ShippingPromiseService.Application;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Api;

public static class ShippingPromiseEndpoints
{
    public static IEndpointRouteBuilder MapShippingPromiseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/shipping-promises")
            .WithTags("Shipping Promises");

        group.MapPost("/", async (
            ShippingPromiseRequest request,
            ShippingPromiseApplicationService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var correlationId = ResolveCorrelationId(httpContext);
            var response = await service.CalculateAsync(request, correlationId, cancellationToken);

            return Results.Ok(response);
        })
        .WithName("CalculateShippingPromise")
        .Produces<ShippingPromiseResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var headerValues) &&
            !string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
        {
            return headerValues.First()!;
        }

        return httpContext.TraceIdentifier;
    }
}
