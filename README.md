# ShippingPromiseService

Shipping Promise Service is the synchronous logistics decision engine used by checkout to answer which delivery promise can be safely shown for a buyer, seller, destination, and set of items.

## Runtime shape

The service is implemented as an ASP.NET Core Minimal API with:

- Redis cache for short-lived final promises.
- PostgreSQL audit storage for requests, responses, and delivery candidates.
- Resilient `HttpClient` integrations for Product Catalog, Inventory, Fulfillment, Routing, Carrier, and Pricing.
- A decision engine that ranks candidates by estimated date, cost, and priority.
- A conservative fallback engine for temporary dependency failures.

## Endpoints

- `POST /shipping-promises/` calculates a promise.
- `GET /health` checks service health, including the configured EF Core database context.

## Local dependencies

Configure these settings in `appsettings.json`, environment variables, or user secrets:

- `ConnectionStrings:ShippingPromiseDb`
- `ConnectionStrings:Redis`
- `Services:ProductCatalog`
- `Services:Inventory`
- `Services:Fulfillment`
- `Services:Routing`
- `Services:Carrier`
- `Services:Pricing`

The minimum audit table schema is available in `Infrastructure/Persistence/schema.sql`.
