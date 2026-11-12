# Integration plan — Swagger Petstore - OpenAPI 3.1.0 .NET SDK into a new ASP.NET Core web app

Task: two pages — (1) create a pet order (form: pet id + quantity → `PlaceOrder` → show the created order), (2) view an order (form: order id → `GetOrderById` → show id, pet id, quantity, status, ship date, complete). Read `Petstore:BaseUrl` and `Petstore:ApiKey` from `IConfiguration`. Runnable with `dotnet run`. Fresh, empty repo — no existing app code.

All SDK facts below are grounded in the SDK map (`sdk-map.md` + `map/operations/Store.md`) and the source files those pages name, read this session from the reference clone. Paths in **Source** cells are relative to the SDK root.

---

## 1. Scope & sequence

| # | Step | Uses | Notes |
| --- | --- | --- | --- |
| 1 | Scaffold the ASP.NET Core web app in the repo root (`dotnet new` web/MVC/Razor) | — | Runnable with `dotnet run`; two pages, form POST → shows result |
| 2 | Install the SDK: `dotnet add package SwaggerPetstoreOpenApi310` (version-less) | — | Floats to latest release (dotnet-getting-started *Install*) |
| 3 | Register the client: `services.AddSwaggerPetstoreOpenApi310Client(...)` with `Petstore:BaseUrl` → `options.Server.Default.Production.BaseUrl` and `Petstore:ApiKey` → `options.ApiKey`; startup fail-fast check (§5 row 1) | — | Client construction & auth facts below |
| 4 | "Create a pet order" page: form (pet id, quantity) → `client.Store.PlaceOrder(new PlaceOrderRequest { PetId = …, Quantity = … })` → show the returned `Order` | `PlaceOrder` | Trap: body is form-url-encoded |
| 5 | "View an order" page: form (order id) → parse to `long` → `client.Store.GetOrderById(new GetOrderByIdRequest { OrderId = … })` → show `Order` fields | `GetOrderById` | `OrderId` is `required long` — parse/validate before building the record |
| 6 | Error boundary: catch `ApiException<PlaceOrderError>` / `ApiException<GetOrderByIdError>` (Case A accessors + `TryGetRawError` fallback) and `ResponseDeserializationException` / `ApiException` | both | REQUIRED READING row 1 |
| 7 | Verify: `dotnet build`, `dotnet run`, exercise both pages; optional page tests | both | dotnet-testing |

