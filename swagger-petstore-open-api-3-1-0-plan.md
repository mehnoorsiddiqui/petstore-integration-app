# SwaggerPetstoreOpenApi310 1.0.1 → 1.0.2 upgrade plan

Task scope: keep the already-updated `SwaggerPetstoreOpenApi310` 1.0.2 package reference and all other dependency versions unchanged. Audit and, only if needed, update the existing SDK call sites and affected tests — the Create/View pages and the JSON API (`POST /api/orders`, `GET /api/orders/{orderId}`) merged from `main` (PR #2). The 1.0.2 contract below is the source of truth.

Before the JSON API was merged, the solution built and its six tests passed against 1.0.2 with no source edits. After merging `main`, 1.0.2 breaks exactly one app line: `Order.Quantity` (and `PlaceOrderRequest.Quantity`) is `long?` in 1.0.2 (`int?` in 1.0.1), so `OrderDto.Quantity` in `Endpoints/OrdersApi.cs` is widened to `long?` — the JSON field and its values are unchanged. No other operation, request, model, error, client-construction, or retry API mismatch requires a source edit. The implementation pass should preserve the current call sites unless a fresh compiler/test result identifies one; do not make cosmetic SDK changes.

## 1. Scope & sequence

| # | Step | Existing SDK usages | Planned action |
| --- | --- | --- | --- |
| 1 | Audit | `Program.cs`; `Services/PetstoreOrderService.cs`; `Services/PetstoreOrderJsonBodyHandler.cs`; `Endpoints/OrdersApi.cs`; `Pages/CreateOrder.cshtml.cs`; `Pages/ViewOrder.cshtml.cs`; `tests/PetstoreWeb.Tests/PetstoreOrderServiceTests.cs`; `tests/PetstoreWeb.Tests/OrdersApiTests.cs` | Keep the package version at 1.0.2; compare each usage with the contract sheet. |
| 2 | Client/configuration | `AddSwaggerPetstoreOpenApi310Client`, `RetryOptions.Default() with { Timeout = 10s }`, `Server.Default.Production.BaseUrl`, default `HttpClient` timeout + `PetstoreOrderJsonBodyHandler` on that client | Preserve the existing DI registration and configuration. Change only if 1.0.2 compilation identifies a concrete option/member difference. |
| 3 | Order creation | `client.Store.PlaceOrder(new PlaceOrderRequest { PetId, Quantity }, cancellationToken: ct)` — reached from the Create page and `POST /api/orders` | Preserve the request-record call and its result/error behavior; update only an actual 1.0.2 contract mismatch. 1.0.2 still sends the body form URL encoded, so keep `PetstoreOrderJsonBodyHandler` (§3). |
| 4 | Order lookup | `client.Store.GetOrderById(new GetOrderByIdRequest { OrderId }, cancellationToken: ct)` — reached from the View page and `GET /api/orders/{orderId}` | Preserve the request-record call, `Order` rendering, and friendly 400/404 handling. |
| 5 | Error/model handling | `ApiException<PlaceOrderError>`, `ApiException<GetOrderByIdError>`, `ResponseDeserializationException`, SDK transport exceptions, `OrderStatus.Match` | Preserve typed Case-A handling, raw fallback, unknown-outcome handling, and open-enum display. |
| 6 | JSON API | `Order` → `OrderDto` mapping (`Id`, `PetId`, `Quantity`, `Status.Value`, `ShipDate`, `Complete`) and `PetstoreOrderResult.StatusCode` in `Endpoints/OrdersApi.cs` | Preserve the response shape and status mapping (§2 application-layer notes). **Changed for 1.0.2:** `OrderDto.Quantity` `int?` → `long?`. |
| 7 | Tests | `PetstoreOrderServiceTests.cs` SDK client construction and service calls; `OrdersApiTests.cs` (`WebApplicationFactory` + fake handler) | Change tests only if an SDK API used by a test changes; retain all service and API behavioral tests. |
| 8 | Verification | `PetstoreWeb.slnx` | Run `dotnet build PetstoreWeb.slnx` and `dotnet test PetstoreWeb.slnx`. |

No in-scope capability is missing from the SDK. No `GetInventory`, `DeleteOrder`, `PetApi`, or `UserApi` usage exists in this repository.

## 2. CONTRACT SHEET

Signatures are generated code, verbatim — each operation that takes input takes ONE request record as its first parameter (an operation with no inputs takes none), built with an object initializer whose property names are the record's own, never flat arguments.

Every SDK type is written fully-qualified with the namespace its source path implies, taken from the path the map gives for THAT type, never from where a neighbouring type sits.

### Per-operation contracts

| Operation / controller | Method signature | Request record + members | Body model + fields | Response envelope + fields the app reads | Error case, accessors, payload | Pagination | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `client.Store.PlaceOrder` | `Task<SwaggerPetstoreOpenApi310.Models.Order> PlaceOrder(SwaggerPetstoreOpenApi310.Requests.Store.PlaceOrderRequest request, SwaggerPetstoreOpenApi310.Core.RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)`; POST `/store/order`; Default server group | `SwaggerPetstoreOpenApi310.Requests.Store.PlaceOrderRequest`: `Id: long?`, `PetId: long?`, `Quantity: long?` (was `int?` in 1.0.1), `ShipDate: DateTimeOffset?`, `Status: SwaggerPetstoreOpenApi310.Models.Enums.OrderStatus?`, `Complete: bool?`; none required. The app supplies `PetId` and `Quantity`. | No JSON body model. The generated request is form URL encoded with `id`, `petId`, `quantity`, `shipDate`, `status`, `complete`; the request record is not serialized as JSON. `petstore3.swagger.io` rejects this form body with HTTP 500; the app re-encodes it as JSON in `PetstoreOrderJsonBodyHandler` (§3). | Direct `SwaggerPetstoreOpenApi310.Models.Order`, no wrapper: `Id (id): long?`, `PetId (petId): long?`, `Quantity (quantity): long?` (was `int?` in 1.0.1), `ShipDate (shipDate): DateTimeOffset?`, `Status (status): SwaggerPetstoreOpenApi310.Models.Enums.OrderStatus?`, `Complete (complete): bool?`; unknown fields use `AdditionalProperties`. | Case A: `ApiException<SwaggerPetstoreOpenApi310.Errors.PlaceOrderError>`. `TryGetNoContent(out SwaggerPetstoreOpenApi310.Core.ErrorResponse.RawError? value)` covers 400/422; `TryGetRawError(out ...? error)` is the fallback. `RawError` exposes `StatusCode`, `ReadAsBytes()`, `ReadAsString()`, and `ReadAsJson<T>()`. | None; SDK defaults to no pagination. | `map/operations/Store.md`; `src/SwaggerPetstoreOpenApi310/Api/Store.cs`; request/model/enum/error files named below |
| `client.Store.GetOrderById` | `Task<SwaggerPetstoreOpenApi310.Models.Order> GetOrderById(SwaggerPetstoreOpenApi310.Requests.Store.GetOrderByIdRequest request, SwaggerPetstoreOpenApi310.Core.RequestOptions? requestOptions = null, CancellationToken cancellationToken = default)`; GET `/store/order/{orderId}`; Default server group | `SwaggerPetstoreOpenApi310.Requests.Store.GetOrderByIdRequest`: `OrderId: long`, required. | None; GET request. | Direct `SwaggerPetstoreOpenApi310.Models.Order`, with the same six nullable fields and JSON names as `PlaceOrder`; the pages and the JSON API read all six. | Case A: `ApiException<SwaggerPetstoreOpenApi310.Errors.GetOrderByIdError>`. `TryGetNoContent(out SwaggerPetstoreOpenApi310.Core.ErrorResponse.RawError? value)` covers 400/404; `TryGetRawError(out ...? error)` is the fallback. | None; SDK defaults to no pagination. | `map/operations/Store.md`; `src/SwaggerPetstoreOpenApi310/Api/Store.cs`; `src/SwaggerPetstoreOpenApi310/Requests/Store/GetOrderByIdRequest.cs`; `src/SwaggerPetstoreOpenApi310/Models/Order.cs`; `src/SwaggerPetstoreOpenApi310/Errors/GetOrderByIdError.cs` |

`RequestOptions` is in `SwaggerPetstoreOpenApi310.Core` in the 1.0.2 source, not `SwaggerPetstoreOpenApi310.Core.Request`; the application does not pass one explicitly.

### CROSS-OPERATION INVARIANTS

| Invariant | Operations | Enforced where |
| --- | --- | --- |
| `none`: the app accepts a user-entered order id, and no in-scope operation enumerates the legal order-id set; `PlaceOrder` returns one order but does not define a caller-supplied set for `GetOrderById` | — | — |

### Enum values actually used

`SwaggerPetstoreOpenApi310.Models.Enums.OrderStatus` is an open `OpenStringEnum<OrderStatus>`, not a C# enum. Unknown wire values must remain visible through the `otherwise(string)` arm used by both page models.

| Static member | Wire value |
| --- | --- |
| `OrderStatus.Placed` | `placed` |
| `OrderStatus.Approved` | `approved` |
| `OrderStatus.Delivered` | `delivered` |

Source: `src/SwaggerPetstoreOpenApi310/Models/Enums/OrderStatus.cs`.

### Client construction, auth, servers, and resilience

| Fact | 1.0.2 contract / application decision | Source |
| --- | --- | --- |
| Client | `SwaggerPetstoreOpenApi310.SwaggerPetstoreOpenApi310Client(HttpClient, SwaggerPetstoreOpenApi310ClientOptions)`; DI uses `AddSwaggerPetstoreOpenApi310Client(...)` and registers a long-lived client. | `sdk-map.md`; `src/SwaggerPetstoreOpenApi310/SwaggerPetstoreOpenApi310Client.cs`; `src/SwaggerPetstoreOpenApi310/ServiceCollectionExtensions.cs` |
| Options used by the app | `Server`, `Retry`, `ApiKey`; `RetryOptions.Default()` supplies the generated required members, then the app sets `Timeout = 10s`. | `sdk-map.md`; `src/SwaggerPetstoreOpenApi310/SwaggerPetstoreOpenApi310ClientOptions.cs`; `src/SwaggerPetstoreOpenApi310/Core/Configuration/RetryOptions.cs` |
| Server selection | Only `ServerEnvironment.Production` exists. `Default` is `https://petstore3.swagger.io/api/v3`, overridden by `options.Server.Default.Production.BaseUrl`; `AuthServer` is unused. Both operations resolve through `Default`. | `sdk-map.md`; `src/SwaggerPetstoreOpenApi310/Servers/ServerEnvironment.cs`; `src/SwaggerPetstoreOpenApi310/Api/Store.cs` |
| Auth | Both in-scope operations use `AuthScheme.None`; neither requires `PetstoreAuth` or `ApiKey`. If configured, `options.ApiKey` is the `api_key` header credential. | `sdk-map.md`; `src/SwaggerPetstoreOpenApi310/Api/Store.cs` |
| Config binding | `Petstore:BaseUrl` is required by the host and mapped to `Server.Default.Production.BaseUrl`; `Petstore:ApiKey` is optional and mapped to `options.ApiKey`. | `YOUR CALL — not in the map` for application binding; SDK targets from `sdk-map.md` |

### Application-layer notes — JSON API scope addition (not SDK rows)

These are app-design facts the API forces; they are **not** SDK contract rows. Source labels: the API field names, the mapping decisions and the error-status choices are `YOUR CALL — not in the map`; the SDK sources named in the table come from Models/Order.cs.

**API response shape — SDK `Order` → API JSON:**

| API JSON field | SDK source | Note |
| --- | --- | --- |
| `orderId` | `Order.Id` (`long?`) | API renames the field: the SDK wire name is `id` (`[JsonPropertyName("id")]`) — the API deliberately exposes `orderId` |
| `petId` | `Order.PetId` (`long?`) | SDK wire name `petId` — no rename |
| `quantity` | `Order.Quantity` (`long?` in 1.0.2) | SDK wire name `quantity` — no rename; `OrderDto.Quantity` is `long?` to match |
| `status` | `Order.Status` (`OrderStatus?`) — raw wire value | `OrderStatus` is an open `OpenStringEnum<OrderStatus>`; `.Value` yields the raw wire string (`placed`/`approved`/`delivered` or an unrecognised value — the `otherwise(Value)` arm of `Match` in Models/Enums/OrderStatus.cs), so unknown statuses pass through unchanged, same behaviour the view page already gets |
| `shipDate` | `Order.ShipDate` (`DateTimeOffset?`) | serialized as ISO-8601 by System.Text.Json |
| `complete` | `Order.Complete` (`bool?`) | SDK wire name `complete` — no rename |

**Service result type extension (forced by the API):** `PetstoreOrderResult` gains a `StatusCode: HttpStatusCode?` member so the API can return the provider's status. Before the API, the result only carried `Order` / `Error` / `OutcomeUnknown` — sufficient for the pages, insufficient for API status mapping. The pages keep using `Order`/`Error`/`OutcomeUnknown` and are unaffected. `YOUR CALL — not in the map`.

**API error-to-HTTP-status mapping (app design — `YOUR CALL — not in the map`):**

| Result / failure | HTTP status | Response body |
| --- | --- | --- |
| Success | `POST`: 201 Created (+ `Location: /api/orders/{id}` when the provider returns a positive id); `GET`: 200 | the mapped order JSON (above) |
| Caller-fault provider 4xx — only 400/404/409/422 | pass through the provider's status; `GetOrderById`'s 400 ("invalid id") is returned as 404 | JSON `{"error": "..."}` |
| Any other provider 4xx (e.g. 401/403 — our credentials, not the caller's) | 502 Bad Gateway | JSON `{"error": "..."}` |
| Provider 5xx | 502 Bad Gateway | JSON `{"error": "..."}` |
| Connection failure / timeout, or an unreadable 2xx response, on the write (`OutcomeUnknown = true`) | 502 — **settled** (one status; the `error` message distinguishes timeout vs connection failure) | JSON body stating the outcome is unknown — reuses the settled unknown-outcome handling in `PetstoreOrderService.PlaceOrderAsync` (§6 Blocker 2) |
| Input validation failure (malformed/non-JSON body; petId/quantity missing or non-positive; orderId non-numeric or non-positive) | 400 | JSON `{"error": "..."}` |

## 3. Trap notes

- Step 2 — A generated operation takes one request record, and the explicit `RequestOptions` type is in the root `Core` namespace; using flat arguments or a neighboring namespace causes a compile failure. **MUST load dotnet-calling-endpoints.**
- Step 2 — `AddSwaggerPetstoreOpenApi310Client` owns a long-lived SDK client and the supplied `HttpClient`; an incorrect registration or lifetime can change disposal and connection behavior. **MUST load dotnet-client-initialization.**
- Step 2 — The two API-key properties target different headers, and an unset credential may be read from the SDK environment at client construction; binding the wrong setting changes authentication silently. **MUST load dotnet-authentication.**
- Step 2 — `RetryOptions.Timeout` bounds one attempt, while the `HttpClient` timeout and request cancellation bound different portions of the caller-visible operation. **MUST load dotnet-configuration-resilience.**
- Steps 3–4 — `PlaceOrder` is form URL encoded, while `GetOrderById` has a required request-record member; a JSON/mock assumption or omitted member makes the call contract wrong. **MUST load dotnet-calling-endpoints.**
  - **Verified live:** `petstore3.swagger.io` answers that form body with HTTP 500 while accepting the same order as JSON. **Settled:** `Services/PetstoreOrderJsonBodyHandler.cs`, a `DelegatingHandler` on the SDK's default `HttpClient` (`Program.cs`), re-encodes only `POST …/store/order` form bodies as JSON (`id`/`petId`/`quantity` as numbers, `complete` as bool). Still required in 1.0.2 (re-checked: 1.0.2 still sends `application/x-www-form-urlencoded`). Remove it once the SDK sends JSON. Covered by `OrdersApiTests.Post_SendsOrderToPetstoreAsJson`.
- Steps 3–5 — `Order` fields are nullable and `OrderStatus` is open; treating the status as a closed C# enum can discard an unknown provider value. **MUST load dotnet-models.**
- Step 5 — Typed Case-A catches do not cover malformed success/error bodies or transport/configuration exceptions; a narrow catch ladder can lose the status/body or let the exception escape. **MUST load dotnet-error-handling.**
- Step 6 — SDK test clients must use the `HttpClient`/fake-handler seam and set any credentials required by the operation; captured request content must be read inside the handler. **MUST load dotnet-testing.**

## 4. REQUIRED READING

Load these plugin-qualified skills before implementation starts:

| Skill | Governs |
| --- | --- |
| `dotnet-integrate-swagger-petstore-open-api-3-1-0` | Plan-first workflow and map-grounded SDK integration |
| `dotnet-getting-started` | SDK map, namespaces, operation/source lookup, servers, auth, idempotency |
| `dotnet-client-initialization` | Client construction, DI, `HttpClient` ownership and lifetime |
| `dotnet-authentication` | `ApiKey`/`PetstoreAuth` binding and missing-secret behavior |
| `dotnet-calling-endpoints` | Request records, operation arguments, cancellation, responses |
| `dotnet-models` | `Order`, nullable fields, open `OrderStatus`, unknown values |
| `dotnet-error-handling` | Typed errors, raw fallback, SDK exception boundary |
| `dotnet-configuration-resilience` | Retry, timeout, server selection, logging and hooks |
| `dotnet-testing` | Fake `HttpClient` handlers, error/retry tests, outgoing requests, `WebApplicationFactory`-based JSON API tests |

Hazard row: a body that does not match its declared type — a drifted or malformed **2xx** response (a missing `required` member) or a **non-2xx** body that does not match its operation's generated `{Operation}Error` shape — surfaces as `ResponseDeserializationException`, an `ApiException` that keeps the HTTP status and names the target type but is **not** an `ApiException<TError>`; a catch ladder that handles only `ApiException<TError>` lets it escape, so it must also catch `ResponseDeserializationException` (or `ApiException`).

## 5. PRODUCTION READINESS

| # | Concern | Decision | Source |
| --- | --- | --- | --- |
| 1 | Credential fail-fast | `Petstore:BaseUrl` fails fast in `Program.cs` when missing/blank so the SDK default live URL is not selected accidentally. No SDK credential fail-fast is needed for these operations because both are anonymous; `Petstore:ApiKey` remains optional. | `YOUR CALL — not in the map`; `sdk-map.md`; `src/SwaggerPetstoreOpenApi310/Api/Store.cs` |
| 2 | Secret sourcing & rotation | Read `Petstore:ApiKey` from `IConfiguration`; the DI helper captures options once in its singleton. Rotation takes effect after process restart; no live rotation is required for this app. | `YOUR CALL — not in the map` |
| 3 | Total timeout budget | Preserve `RetryOptions.Timeout = 10s` per attempt and default `HttpClient.Timeout = 30s`; the request cancellation token remains the caller's whole-request cancellation boundary. | `YOUR CALL — not in the map`; `sdk-map.md` |
| 4 | Write-retry ownership | `PlaceOrder` is POST and is outside the default retried methods; `GetOrderById` is GET and may retry the generated retryable statuses. Preserve the current retry settings. | `src/SwaggerPetstoreOpenApi310/Api/Store.cs`; `sdk-map.md` |
| 5 | Idempotency & ambiguous writes | `PlaceOrderRequest` has no real idempotency-key member. The generated `Idempotency-Key` header is not a deduplication key. Preserve the current unknown-outcome behavior when a response is not received. | `src/SwaggerPetstoreOpenApi310/Requests/Store/PlaceOrderRequest.cs`; `src/SwaggerPetstoreOpenApi310/Api/Store.cs`; `YOUR CALL — not in the map` |
| 6 | Observability | Preserve service error logging with HTTP status and raw error text. The in-scope typed error payload is `RawError` and exposes no provider correlation-id member; do not invent one. The JSON API's error body is only `{"error": "..."}` and must never leak SDK exception details (type names, stack traces, provider bodies); those go to the log only. | `src/SwaggerPetstoreOpenApi310/Errors/PlaceOrderError.cs`; `src/SwaggerPetstoreOpenApi310/Errors/GetOrderByIdError.cs`; `sdk-map.md` |
| 7 | Sensitive data | The order payload has no payment or personal-data field, but `Petstore:ApiKey` is secret. Do not log the credential or enable SDK request-body logging in production; use an explicit logger factory if SDK logging is enabled. | `src/SwaggerPetstoreOpenApi310/Models/Order.cs`; `sdk-map.md`; `YOUR CALL — not in the map` |
| 8 | Environment selection | Only Production is declared; there is no SDK sandbox. Both operations use `Default`. Test traffic is isolated by the existing fake `HttpClient` handlers; deployed non-live traffic must set `Petstore:BaseUrl` to its mock/local endpoint. | `sdk-map.md`; `src/SwaggerPetstoreOpenApi310/Servers/ServerEnvironment.cs`; `YOUR CALL — not in the map` |
| 9 | Duplicate prevention under concurrency | No durable claim store exists in this repository. Preserve the current behavior and record duplicate `PlaceOrder` submission — a double form submit or a repeated `POST /api/orders` — as a known production limitation, not an SDK migration change. | `YOUR CALL — not in the map` |
| 10 | Partial results | N/A: neither in-scope operation is paged; both return one `Order`. | `sdk-map.md`; `map/operations/Store.md` |
| 11 | Unknown outcomes | A connection/timeout after `PlaceOrder` may leave the provider outcome unknown. Preserve the service's `OutcomeUnknown` result (also set for an unreadable 2xx response) rather than reporting a definitive failure; no order id is available for reconciliation when no response arrives. `POST /api/orders` surfaces `OutcomeUnknown` as HTTP 502 with an unknown-outcome JSON body. | `YOUR CALL — not in the map`; `src/SwaggerPetstoreOpenApi310/Api/Store.cs` |

### DUPLICATE CLAIMS

| Write | Where the claim is stored | What rejects the second one | Where that rejection is caught | Where in the code |
| --- | --- | --- | --- | --- |
| `PlaceOrder` (double form submit or repeated `POST /api/orders`) | none; repository has no durable claim store | none; duplicate submits are accepted | n/a | n/a; `PetstoreOrderService.PlaceOrderAsync` performs the SDK call without a claim, reached from the Create page model and `POST /api/orders` |

### PAGED READS

| Read | What caps it | How the caller learns the answer was cut short | Where in the code |
| --- | --- | --- | --- |
| none | — | — | — |

### UNKNOWN OUTCOMES

| Write | The operation to re-read with | The reference to search by | Where in the code | The test that fails the connection |
| --- | --- | --- | --- | --- |
| `PlaceOrder` | `GetOrderById` only when a response supplies an order id; no re-read is possible after a no-response failure | returned `Order.Id`, unavailable on a transport failure | `PetstoreOrderService.PlaceOrderAsync` catches `SdkTimeoutException`/`SdkConnectionException` (and `ResponseDeserializationException` on a 2xx) and returns `OutcomeUnknown = true`; `POST /api/orders` maps it to HTTP 502 | `PetstoreOrderServiceTests.PlaceOrderAsync_ConnectionFailure_ReportsUnknownOutcome` asserts the unknown result and one POST; `PlaceOrderAsync_UnreadableSuccessBody_ReportsUnknownOutcome` |

## 6. Assumptions & Blockers

### Assumptions

- The package reference in `PetstoreWeb.csproj` is intentionally 1.0.2 and must not be edited; no other dependency version is in scope.
- The only application SDK operations are `Store.PlaceOrder` and `Store.GetOrderById` (used by both the pages and the JSON API); all other SDK symbols are configuration, models, exceptions, or test construction.
- The 1.0.2 contract's only breaking change for these usages is `Order.Quantity`/`PlaceOrderRequest.Quantity` widening to `long?`; it affects only `OrderDto.Quantity` (now `long?`). The pages and service compile unchanged (`int` converts implicitly to `long?`). If a later clean build differs, resolve that symbol against the contract sheet and change only the affected call site/test.
- `Petstore:ApiKey` is retained for configuration compatibility even though neither operation declares an auth scheme.

- **JSON API scope addition** — new assumptions the API forces, all `YOUR CALL — not in the map`:
  - **Field naming:** the API exposes `orderId` while the SDK's wire name for `Order.Id` is `id` — the API deliberately renames the field per the task's required response shape; the SDK call and the `Order` model are untouched.
  - **Status as raw wire string:** `status` is returned as the raw wire value from the open `OrderStatus` enum's `.Value` — known values (`placed`/`approved`/`delivered`) and unknown values pass through unchanged; the app invents no status vocabulary.
  - **HTTP status choices:** 201 on create / 200 on read; provider 400/404/409/422 passed through (other provider 4xx → 502); provider 5xx → 502; write connection failure/timeout or an unreadable 2xx response (`OutcomeUnknown`) → 502 with an unknown-outcome JSON body; input validation failure → 400 (recorded in the §2 API error mapping).
  - **API error body shape:** every non-2xx API response is `{"error": "..."}` and never leaks SDK exception details (row 6).
  - The two settled blockers are unchanged by the API: duplicate acceptance and unknown-outcome surfacing cover the API POST path as a second caller route to the same write (rows 9/11, DUPLICATE CLAIMS, UNKNOWN OUTCOMES).

### Blockers / known production gaps

- No durable duplicate claim store exists, so the plan cannot guarantee at-most-once `PlaceOrder` under concurrent/double submission. This is outside the dependency upgrade and must not be “fixed” by treating the SDK's generated `Idempotency-Key` header as a real key.
- A lost `PlaceOrder` response has no returned order id and no in-scope search operation, so the application can only surface an unknown outcome; it cannot reconcile that case during this upgrade.

## 7. Source labels

SDK contract rows cite `sdk-map.md`, `map/operations/Store.md`, or the exact map-named source file. Application choices are marked `YOUR CALL — not in the map`; no live-traffic fact is left as `UNVERIFIED`. No project file, package reference, or dependency version is changed by this plan refresh; the only code change the 1.0.2 upgrade requires after merging the JSON API is `OrderDto.Quantity` → `long?`.
