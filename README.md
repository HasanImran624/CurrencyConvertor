# Currency Converter API (ASP.NET Core 8 + Frankfurter)


## Features

Latest rates: GET /api/rates/latest?base=EUR

Convert amounts: GET /api/rates/convert?amount=100&from=USD&to=EUR[&date=YYYY-MM-DD]

Health check: GET /health

JWT auth with role policies (User, Admin)

Rate limiting (fixed window, default 20 req/s)

Caching via IMemoryCache (configurable TTLs)

Serilog structured logs + Seq sink

OpenTelemetry (ASP.NET Core + HttpClient instrumentation)

Resilience with Polly (retry + circuit breaker)

xUnit + Moq unit tests (coverage ready)

## Tech & NuGet

ASP.NET Core 8

Serilog (Serilog.AspNetCore, Serilog.Sinks.Seq, Serilog.Sinks.Console, enrichers)

OpenTelemetry (Extensions.Hosting, Instrumentation.AspNetCore, Instrumentation.Http, Exporter.Console or Exporter.OpenTelemetryProtocol)

Polly (v7 API) + Microsoft.Extensions.Http.Polly

JWT (Microsoft.AspNetCore.Authentication.JwtBearer)

Swagger (Swashbuckle.AspNetCore)

xUnit, Moq, coverlet

---
##  ProjectStructure HighLevel

Api/
  Program.cs
  Controllers/...
Application/
  Abstraction/ (interfaces: IExchangeRateProvider, ICacheService, ...)
  Contracts/   (IRatesService, ITokenService)
  DTOs/        (LatestRatesDto, ConversionResultDto, HistoricalRatesDto, Paged<T>)
  Options/     (FrankfurterOptions, CachingOptions, JwtOptions)
  Services/    (RatesService, MemoryCacheService)
Infrastructure/
  Providers/   (FrankfurterProvider)
  Security/   (TokenService)
tests/
  Unit/        (RatesServiceTests, etc.)
  
---
##  Quick Start

```bash
dotnet restore
dotnet build
dotnet run --project src/Api/Api.csproj
```

Open Swagger: [https://localhost:49713/swagger](http://localhost:49714/swagger)

> Endpoints are protected — get a token via the auth endpoint below.

---
##  Postman Collection

A Postman collection is included for easy testing.

- Import `/postman/CurrencyConverter.postman_collection.json` into Postman.
- Optional: Import `/postman/CurrencyConverter.postman_environment.json` for base URL and token management.

> The collection covers:  
> - Auth (token request)  
> - Latest rates  
> - Convert  
> - Historical rates  
---

##  Authentication (JWT)

### Get a token
```
POST /api/v1/auth/token
{
  "username": "hasan",
  "password": "currency@123"
}
```

### Use the token
Send in header:

```
Authorization: Bearer <token>
```

### Roles
- `hasan` → `User`
- `admin` → `Admin`, `User`

> Configure via `appsettings.json` → `Jwt` (Issuer, Audience, Key, AccessTokenMinutes).

---

##  Endpoints

- `GET /api/v1/rates/latest?base=EUR`
- `GET /api/v1/convert?from=USD&to=GBP&amount=100`
- `GET /api/v1/rates/history?start=2020-01-01&end=2020-01-31&base=EUR&page=1&pageSize=10`

 Excluded currencies: `TRY, PLN, THB, MXN` → 400 Bad Request if used.

---

## Configuration

`src/Api/appsettings.json`:
```json
{
  "Frankfurter": {
    "BaseUrl": "https://api.frankfurter.app/"
  },
  "Caching": {
    "Enabled": true,
    "LatestTtlSeconds": 300,
    "ConvertTtlSeconds": 60,
    "HistoryTtlSeconds": 600
  },
  "Jwt": {
    "Issuer": "currency-converter",
    "Audience": "currency-converter",
    "Key": "secure-key"
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.Seq", "Serilog.Enrichers.Environment" ],
    "MinimumLevel": { "Default": "Information" },
    "Enrich": [ "FromLogContext", "WithMachineName" ],
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Seq",
        "Args": { "serverUrl": "http://localhost:5341" }
      }
    ]
  },
  "AllowedHosts": "*"
}

```

---

## Logging & Seq

Structured logs via Serilog

Local Seq (Docker):

docker run --name seq -d --restart unless-stopped -e ACCEPT_EULA=Y -e SEQ_PASSWORD=<password> -p 5341:80 datalust/seq:latest
use username : admin
set your password accordingly
---
## OpenTelemetry (Traces & Metrics)

Already wired for:

ASP.NET Core (incoming requests)

HttpClient (Frankfurter calls)

Console exporter (dev): add OpenTelemetry.Exporter.Console and enable:
---

##  Tests & Coverage

Run locally:
dotnet test

Collect coverage (collector):
dotnet add tests/Unit package coverlet.collector
dotnet test Unit.Tests.csproj \ --collect:"XPlat Code Coverage" --results-directory ./TestResults

Pretty HTML:

dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html


---

## Design notes

IHttpClientFactory named client "Frankfurter" with Polly:

Retry (3x, exponential backoff) for network/5xx

Circuit breaker to fail fast on repeated faults

Caching: ICacheService wrapper over IMemoryCache, cache keys like rates:latest:EUR, history:<base>:<start>:<end>:p<page>:s<pageSize>

Security: JWT bearer; role policies; dev token endpoint for local testing

Observability: correlation id, Serilog enrichment, OTel traces/metrics

---



