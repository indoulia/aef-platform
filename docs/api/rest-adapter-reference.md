# REST Adapter API Reference

Base path: `/api/v1` (configurable via `RestAdapter:BasePath`, `appsettings.json`).

## `POST /api/v1/execution`

Request body:

```json
{ "sessionId": null, "userRequest": "start a new feature", "projectProfileId": "default-profile", "payload": null }
```

Response: `Runtime.Agent.ExecutionResponse` (camelCase JSON), `200 OK` for `Completed`/`NeedsClarification`/most `Failed` outcomes, `404 Not Found` when `sessionId` names an unknown session, `400 Bad Request` for empty `userRequest`/`projectProfileId` or malformed JSON, `415 Unsupported Media Type` for a non-JSON `Content-Type`, `403 Forbidden` if the security seam denies the request.

## `GET /api/v1/capabilities`

Returns the full `CapabilityDescriptor[]` catalog. Always `200 OK`.

## `POST /api/v1/sessions`

Request body: `{ "projectProfileId": null }`. Returns a new `Runtime.Agent.ExecutionSession`, `201 Created`.

## `GET /api/v1/health`

Returns `{ "status": "Healthy" }` unconditionally, `200 OK`. Real health-contributor logic is Capability 4's chartered scope — this is an honest placeholder, not yet a live signal.

## `GET /api/v1/version`

Returns `{ "version": "<assembly informational version>" }`, `200 OK`.

## OpenAPI

`GET /swagger/v1/swagger.json` — machine-readable OpenAPI document. `GET /swagger` — interactive Swagger UI.

## Security

Every `/api/v1/execution` call passes through `ISecurityEvaluator` before `Runtime.Agent` is invoked. The trusted-default implementation authorizes every request — no production authentication exists yet (Non-Goal for this Capability).

## Configuration

`appsettings.json`'s `RestAdapter` section: `Port` (default `5080`), `BasePath` (default `/api/v1`), `MaxRequestBodyBytes` (default `1048576`).

## Deployment notes

Kestrel binds `http://localhost:{Port}` by default — no HTTPS or authentication is configured beyond ASP.NET Core's own framework defaults; both are explicit Non-Goals for this Capability and are deployment decisions for a later stage.
