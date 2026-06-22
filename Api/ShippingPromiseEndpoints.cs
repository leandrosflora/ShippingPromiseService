using ShippingPromiseService.Application;
using ShippingPromiseService.Contracts;

namespace ShippingPromiseService.Api;

public static class ShippingPromiseEndpoints
{
    public static IEndpointRouteBuilder MapShippingPromiseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/shipping-promises")
            .WithTags("Shipping Promises");

        group.MapPost("", async Task<IResult> (
            ShippingPromiseRequest request,
            ShippingPromiseApplicationService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var correlationId = ResolveCorrelationId(httpContext);

            try
            {
                var response = await service.CalculateAsync(request, correlationId, cancellationToken);

                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(
                    title: "Invalid shipping promise request",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CalculateShippingPromise")
        .Produces<ShippingPromiseResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{promiseId}", async Task<IResult> (
            string promiseId,
            ShippingPromiseApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.GetCalculatedPromiseAsync(promiseId, cancellationToken);

            return response is null
                ? Results.NotFound()
                : Results.Ok(response);
        })
        .WithName("GetShippingPromise")
        .Produces<ShippingPromiseResponse>()
        .Produces(StatusCodes.Status404NotFound);

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
