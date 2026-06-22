# ShippingPromiseService

Documentação em português (pt-BR) do microserviço **ShippingPromiseService**, responsável por calcular promessas de entrega para o checkout a partir de dados de comprador, vendedor, destino, itens, estoque, fulfillment, rotas, transportadoras e precificação.

---

## Sumário

- [Objetivo do serviço](#objetivo-do-serviço)
- [Escopo funcional](#escopo-funcional)
- [Arquitetura](#arquitetura)
- [Fluxo de decisão](#fluxo-de-decisão)
- [Endpoints HTTP](#endpoints-http)
- [Contratos da API](#contratos-da-api)
- [Regras de negócio](#regras-de-negócio)
- [Integrações externas](#integrações-externas)
- [Cache Redis](#cache-redis)
- [Auditoria em PostgreSQL](#auditoria-em-postgresql)
- [Kafka](#kafka)
- [Fallback operacional](#fallback-operacional)
- [Configuração](#configuração)
- [Execução local](#execução-local)
- [Swagger e arquivo HTTP](#swagger-e-arquivo-http)
- [Observabilidade e resiliência](#observabilidade-e-resiliência)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Limitações e pontos de atenção](#limitações-e-pontos-de-atenção)

---

## Objetivo do serviço

O **ShippingPromiseService** é o motor síncrono de decisão logística usado pelo checkout para responder à pergunta:

> Para este comprador, vendedor, destino e conjunto de itens, quais opções de entrega existem, quanto custam e qual prazo pode ser prometido?

O serviço calcula uma promessa logística segura para exibição antes da compra. Ele avalia disponibilidade de produto, estoque por centro de fulfillment, capacidade operacional, rotas disponíveis, disponibilidade de transportadora, custo de frete e data estimada de entrega.

---

## Escopo funcional

### O que o serviço faz

- Recebe uma solicitação de promessa de entrega via `POST /shipping-promises/`.
- Valida comprador, vendedor, destino e itens.
- Consulta informações físicas dos produtos.
- Consulta disponibilidade de estoque para os SKUs solicitados.
- Consulta centros de fulfillment candidatos para o vendedor e destino.
- Calcula dados do pacote, incluindo peso real e peso cúbico.
- Consulta rotas logísticas disponíveis.
- Verifica disponibilidade de transportadora para o destino.
- Consulta preço de frete.
- Monta candidatos de entrega.
- Seleciona o melhor candidato com base em prazo, custo e prioridade.
- Retorna a promessa final ao checkout.
- Armazena o resultado em cache Redis por curto período.
- Persiste uma auditoria da decisão em PostgreSQL.
- Executa fallback conservador em falhas temporárias.

### O que o serviço não faz

- Não cria pedidos.
- Não cria remessas.
- Não reserva estoque.
- Não agenda coleta.
- Não executa tracking/rastreamento.
- Não confirma a contratação da transportadora.
- Não substitui os serviços especialistas de catálogo, estoque, fulfillment, rotas, transportadoras ou preços.

---

## Arquitetura

A aplicação foi implementada em **ASP.NET Core Minimal API** com **.NET 8**.

Componentes principais:

- **API HTTP**: expõe os endpoints do microserviço.
- **Application Service**: orquestra validação, cache, chamadas externas, cálculo de candidatos, decisão, auditoria e fallback.
- **Decision Engine**: escolhe a melhor opção entre os candidatos logísticos.
- **Package Calculator**: consolida dimensões, peso real, peso cúbico e flags do pacote.
- **Fallback Engine**: constrói uma promessa conservadora quando dependências falham.
- **Clients HTTP resilientes**: integram com Product Catalog, Inventory, Fulfillment, Routing, Carrier e Pricing.
- **Redis**: armazena promessas finais por TTL curto.
- **PostgreSQL**: armazena auditoria das decisões.
- **Kafka**: consome `checkout.shipping.quote.requested` e publica `shipping.promise.calculated` após uma promessa disponível ser calculada ou recuperada do cache para uma solicitação assíncrona, usando o envelope padrão da arquitetura Meli Envios.
- **Health Checks**: validam a saúde da aplicação e do `DbContext`.
- **Swagger/OpenAPI**: disponível em ambiente de desenvolvimento.

### Diagrama lógico

```text
Checkout
   |
   | POST /shipping-promises/
   v
ShippingPromiseService
   |
   +--> Redis Cache
   |
   +--> Product Catalog Service
   +--> Inventory Service
   +--> Fulfillment Service
   +--> Routing Service
   +--> Carrier Service
   +--> Pricing Service
   |
   +--> Decision Engine
   |
   +--> PostgreSQL Audit
   |
   v
Resposta de promessa de entrega
```

---

## Fluxo de decisão

```text
POST /shipping-promises/
    ↓
Valida a requisição
    ↓
Gera chave de cache
    ↓
Busca promessa no Redis
    ↓
Se cache hit: retorna promessa com Source = "Cache"
    ↓
Se cache miss: consulta Product Catalog, Inventory e Fulfillment em paralelo
    ↓
Valida se todas as informações de produto foram retornadas
    ↓
Bloqueia item restrito
    ↓
Calcula pacote e peso cúbico
    ↓
Filtra centros de fulfillment com capacidade
    ↓
Valida estoque de todos os itens no centro de fulfillment
    ↓
Consulta rotas disponíveis para origem, destino e pacote
    ↓
Verifica disponibilidade da transportadora
    ↓
Consulta preço do frete
    ↓
Monta candidatos de entrega
    ↓
Seleciona o melhor candidato
    ↓
Grava cache com TTL curto
    ↓
Grava auditoria no PostgreSQL
    ↓
Publica `shipping.promise.calculated` no Kafka com o mesmo `X-Correlation-Id` da requisição quando houver `checkoutId`
    ↓
Retorna promessa ao checkout
```

---

## Endpoints HTTP

### `POST /shipping-promises/`

Calcula a promessa de entrega para um comprador, vendedor, destino e lista de itens.

#### Requisição

```http
POST /shipping-promises/
Content-Type: application/json
```

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

#### Resposta com promessa disponível

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

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

#### Resposta sem promessa disponível

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

#### Possíveis valores de `source`

| Valor | Significado |
| --- | --- |
| `Calculated` | Promessa calculada a partir das integrações em tempo real. |
| `Cache` | Promessa retornada a partir do Redis. |
| `Fallback` | Promessa conservadora gerada durante falha temporária. |

#### Possíveis valores de `mode`

| Valor | Descrição |
| --- | --- |
| `FULFILLMENT` | Entrega via malha própria/fulfillment. |
| `FLEX` | Modal flexível. O domínio possui o modo, mas o fluxo atual não o seleciona diretamente. |
| `CROSSDOCKING` | Modal cross-docking. O domínio possui o modo, mas o fluxo atual não o seleciona diretamente. |
| `SELLERSHIPPING` | Entrega realizada pelo vendedor, usada no fallback conservador. |
| `CARRIER` | Entrega por transportadora externa. |

> Observação: no fluxo atual, rotas com carrier `MELI_LOGISTICS` são classificadas como `FULFILLMENT`; demais carriers são classificados como `CARRIER`.

---

### `GET /health`

Executa o health check da aplicação.

```http
GET /health
Accept: application/json
```

O health check inclui a conectividade do `ShippingPromiseDbContext` configurado para PostgreSQL.

---

## Contratos da API

### `ShippingPromiseRequest`

| Campo | Tipo | Obrigatório | Descrição |
| --- | --- | --- | --- |
| `buyerId` | `Guid` | Sim | Identificador do comprador. |
| `sellerId` | `Guid` | Sim | Identificador do vendedor. |
| `destination` | `AddressDto` | Sim | Endereço de destino. |
| `items` | Lista de `ShippingPromiseItemDto` | Sim | Itens que compõem a entrega. |

### `AddressDto`

| Campo | Tipo | Obrigatório | Descrição |
| --- | --- | --- | --- |
| `zipCode` | `string` | Sim | CEP ou código postal. |
| `city` | `string` | Sim | Cidade de destino. |
| `state` | `string` | Sim | Estado/UF. |
| `country` | `string` | Sim | País, por exemplo `BR`. |

### `ShippingPromiseItemDto`

| Campo | Tipo | Obrigatório | Descrição |
| --- | --- | --- | --- |
| `skuId` | `Guid` | Sim | Identificador do SKU. |
| `quantity` | `int` | Sim | Quantidade solicitada. Deve ser maior que zero. |
| `unitPrice` | `decimal` | Sim | Preço unitário do item informado pelo checkout. |

### `ShippingPromiseResponse`

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `available` | `bool` | Indica se existe promessa disponível. |
| `promiseId` | `string?` | Identificador gerado para a promessa. Nulo quando indisponível. |
| `mode` | `string?` | Modal logístico selecionado. Nulo quando indisponível. |
| `carrier` | `string?` | Transportadora selecionada. Nulo quando indisponível. |
| `estimatedDeliveryDate` | `DateOnly?` | Data estimada de entrega. Nula quando indisponível. |
| `cost` | `decimal?` | Custo final do frete. Nulo quando indisponível. |
| `source` | `string` | Origem da resposta: `Calculated`, `Cache` ou `Fallback`. |
| `unavailableReason` | `string?` | Motivo de indisponibilidade. Nulo quando há promessa disponível. |

---

## Regras de negócio

### Validação de entrada

A requisição é rejeitada por exceção de argumento quando:

- A requisição é nula.
- `buyerId` é `Guid.Empty`.
- `sellerId` é `Guid.Empty`.
- `destination` é nulo.
- `items` é nulo ou vazio.
- `destination.zipCode` está vazio ou em branco.
- Algum item possui `quantity <= 0`.

### Informações de produto

- O serviço consulta informações físicas dos SKUs em lote.
- Se o Product Catalog não retornar dados para todos os SKUs, a promessa fica indisponível com motivo `Product information unavailable`.
- Se algum item for restrito (`isRestricted = true`), a promessa fica indisponível com motivo `Restricted item`.

### Cálculo do pacote

O cálculo consolida:

- Peso total real (`TotalWeightKg`).
- Peso cúbico (`CubicWeightKg`).
- Maior altura entre os itens.
- Maior largura entre os itens.
- Soma dos comprimentos multiplicados pela quantidade.
- Presença de item frágil.
- Presença de item restrito.

Fórmula utilizada para peso cúbico:

```text
peso_cubico = (altura_maxima_cm * largura_maxima_cm * comprimento_total_cm) / 6000
```

### Seleção de centros de fulfillment

Para cada centro de fulfillment candidato:

1. O centro precisa ter capacidade (`hasCapacity = true`).
2. O centro precisa possuir estoque suficiente para todos os itens da requisição.
3. Apenas então o serviço consulta rotas para aquela origem.

### Construção dos candidatos de entrega

Um candidato é criado quando:

- A rota está marcada como disponível.
- A transportadora está disponível para o destino.
- O preço de frete foi consultado.

O custo final do candidato é calculado como:

```text
custo_final = cost - discount
```

Quando o desconto é nulo, considera-se desconto zero.

### Cálculo da data estimada

A data estimada depende do horário de corte (`cutoffTime`) do fulfillment center e dos dias de trânsito (`transitDays`) da rota.

```text
se horário_atual_utc > cutoffTime:
    data_inicio = amanhã
senão:
    data_inicio = hoje

data_estimada = data_inicio + transitDays
```

### Escolha do melhor candidato

O `DeliveryDecisionEngine` ordena os candidatos por:

1. Menor data estimada de entrega.
2. Menor custo de frete.
3. Menor prioridade numérica.

O primeiro candidato dessa ordenação é selecionado.

### Indisponibilidade

A resposta fica indisponível quando:

- Faltam informações físicas de produto.
- Existe item restrito.
- Não há rota ou estoque disponível.
- Há falha temporária sem possibilidade de fallback.

---

## Integrações externas

As integrações são feitas por `HttpClient` tipado com `AddStandardResilienceHandler()` e timeouts curtos configurados no bootstrap da aplicação.

### Product Catalog

- **Base URL**: `Services:ProductCatalog`
- **Endpoint chamado**: `POST /products/physical-info/batch`
- **Objetivo**: obter dados físicos dos SKUs.
- **Request enviada**:

```json
{
  "skuIds": ["33333333-3333-3333-3333-333333333333"]
}
```

- **Resposta esperada**:

```json
[
  {
    "skuId": "33333333-3333-3333-3333-333333333333",
    "weightKg": 1.2,
    "heightCm": 10,
    "widthCm": 20,
    "lengthCm": 30,
    "category": "electronics",
    "isFragile": false,
    "isRestricted": false
  }
]
```

### Inventory

- **Base URL**: `Services:Inventory`
- **Endpoint chamado**: `POST /inventory/availability/batch`
- **Objetivo**: consultar disponibilidade por SKU e centro de fulfillment.
- **Request enviada**:

```json
{
  "sellerId": "22222222-2222-2222-2222-222222222222",
  "skuIds": ["33333333-3333-3333-3333-333333333333"]
}
```

- **Resposta esperada**:

```json
[
  {
    "skuId": "33333333-3333-3333-3333-333333333333",
    "fulfillmentCenterId": "44444444-4444-4444-4444-444444444444",
    "availableQuantity": 10
  }
]
```

### Fulfillment

- **Base URL**: `Services:Fulfillment`
- **Endpoint chamado**: `POST /fulfillment-centers/candidates/search`
- **Objetivo**: localizar centros candidatos para vendedor e destino.
- **Request enviada**:

```json
{
  "sellerId": "22222222-2222-2222-2222-222222222222",
  "destinationPostalCode": "01310-100",
  "mode": "Fulfillment",
  "package": {
    "weightKg": 1.2,
    "cubicWeightKg": 1.0,
    "isFragile": false,
    "isRestricted": false
  },
  "requestedAtUtc": "2026-06-10T12:00:00Z"
}
```

- **Resposta esperada**:

```json
[
  {
    "fulfillmentCenterId": "44444444-4444-4444-4444-444444444444",
    "region": "SP",
    "cutoffTime": "18:00:00",
    "hasCapacity": true,
    "capacityScore": 1
  }
]
```

### Routing

- **Base URL**: `Services:Routing`
- **Endpoint chamado**: `POST /routes/search`
- **Objetivo**: listar rotas disponíveis para origem, destino e pacote.
- **Request enviada**:

```json
{
  "originNodeId": "44444444-4444-4444-4444-444444444444",
  "destinationPostalCode": "01310-100",
  "package": {
    "weightKg": 1.2,
    "cubicWeightKg": 1.0,
    "isFragile": false,
    "isRestricted": false
  },
  "requestedAtUtc": "2026-06-10T12:00:00Z",
  "maxOptions": 3
}
```

- **Resposta esperada**:

```json
[
  {
    "routeId": "route-001",
    "originNodeId": "44444444-4444-4444-4444-444444444444",
    "destinationNodeId": "SP-CAPITAL",
    "carrierCode": "MELI_LOGISTICS",
    "serviceLevelCode": "STANDARD",
    "transitDays": 2,
    "available": true,
    "priority": 1
  }
]
```

### Carrier

- **Base URL**: `Services:Carrier`
- **Endpoint chamado**: `POST /carrier-availability/search`
- **Objetivo**: verificar se uma transportadora atende o destino.
- **Request enviada**:

```json
{
  "checks": [
    {
      "carrierCode": "MELI_LOGISTICS",
      "serviceLevelCode": "STANDARD",
      "originNodeId": "44444444-4444-4444-4444-444444444444",
      "destinationNodeId": "SP-CAPITAL",
      "destinationPostalCode": "01310-100",
      "plannedDepartureAtUtc": "2026-06-10T12:00:00Z",
      "package": {
        "weightKg": 1.2,
        "cubicWeightKg": 1.0,
        "isFragile": false,
        "isRestricted": false
      }
    }
  ]
}
```

- **Resposta esperada**:

```json
{
  "available": true
}
```

### Pricing

- **Base URL**: `Services:Pricing`
- **Endpoint chamado**: `POST /shipping-prices/quotes/batch`
- **Objetivo**: calcular preço de frete para modal, carrier e pacote.
- **Request enviada**:

```json
{
  "quotes": [
    {
      "candidateId": "route-001",
      "routeId": "route-001",
      "originNodeId": "44444444-4444-4444-4444-444444444444",
      "carrierCode": "MELI_LOGISTICS",
      "serviceLevelCode": "STANDARD",
      "mode": "Fulfillment",
      "package": {
        "weightKg": 1.2,
        "cubicWeightKg": 1.0,
        "isFragile": false,
        "isRestricted": false
      }
    }
  ]
}
```

- **Resposta esperada**:

```json
{
  "quotes": [
    {
      "candidateId": "route-001",
      "cost": 19.90,
      "discount": 7.00
    }
  ]
}
```

### Comportamento em falhas HTTP

Quando uma integração retorna status não bem-sucedido, o client registra warning e retorna um valor seguro:

| Integração | Retorno seguro |
| --- | --- |
| Product Catalog | Lista vazia. |
| Inventory | Lista vazia. |
| Fulfillment | Lista vazia. |
| Routing | Lista vazia. |
| Carrier | `false`. |
| Pricing | Preço `0` e desconto `null`. |

---

## Cache Redis

O Redis armazena a promessa final com TTL curto de **60 segundos**.

### Por que o TTL é curto?

Promessas de entrega envelhecem rapidamente porque dependem de:

- Estoque.
- Capacidade de fulfillment center.
- Horário de corte.
- Disponibilidade de rota.
- Disponibilidade de transportadora.
- Destino e região.
- Política de preço e desconto de frete.

### Chave de cache

A chave é criada a partir de:

- `sellerId`.
- `destination.zipCode`.
- `destination.state`.
- `destination.country`.
- Lista de SKUs e quantidades, ordenada por `skuId`.

O valor bruto é convertido em SHA-256 e prefixado com `promise:`.

Formato final:

```text
promise:{SHA256}
```

> Observação: `buyerId` e `unitPrice` não fazem parte da chave de cache atual.

---

## Auditoria em PostgreSQL

A cada promessa calculada ou fallback aplicado, o serviço salva uma auditoria.

### Tabela

```sql
CREATE TABLE shipping_promise_audits (
    id UUID PRIMARY KEY,
    request_json JSONB NOT NULL,
    response_json JSONB NOT NULL,
    candidates_json JSONB NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL
);
```

### Campos

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `id` | `UUID` | Identificador da auditoria. |
| `request_json` | `JSONB` | Requisição original recebida pelo serviço. |
| `response_json` | `JSONB` | Resposta retornada ao checkout. |
| `candidates_json` | `JSONB` | Candidatos avaliados pelo motor de decisão. |
| `created_at` | `TIMESTAMP WITH TIME ZONE` | Data/hora UTC de criação da auditoria. |

O script mínimo de criação da tabela está em `Infrastructure/Persistence/schema.sql`.

---

## Kafka

O serviço fecha o fluxo assíncrono com o `CheckoutService` via Kafka usando `Confluent.Kafka`, sem acoplar a regra de negócio ao client do broker. A camada `Application` depende do port `IShippingPromiseEventPublisher`; consumers e producers concretos ficam em `Infrastructure/Messaging`.

### Evento consumido

| Campo | Valor |
| --- | --- |
| Tópico | `checkout.shipping.quote.requested` |
| `eventType` | `checkout.shipping.quote.requested` |
| Consumer group | `shipping-promise-service` |

O consumer `ShippingQuoteRequestedConsumer` roda como `BackgroundService`, valida o `eventType`, desserializa o envelope canônico, exige `checkoutId` válido para o fluxo Kafka, mapeia o payload para o request interno de cálculo e chama `ShippingPromiseApplicationService.CalculateAsync` propagando o `correlationId` do envelope. O offset é commitado manualmente somente após o processamento bem-sucedido. Payloads inválidos são logados como erro estruturado e commitados para não travar a partição no E2E local.

Payload recebido em `payload`:

```json
{
  "checkoutId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "buyerId": "11111111-1111-1111-1111-111111111111",
  "sellerId": "22222222-2222-2222-2222-222222222222",
  "destination": {
    "zipCode": "05700-000",
    "city": "São Paulo",
    "state": "SP",
    "country": "BR"
  },
  "items": [
    {
      "skuId": "33333333-3333-3333-3333-333333333333",
      "sellerId": "22222222-2222-2222-2222-222222222222",
      "quantity": 1,
      "unitPrice": 129.90
    }
  ]
}
```

### Evento publicado

| Campo | Valor |
| --- | --- |
| Tópico | `shipping.promise.calculated` |
| `eventType` | `shipping.promise.calculated` |
| `producer` | `shipping-promise-service` |
| Consumidores esperados no contrato | `checkout-service`, `audit-service`, `analytics` |

A publicação ocorre quando existe promessa disponível. No fluxo HTTP síncrono sem `checkoutId`, o serviço mantém compatibilidade e publica com `checkoutId = Guid.Empty` quando não há alternativa. No fluxo Kafka, o `checkoutId` é obrigatório e é propagado no evento `shipping.promise.calculated` para que o `CheckoutService` associe a promessa ao checkout correto. Quando a promessa vem do cache em uma entrada Kafka, o evento também é publicado, pois o checkout precisa da resposta assíncrona mesmo sem novo cálculo.

Payload publicado em `payload`:

```json
{
  "checkoutId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "buyerId": "11111111-1111-1111-1111-111111111111",
  "sellerId": "22222222-2222-2222-2222-222222222222",
  "promiseId": "promise_9f5c2a",
  "mode": "FULFILLMENT",
  "carrier": "MELI_LOGISTICS",
  "estimatedDeliveryDate": "2026-06-15",
  "cost": 14.90,
  "currency": "BRL",
  "source": "Calculated"
}
```

### Envelope do evento

Os eventos consumidos e publicados usam o envelope canônico definido na arquitetura Meli Envios:

```json
{
  "eventId": "9a2a3d5e-1f6d-4a9e-9a7d-93482f4d3a7c",
  "eventType": "shipping.promise.calculated",
  "schemaVersion": "1.0",
  "occurredAt": "2026-06-14T12:00:00Z",
  "correlationId": "corr-123",
  "producer": "shipping-promise-service",
  "payload": {}
}
```

O `correlationId` vem do envelope Kafka no fluxo assíncrono. No fluxo HTTP, ele vem do header `X-Correlation-Id`; quando o header não é enviado, o serviço usa o `TraceIdentifier` da requisição. Logs de consumo e publicação incluem tópico, offset ou message key, `eventType`, `checkoutId` quando disponível e `correlationId`.

### Configuração local

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "ConsumerGroupId": "shipping-promise-service",
    "Topics": {
      "ShippingQuoteRequested": "checkout.shipping.quote.requested",
      "ShippingPromiseCalculated": "shipping.promise.calculated"
    }
  }
}
```

### Como validar no Kafka UI

1. Suba a stack local do repositório `logistica-envios-demo-arch`, garantindo que o broker esteja disponível em `localhost:9092`.
2. Execute este serviço com `dotnet run`.
3. Publique um envelope `checkout.shipping.quote.requested` com `checkoutId` e `correlationId`.
4. Abra `http://localhost:8088`.
5. Acesse o tópico `shipping.promise.calculated`.
6. Confira se a mensagem contém `eventType = shipping.promise.calculated`, o mesmo `checkoutId` do payload recebido e o mesmo `correlationId` do envelope consumido.

Falhas temporárias de Kafka na publicação são registradas como warning e não devem derrubar a resposta HTTP síncrona, pois a integração assíncrona complementa o fluxo já existente do checkout.

---

## Fallback operacional

O fallback é conservador por design.

Quando ocorre uma exceção durante o cálculo:

1. O erro é registrado como warning.
2. O serviço tenta montar uma promessa fallback.
3. O fallback só é criado para destinos cujo país é `BR`.
4. A promessa fallback usa:
   - Modal: `SellerShipping`.
   - Carrier: `DEFAULT_CARRIER`.
   - Prazo: data atual UTC + 7 dias.
   - Custo: `29.90`.
   - Prioridade: `999`.
   - Source: `Fallback`.
5. A decisão fallback também é auditada no PostgreSQL.

Se o destino não for Brasil ou o fallback não puder ser criado, a resposta será indisponível com motivo:

```text
Shipping promise temporarily unavailable
```

---

## Configuração

As configurações podem ser fornecidas por `appsettings.json`, `appsettings.Development.json`, variáveis de ambiente ou user secrets.

### Exemplo de configuração

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
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "ConsumerGroupId": "shipping-promise-service",
    "Topics": {
      "ShippingQuoteRequested": "checkout.shipping.quote.requested",
      "ShippingPromiseCalculated": "shipping.promise.calculated"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Variáveis de ambiente equivalentes

```bash
ConnectionStrings__ShippingPromiseDb='Host=localhost;Port=5432;Database=shipping_promise;Username=postgres;Password=postgres'
ConnectionStrings__Redis='localhost:6379'
Services__ProductCatalog='https://product-catalog.local'
Services__Inventory='https://inventory.local'
Services__Fulfillment='https://fulfillment.local'
Services__Routing='https://routing.local'
Services__Carrier='https://carrier.local'
Services__Pricing='https://pricing.local'
Kafka__BootstrapServers='localhost:9092'
Kafka__ConsumerGroupId='shipping-promise-service'
Kafka__Topics__ShippingQuoteRequested='checkout.shipping.quote.requested'
Kafka__Topics__ShippingPromiseCalculated='shipping.promise.calculated'
```

### Timeouts HTTP configurados

| Integração | Timeout |
| --- | --- |
| Product Catalog | 500 ms |
| Inventory | 600 ms |
| Fulfillment | 600 ms |
| Routing | 700 ms |
| Carrier | 500 ms |
| Pricing | 500 ms |

---

## Execução local

### Pré-requisitos

- .NET SDK 8.x.
- PostgreSQL disponível.
- Redis disponível.
- Kafka disponível em `localhost:9092` para validar a integração assíncrona.
- Serviços externos ou mocks compatíveis com os contratos descritos nesta documentação.

### Preparar banco de dados

Crie o banco e execute o script:

```bash
psql "Host=localhost;Port=5432;Database=shipping_promise;Username=postgres;Password=postgres" \
  -f Infrastructure/Persistence/schema.sql
```

### Restaurar dependências

```bash
dotnet restore
```

### Compilar

```bash
dotnet build
```

### Executar

```bash
dotnet run
```

### Testar publicação Kafka

Com a aplicação em execução e Kafka local em `localhost:9092`, envie uma requisição com correlação explícita:

```bash
curl -X POST http://localhost:5000/shipping-promises/ \
  -H 'Content-Type: application/json' \
  -H 'X-Correlation-Id: local-e2e-001' \
  -d @request.json
```

Depois valide a mensagem no Kafka UI em `http://localhost:8088`, no tópico `shipping.promise.calculated`.

Por padrão, o perfil de desenvolvimento do projeto expõe a aplicação em endereço definido em `Properties/launchSettings.json`.

---

## Swagger e arquivo HTTP

Em ambiente `Development`, a aplicação habilita:

- Swagger JSON.
- Swagger UI.

Também existe o arquivo `ShippingPromiseService.http`, com exemplos para:

- `GET /health`.
- `POST /shipping-promises/`.

Esse arquivo pode ser executado por IDEs como Visual Studio, Rider ou extensões REST Client compatíveis.

---

## Observabilidade e resiliência

### Logs

Os clients HTTP registram warning quando uma dependência externa retorna status HTTP não bem-sucedido.

O serviço principal registra warning quando ocorre exceção no cálculo da promessa e o fluxo precisa tentar fallback.

### Health check

O endpoint `/health` utiliza o mecanismo de health checks do ASP.NET Core e inclui validação do `ShippingPromiseDbContext`.

### Resiliência HTTP

Todos os clients HTTP tipados usam `AddStandardResilienceHandler()`, adicionando políticas padrão de resiliência para chamadas externas.

---

## Estrutura de pastas

```text
.
├── Api/
│   └── ShippingPromiseEndpoints.cs
├── Application/
│   ├── CacheKeyFactory.cs
│   ├── DeliveryDecisionEngine.cs
│   ├── FallbackEngine.cs
│   ├── PackageCalculator.cs
│   ├── ShippingPromiseApplicationService.cs
│   └── Ports/
├── Contracts/
│   ├── ShippingPromiseRequest.cs
│   └── ShippingPromiseResponse.cs
├── Domain/
│   ├── DeliveryCandidate.cs
│   ├── ShippingMode.cs
│   └── ShippingPromise.cs
├── Infrastructure/
│   ├── Cache/
│   ├── Clients/
│   ├── Messaging/
│   └── Persistence/
├── Program.cs
├── ShippingPromiseService.csproj
├── ShippingPromiseService.http
├── appsettings.json
└── README.md
```

### Responsabilidades por camada

| Camada | Responsabilidade |
| --- | --- |
| `Api` | Mapeamento de endpoints HTTP. |
| `Contracts` | DTOs de entrada e saída da API. |
| `Application` | Orquestração dos casos de uso e regras de aplicação. |
| `Application/Ports` | Interfaces e contratos internos para dependências externas. |
| `Domain` | Modelos e conceitos de domínio logístico. |
| `Infrastructure/Clients` | Implementações HTTP dos ports externos. |
| `Infrastructure/Messaging` | Implementação Kafka do producer `shipping.promise.calculated`. |
| `Infrastructure/Cache` | Implementação de cache Redis. |
| `Infrastructure/Persistence` | EF Core, entidade de auditoria, repositório e schema SQL. |

---

## Limitações e pontos de atenção

- O endpoint `POST /shipping-promises/` sempre retorna `200 OK` para respostas de negócio, inclusive indisponibilidade logística.
- As validações lançam `ArgumentException`; a aplicação usa `UseExceptionHandler()`, mas não há middleware customizado de conversão explícita para `400` no código atual.
- A chave de cache não inclui `buyerId` nem `unitPrice`.
- O cache só é gravado para promessas calculadas com sucesso, não para indisponibilidades nem fallback.
- A auditoria é gravada para promessas calculadas com sucesso e fallback; indisponibilidades retornadas antes da seleção de candidatos não são auditadas no fluxo atual.
- O cálculo usa `DateTime.UtcNow` e `TimeOnly` para comparar o cutoff; garanta alinhamento de timezone com os serviços de fulfillment.
- O preço padrão retornado pelo client de Pricing em falha HTTP é zero, mas falhas por exceção entram no fluxo de fallback.
- Não há testes automatizados presentes no repositório atualmente.
- Não há outbox transacional neste repositório; a publicação Kafka é best-effort após auditoria e falhas são registradas sem interromper indevidamente o fluxo HTTP síncrono.

---

## Exemplo completo de operação

1. O checkout envia comprador, vendedor, destino e SKUs.
2. O serviço verifica se já existe promessa recente no Redis.
3. Em cache miss, o serviço consulta catálogo, estoque e fulfillment em paralelo.
4. O serviço calcula o pacote com dimensões agregadas.
5. Para cada fulfillment center com capacidade, valida estoque total.
6. Para cada rota disponível, valida transportadora e preço.
7. O Decision Engine seleciona a opção com menor prazo, menor custo e melhor prioridade.
8. A promessa é cacheada por 60 segundos.
9. A decisão é auditada em PostgreSQL.
10. O checkout recebe uma resposta com `available = true` ou o motivo de indisponibilidade.
