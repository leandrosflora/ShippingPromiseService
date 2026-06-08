# ShippingPromiseService

O **Shipping Promise Service** é o motor síncrono de decisão logística usado pelo checkout para responder:

> Para este comprador, vendedor, destino e conjunto de itens, quais opções de entrega existem, quanto custam e qual prazo pode ser prometido?

Ele **não cria pedido**, **não cria remessa** e **não faz rastreamento**. A responsabilidade do serviço é calcular uma promessa de entrega segura para ser exibida no checkout.

## Visão geral da arquitetura

A aplicação foi implementada em ASP.NET Core Minimal API e é composta por:

- **API síncrona** para cálculo de promessa de entrega.
- **Cache Redis** com TTL curto para promessas finais.
- **Decision Engine** para escolher a melhor opção entre candidatos logísticos.
- **Fallback Engine** conservador para falhas temporárias de dependências externas.
- **Clients HTTP resilientes** para integrações com Product Catalog, Inventory, Fulfillment, Routing, Carrier e Pricing.
- **PostgreSQL** para auditoria das decisões tomadas pelo motor.

## Fluxo principal

```text
POST /shipping-promises/
    ↓
Valida a requisição
    ↓
Monta a chave de cache
    ↓
Busca promessa no Redis
    ↓ cache miss
Consulta Product Catalog + Inventory + Fulfillment em paralelo
    ↓
Calcula pacote e peso cúbico
    ↓
Consulta rotas disponíveis
    ↓
Valida disponibilidade da transportadora
    ↓
Consulta preço
    ↓
Monta candidatos de entrega
    ↓
Decision Engine escolhe a melhor promessa
    ↓
Grava cache com TTL curto
    ↓
Grava auditoria no PostgreSQL
    ↓
Retorna promessa ao checkout
```

## Endpoints

### `POST /shipping-promises/`

Calcula a promessa logística para comprador, vendedor, destino e itens.

Exemplo de requisição:

```json
{
  "buyerId": "11111111-1111-1111-1111-111111111111",
  "sellerId": "22222222-2222-2222-2222-222222222222",
  "destination": {
    "zipCode": "01310-100",
    "city": "São Paulo",
    "state": "SP",
    "country": "BR"
  },
  "items": [
    {
      "skuId": "33333333-3333-3333-3333-333333333333",
      "quantity": 1,
      "unitPrice": 199.90
    }
  ]
}
```

Exemplo de resposta disponível:

```json
{
  "available": true,
  "promiseId": "promise_9f5c2a",
  "mode": "FULFILLMENT",
  "carrier": "MELI_LOGISTICS",
  "estimatedDeliveryDate": "2026-06-10",
  "cost": 12.90,
  "source": "Calculated",
  "unavailableReason": null
}
```

Exemplo de resposta indisponível:

```json
{
  "available": false,
  "promiseId": null,
  "mode": null,
  "carrier": null,
  "estimatedDeliveryDate": null,
  "cost": null,
  "source": "Calculated",
  "unavailableReason": "No route or inventory available"
}
```

### `GET /health`

Verifica a saúde da aplicação, incluindo a conectividade do `DbContext` configurado para PostgreSQL.

## Configuração local

Configure os valores abaixo em `appsettings.json`, variáveis de ambiente ou user secrets:

```json
{
  "ConnectionStrings": {
    "ShippingPromiseDb": "Host=localhost;Port=5432;Database=shipping_promise;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "Services": {
    "ProductCatalog": "https://product-catalog.local",
    "Inventory": "https://inventory.local",
    "Fulfillment": "https://fulfillment.local",
    "Routing": "https://routing.local",
    "Carrier": "https://carrier.local",
    "Pricing": "https://pricing.local"
  }
}
```

## Banco de dados

A auditoria das decisões é armazenada na tabela `shipping_promise_audits`.

O schema mínimo está em:

```text
Infrastructure/Persistence/schema.sql
```

Campos persistidos:

- `request_json`: requisição original.
- `response_json`: resposta retornada ao checkout.
- `candidates_json`: candidatos avaliados pelo motor de decisão.
- `created_at`: data/hora da auditoria.

## Cache

O Redis armazena a promessa final com TTL curto, pois o resultado depende de informações que envelhecem rapidamente:

- estoque;
- capacidade do fulfillment center;
- cutoff logístico;
- disponibilidade de transportadora;
- destino e região;
- preço de frete.

## Fallback

O fallback é propositalmente conservador. Em caso de falha temporária, o serviço pode retornar uma promessa mais lenta e segura para destinos no Brasil, sem prometer entregas agressivas sem confiança operacional.

## Executando

```bash
dotnet restore
dotnet build
dotnet run
```

Após iniciar a aplicação, use o arquivo `ShippingPromiseService.http` para testar o health check e o cálculo de promessa.
