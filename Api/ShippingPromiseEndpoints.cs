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
            CancellationToken cancellationToken) =>
        {
            var response = await service.CalculateAsync(request, cancellationToken);

            return Results.Ok(response);
        })
        .WithName("CalculateShippingPromise")
        .Produces<ShippingPromiseResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