The map offers every capability the task needs (`PlaceOrder` to create, `GetOrderById` to read) — no missing-capability Blocker here. Nothing in scope needs pagination (the map's defaults table: no pagination in this SDK).

---

## 2. CONTRACT SHEET

> **Two warnings that hold for every row below.** Signatures are generated code, verbatim — each operation that takes input takes ONE request record as its first parameter (an operation with no inputs takes none), built with an object initializer whose property names are the record's own, never flat arguments. Every SDK type is written fully-qualified with the namespace its source path implies, taken from the path the map gives for THAT type, never from where a neighbouring type sits.

### Per-operation rows

| | PlaceOrder | GetOrderById |
| --- | --- | --- |
| Controller property | `client.Store` | `client.Store` |
| Method signature | `Task<Order> PlaceOrder(SwaggerPetstoreOpenApi310.Requests.Store.PlaceOrderRequest request, SwaggerPetstoreOpenApi310.Core.Request.RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)` — POST `/store/order`, Default server group | `Task<Order> GetOrderById(SwaggerPetstoreOpenApi310.Requests.Store.GetOrderByIdRequest request, SwaggerPetstoreOpenApi310.Core.Request.RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)` — GET `/store/order/{orderId}`, Default server group |
| Request record + members | `PlaceOrderRequest` (ns `SwaggerPetstoreOpenApi310.Requests.Store`): `Id: long?` · `PetId: long?` · `Quantity: int?` · `ShipDate: DateTimeOffset?` · `Status: OrderStatus?` (ns `SwaggerPetstoreOpenApi310.Models.Enums`) · `Complete: bool?` — **none `required`**; the app sets `PetId` + `Quantity` per the form and leaves the rest unset | `GetOrderByIdRequest` (ns `SwaggerPetstoreOpenApi310.Requests.Store`): `OrderId: long` — **`required`**; the form string must be parsed to `long` before the object initializer (parse failure is app-side validation) |
| Body model | No JSON body model — the request record is never serialized; the method builds `application/x-www-form-urlencoded` params `id`, `petId`, `quantity`, `shipDate`, `status`, `complete` | none (GET) |
| Response envelope + fields read | The plain `Order` record, returned directly — **no wrapper field** (`JsonResponse.Create<Order>()`). Fields read: `Id` (`id`: `long?`) · `PetId` (`petId`: `long?`) · `Quantity` (`quantity`: `int?`) · `Status` (`status`: `OrderStatus?`) · `ShipDate` (`shipDate`: `DateTimeOffset?`) · `Complete` (`complete`: `bool?`); unknown fields → `AdditionalProperties` (`[JsonExtensionData]`, ns `SwaggerPetstoreOpenApi310.Core.Models`) | same — plain `Order`, no wrapper; same fields |
| Error case / accessors / payload | **Case A** — `ApiException<SwaggerPetstoreOpenApi310.Errors.PlaceOrderError>`: `TryGetNoContent([NotNullWhen(true)] out RawError? value)` [400, 422] · `TryGetRawError([NotNullWhen(true)] out RawError? error)` [fallback]; payload `RawError` (ns `SwaggerPetstoreOpenApi310.Core.ErrorResponse`: `StatusCode`, `ReadAsBytes()`, `ReadAsString()`, `ReadAsJson<T>()`) | **Case A** — `ApiException<SwaggerPetstoreOpenApi310.Errors.GetOrderByIdError>`: `TryGetNoContent([NotNullWhen(true)] out RawError? value)` [400, 404] · `TryGetRawError([NotNullWhen(true)] out RawError? error)` [fallback]; payload `RawError` |
| Pagination | none (default) | none (default) |
| Auth | none — `AuthScheme.None` in the Api file (the page's Auth bullet is absent; a block with no Auth bullet declares no scheme) | none — `AuthScheme.None` in the Api file |
| Behavioural prose (Api/`Store.cs` `<remarks>`) | "Place a new order in the store." | "For valid response try integer IDs with value <= 5 or > 10. Other values will generate exceptions." — so an id 6..10 returns through the 400/404 `TryGetNoContent` path; surface as a normal invalid/not-found message, not a crash |
| Source | map/operations/Store.md (PlaceOrder block) · Requests/Store/PlaceOrderRequest.cs · Models/Order.cs · Models/Enums/OrderStatus.cs · Errors/PlaceOrderError.cs · Api/Store.cs | map/operations/Store.md (GetOrderById block) · Requests/Store/GetOrderByIdRequest.cs · Models/Order.cs · Errors/GetOrderByIdError.cs · Api/Store.cs |

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `none` — the two pages are independent: the `OrderId` entered on the view page is not required to come from this app's `PlaceOrder` result, and no in-scope operation enumerates the legal set of order ids (there is no list-orders operation in scope; `GetOrderById` is itself the only reader), so no caller-supplied value is checked against a set another in-scope operation defines | — | — |

### Enum value table — `OrderStatus`

`OrderStatus` (ns `SwaggerPetstoreOpenApi310.Models.Enums`) is an **open** `OpenStringEnum<OrderStatus>` — not a C# enum; the static members are the known values, and an unrecognised wire value only surfaces through `Match`'s `otherwise(string)` arm (or `TryGetKnownValue` to resolve a raw value). Source: Models/Enums/OrderStatus.cs.

| Static member | Wire value |
| --- | --- |
| `OrderStatus.Placed` | `placed` |
| `OrderStatus.Approved` | `approved` |
| `OrderStatus.Delivered` | `delivered` |

`Match(onPlaced, onApproved, onDelivered, otherwise(string))` — when rendering the view page's status, an unrecognised status lands in `otherwise` and must be shown as-is, not lost.

### Client construction, auth and server-node facts

| Fact | Value | Source |
| --- | --- | --- |
| Client class / constructor | `SwaggerPetstoreOpenApi310Client` (root ns `SwaggerPetstoreOpenApi310`); only constructor `SwaggerPetstoreOpenApi310Client(HttpClient httpClient, SwaggerPetstoreOpenApi310ClientOptions options)` | sdk-map.md *Getting a client* · SwaggerPetstoreOpenApi310Client.cs |
| DI registration | `services.AddSwaggerPetstoreOpenApi310Client(Action<SwaggerPetstoreOpenApi310ClientOptions>? configure = null)` — C# 14 `extension(IServiceCollection)` member in the root ns (registers `IHttpClientFactory` + a **singleton** client) | ServiceCollectionExtensions.cs |
| Options class | `SwaggerPetstoreOpenApi310ClientOptions` (root ns): `Environment: ServerEnvironment` (default `Production`), `Server: ServerOptions`, `PetstoreAuth: string?`, `ApiKey: string?`, `PetstoreAuthProvider` / `ApiKeyProvider: CredentialProvider<string>?`, `Retry: RetryOptions` (defaults: `MaxRetries` 3, `Delay` 1 s, `Timeout` 100 s, `UseExponentialBackoff` true, retried statuses 408/429/500/502/503/504, retried methods GET/HEAD/PUT/OPTIONS), `Logging: LoggingOptions`, `TimeProvider`, `StreamReadTimeout: TimeSpan?` (default 60 s — bounds only SSE frame waits), `Hooks` | sdk-map.md options table · SwaggerPetstoreOpenApi310ClientOptions.cs · RetryOptions.cs |
| Environments | only `ServerEnvironment.Production` exists (ns `SwaggerPetstoreOpenApi310.Servers`, value `production`, default) — no sandbox environment is declared | sdk-map.md *Servers & auth* · Servers/ServerEnvironment.cs |
| Server groups | 2 groups: `Default` → `https://petstore3.swagger.io/api/v3`, override `options.Server.Default.Production.BaseUrl` (settable `string`); `AuthServer` → `https://petstore3.swagger.io/oauth`, override `options.Server.AuthServer.Production.BaseUrl`. **Both in-scope operations resolve through the Default group** (`_server.Default(...)` in Api/Store.cs) | sdk-map.md *Servers & auth* · Servers/DefaultOptions.cs · Servers/AuthServerOptions.cs · Api/Store.cs |
| Config mapping (per task) | `Petstore:BaseUrl` → `options.Server.Default.Production.BaseUrl`; `Petstore:ApiKey` → `options.ApiKey` (the `api_key`-header credential — the config-key suffix and the options property share the name `ApiKey`) | sdk-map.md *Servers & auth* |
| Auth facts | `options.PetstoreAuth` → `Authorization` header; `options.ApiKey` → `api_key` header. Neither in-scope operation declares a scheme (`AuthScheme.None`); only `GetInventory` requires `options.ApiKey`. A credential left `null` is read from the SDK's own environment variable at construction (code always wins) | sdk-map.md *Servers & auth* · Api/Store.cs |

---

## 3. Trap notes

Hazard and what it costs — no fix here; the named skill carries the answer.

- Step 3 — The DI helper `AddSwaggerPetstoreOpenApi310Client` is a C# 14 `extension(IServiceCollection)` member in the root namespace; a wrong `LangVersion` or a missing `using SwaggerPetstoreOpenApi310;` makes the call fail to compile, and the registration silently never runs. **MUST load dotnet-client-initialization.**
- Step 3 — The client's only constructor takes an `HttpClient` plus the options object; under DI the ownership and lifetime of that `HttpClient` decides who disposes it, and getting it wrong leaks sockets or double-disposes on shutdown. **MUST load dotnet-client-initialization.**
- Step 3 — Two API-key options exist (`PetstoreAuth` → `Authorization`, `ApiKey` → `api_key`); the wrong one silently sends a header the in-scope operations ignore, and a credential left `null` falls back to the SDK's own environment variable — a secret source the deployment never set. **MUST load dotnet-authentication.**
- Step 3 — The base-URL override sits on the Default group's Production node (`Server.Default.Production.BaseUrl`); a missing/blank `Petstore:BaseUrl` silently keeps the built-in default URL instead of erroring, so the app can silently hit the shared live endpoint. **MUST load dotnet-configuration-resilience.**
- Step 4 — `PlaceOrder`'s body is `application/x-www-form-urlencoded` (the Api file builds `FormUrlEncodedRequest`), not JSON — a capture or mock that assumes JSON misreads the wire. **MUST load dotnet-calling-endpoints.**
- Steps 4–5 — Every `Order` field is nullable and `Status` is an open string enum; a 2xx can carry absent members, and an unrecognised status wire value only surfaces through `Match`'s `otherwise(string)` arm, so treating it as a known value loses data. **MUST load dotnet-models.**
- Step 6 — Both in-scope errors are Case A with one typed accessor plus the `TryGetRawError` fallback; a catch ladder that handles only the typed accessor drops the raw body of any undeclared status (e.g. 500), and `ResponseDeserializationException` (an `ApiException` that is **not** `ApiException<TError>`) escapes a ladder that catches only `ApiException<TError>`. **MUST load dotnet-error-handling.**
- Step 6 — `RetryOptions.Timeout` (default 100 s) bounds one attempt and `StreamReadTimeout` bounds only SSE frame waits — neither is the whole-call budget the caller experiences, which lives at the HttpClient level. **MUST load dotnet-configuration-resilience.**
- Step 6 — GET (`GetOrderById`) is retried by the default policy on 408/429/5xx up to `MaxRetries` = 3, while POST (`PlaceOrder`) is never in the default retried-methods set — a duplicate order can only come from the caller (double submit), never from the SDK's retry. **MUST load dotnet-configuration-resilience.**

---

## 4. REQUIRED READING

Load every skill below before implementation starts (all names as shipped by this plugin; each must be the copy that ships with this plugin):

| Skill | Governs |
| --- | --- |
| dotnet-client-initialization | Step 3 — client construction and DI registration |
| dotnet-authentication | Step 3 — `Petstore:ApiKey` → `options.ApiKey` |
| dotnet-calling-endpoints | Steps 4–5 — `PlaceOrder` / `GetOrderById` call shapes |
| dotnet-models | Steps 4–5 — `Order` fields, `OrderStatus` enum, unions/extension data |
| dotnet-error-handling | Step 6 — Case A accessors and the error boundary |
| dotnet-configuration-resilience | Steps 3, 6 — base URL, retry/timeout, logging |
| dotnet-testing | Step 7 — stubbing the SDK for page tests |

Hazard row (applies to the whole error boundary, must be handled alongside the Case A ladder above): a body that does not match its declared type — a drifted or malformed **2xx** response (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

---

## 5. PRODUCTION READINESS

| # | Concern | The decision the plan must record |
| --- | --- | --- |
| 1 | **Credential fail-fast** | Both settings bind from `IConfiguration` in `Program.cs`. **Settled:** the host throws `InvalidOperationException` at startup on a missing/blank `Petstore:BaseUrl` (checked in `Program.cs` before DI registration), so the SDK can never silently fall back to the built-in `https://petstore3.swagger.io/api/v3`. For `Petstore:ApiKey`: **no fail-fast** — neither in-scope operation requires a credential (`AuthScheme.None`), so a missing key can never surface as an `AuthSchemeException` on these two pages; the key is bound to `options.ApiKey` only when non-blank. |
| 2 | **Secret sourcing & rotation** | The secret is `Petstore:ApiKey` from `IConfiguration`, captured in the singleton client options built once at DI registration — a rotated secret does not take effect until the process restarts. No rotation-without-restart required for this dev-run app; if required later, rebuild the options (e.g. via a named client/factory) — `YOUR CALL — not in the map`. |
| 3 | **Total timeout budget** | **Settled:** `Program.cs` sets the default `HttpClient` timeout to **30 s** (per-attempt backstop — it ends the call the first time it fires) and `options.Retry.Timeout` to **10 s** (per attempt). The whole-call budget is the Razor Pages handler's `CancellationToken` (bound to the request's `RequestAborted`), passed to every SDK call. |
| 4 | **Write-retry ownership** | Default retry config kept for the retried **methods and statuses** (`HttpMethodsToRetry` GET/HEAD/PUT/OPTIONS — **POST (`PlaceOrder`) is never resent by the SDK**, so the SDK cannot duplicate the write; `GetOrderById` (GET) is retried on 408/429/500/502/503/504 up to `MaxRetries` = 3). Only `RetryOptions.Timeout` is lowered to 10 s per attempt (row 3). Any duplicate `PlaceOrder` must come from the caller (double submit) — see DUPLICATE CLAIMS / §6 Blocker 1. |
| 5 | **Idempotency & ambiguous writes** | `PlaceOrderRequest` has **no real idempotency-key member**; every non-GET call sends an `Idempotency-Key` header of a fresh `Guid.NewGuid()` (Api/Store.cs) that deduplicates nothing and must not be cited as a key. Reconciliation path: none in scope for a lost response (see §6 Blocker 2); `GetOrderById` is a read, not a write. |
| 6 | **Observability** | Decision: log page actions at Information, call failures at Error/Warning including the `RawError` body (`ReadAsString()` / `ReadAsJson<T>()` from the Case A fallback or the typed accessor); the error payloads in scope carry **no provider correlation id** to forward — log the HTTP status instead. |
| 7 | **Sensitive data** | `N/A` — the scope's fields are pet id, quantity, order id, status, ship date, complete; no card/personal data. Note (why it stays N/A): still wire `ILoggerFactory` via the SDK's DI helper so the `SWAGGER_PETSTORE_OPEN_API310_LOG` environment variable (trace forces body logging) cannot arm itself in production. |
| 8 | **Environment selection** | Server groups from the map: `Default` (`https://petstore3.swagger.io/api/v3`) and `AuthServer` (`https://petstore3.swagger.io/oauth`); only **`Default`** is touched (both operations resolve `_server.Default(...)`). Only one environment exists — `ServerEnvironment.Production` (default), and the SDK declares **no sandbox environment**. Decision: point `Petstore:BaseUrl` at a local/mock endpoint for test traffic to keep it off the live system (`YOUR CALL — not in the map` where that endpoint lives). |
| 9 | **Duplicate prevention under concurrency** | The only write (`PlaceOrder`) can be triggered twice by a double submit. **Settled:** accept (no durable claim store in this repo; single-user dev app) — a double submit may place two orders; the decision and its consequence are recorded in §6 Blocker 1 and DUPLICATE CLAIMS. |
| 10 | **Partial results** | `N/A` — no paged reads in scope; the map's defaults table states no pagination exists in this SDK, and both operations return a single `Order`. |
| 11 | **Unknown outcomes** | `PlaceOrder` is a write whose connection can fail after the provider may have acted. **Settled:** the `catch (SdkTimeoutException)` / `catch (SdkConnectionException)` in `PetstoreOrderService.PlaceOrderAsync` set `OutcomeUnknown = true` and surface "The order may have been placed, but …" — an unknown, not a definitive failure. The re-read path (via `GetOrderById` by the returned id) applies only when the response was received; a no-response failure has no id to re-read by — see UNKNOWN OUTCOMES. |

**DUPLICATE CLAIMS** — one row per write a caller can trigger twice:

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `PlaceOrder` (double form submit) | none — **settled: accept** (no claim store; the repo has no durable store and this is a single-user dev app, so a double submit may place two orders) | none — nothing rejects the second one (decision recorded in §6 Blocker 1) | n/a — there is no claim and no rejection to catch | n/a — no claim member exists; the SDK call is `PetstoreOrderService.PlaceOrderAsync` (Services/PetstoreOrderService.cs) |

**PAGED READS** — one row per read that walks pages:

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| `none` — the scope reads no paged list | — | — | — |

**UNKNOWN OUTCOMES** — one row per write whose connection can fail after the provider may already have acted:

| Write | The operation you re-read with | The reference you search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `PlaceOrder` (POST) | `GetOrderById` — only when the response was received (the returned `Order.Id` is then already shown); a **no-response** connection failure leaves no id and no in-scope operation can find the order → settled as **unknown** (decision in §6 Blocker 2) | the `Order.Id` from the `PlaceOrder` response (unavailable on a no-response failure) | `catch (SdkTimeoutException)` and `catch (SdkConnectionException)` in `PetstoreOrderService.PlaceOrderAsync` — set `OutcomeUnknown = true` and surface "The order may have been placed, but …" | `PlaceOrderAsync_ConnectionFailure_ReportsUnknownOutcome` — asserts `OutcomeUnknown` is true and exactly one POST was sent (no SDK resend) |

---

## 6. Assumptions & Blockers

**Assumptions**

- `Petstore:ApiKey` maps to the `options.ApiKey` credential (the `api_key` header): the config-key suffix and the options property share the name `ApiKey`. The other API-key credential (`PetstoreAuth`, `Authorization` header) is not what the key names. **Settled:** `Program.cs` sets `options.ApiKey` only when the config value is non-blank (a blank key is left unset so the SDK's own environment-variable fallback stays reachable); no fail-fast on the key — see row 1.
- Neither in-scope operation requires a credential — Api/Store.cs passes `AuthScheme.None` for both `PlaceOrder` and `GetOrderById` (only `GetInventory` declares `options.ApiKey`). The API key is read from configuration per the task but is **not exercised by these two pages**; it takes effect only if an auth-requiring operation is later added. **Settled:** the app binds the key when present and does not depend on it for these two pages.
- `Petstore:BaseUrl` overrides the **Default** server group's Production base URL (`options.Server.Default.Production.BaseUrl`); both in-scope operations resolve through the Default group. **Settled:** `Program.cs` fails fast at startup when the key is missing or blank (so the SDK cannot silently fall back to the built-in `https://petstore3.swagger.io/api/v3`); `appsettings.json` ships the live URL as the default so `dotnet run` works out of the box.
- The web-app shape follows the task: two pages, form POST → shows result. **Settled:** Razor Pages (`dotnet new webapp`), one page model per page (`Pages/CreateOrder` and `Pages/ViewOrder`), `[Range]` validation on the bound ids, and a friendly "No order was found for id X." message for the 400/404 path.
- `GetOrderById`'s `<remarks>` say only ids ≤ 5 or > 10 return a valid response — other values "generate exceptions"; a user-entered id in 6..10 comes back through the 400/404 `TryGetNoContent` path and the app should present it as a normal message, not a crash.

**Blockers** (the two production-readiness gaps the skill classes as §6 Blockers — nothing else stops planning)

1. **Duplicate prevention for `PlaceOrder`** (row 9 / DUPLICATE CLAIMS): **settled — accept**. The repo has no durable record store and this is a single-user dev-run app, so a double submit may place two orders; nothing rejects the second one. If this ever needs to hold at one order, a durable claim store is required first.
2. **Unknown outcome on `PlaceOrder` connection failure** (row 11 / UNKNOWN OUTCOMES): **settled — surface as unknown**. `catch (SdkTimeoutException)` / `catch (SdkConnectionException)` in `PetstoreOrderService.PlaceOrderAsync` set `OutcomeUnknown = true` and show "The order may have been placed, but …", instead of reporting a definitive failure. The re-read path (`GetOrderById` by the returned id) applies only when the response was received; a no-response failure has no id and no in-scope operation can find the order. Test: `PlaceOrderAsync_ConnectionFailure_ReportsUnknownOutcome`.

---

## 7. Source labels

Every **Source** cell above is one of: a map page (`sdk-map.md`, `map/operations/Store.md`) or a map-named source file (all paths under `src/SwaggerPetstoreOpenApi310/`), `YOUR CALL — not in the map` (an application decision the task or SDK forces, marked in place), or `UNVERIFIED` (none used — nothing in this plan waits on live traffic). No capability the map lacks was replaced by an invented data path; no clone path appears in this file.
