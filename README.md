# HrAuth API — Clean Architecture (.NET 9)

An ASP.NET Core Web API that wraps the HaramAPI **HrAuth** endpoints
(`https://hrapi.qm.edu.sa`) — `login`, `refresh-token`, and `validate-token` —
behind its own contract, structured with Clean Architecture.

## Solution layout

```
HrAuth.sln
src/
  HrAuth.Domain          — entities (AuthSession, AuthenticatedUser). No dependencies.
  HrAuth.Application     — use cases, DTOs, ports (IHrAuthProvider). Depends on Domain.
  HrAuth.Infrastructure  — HrAuth REST client implementation. Depends on Application.
  HrAuth.WebApi          — controllers, DI wiring, Swagger. Depends on Application + Infrastructure.
```

**Dependency rule:** dependencies point inward. Domain knows nothing of the
others; Infrastructure implements an interface (`IHrAuthProvider`) *declared*
in Application, so the HTTP/transport detail is swappable.

## Endpoints

| Method | Route                     | Purpose                                  |
|--------|---------------------------|------------------------------------------|
| POST   | `/api/auth/login`         | Authenticate, return JWT + refresh token |
| POST   | `/api/auth/refresh-token` | Exchange a token pair for a fresh one    |
| POST   | `/api/auth/validate-token`| Validate the `Authorization: Bearer` token |

## Run

```bash
dotnet build
dotnet run --project src/HrAuth.WebApi
```

Swagger UI is served at the app root (e.g. `http://localhost:5284/`) in the
Development environment. `src/HrAuth.WebApi/HrAuth.WebApi.http` has ready-made
requests.

## Configuration

`src/HrAuth.WebApi/appsettings.json` → `HrAuth` section:

| Key                         | Default                     | Notes                                            |
|-----------------------------|-----------------------------|--------------------------------------------------|
| `BaseUrl`                   | `https://hrapi.qm.edu.sa`   | HR identity API base URL                         |
| `GatewayToken`              | *(empty)*                   | Optional — enables secure-client login mode      |
| `AcceptUntrustedCertificate`| `true`                      | Accepts the legacy server's self-signed cert     |
| `TimeoutSeconds`            | `30`                        | HTTP request timeout                             |

> **Security note:** no secret is hard-coded. If you set `GatewayToken`, keep it
> out of source control — supply it via environment variable
> (`HrAuth__GatewayToken`) or user-secrets, not `appsettings.json`.

## Notes

- The HrAuth `data` payload is untyped in the API spec, so
  `HrAuthSessionParser` reads fields defensively, tolerating both `snake_case`
  and `camelCase` keys (`token`/`access_token`, `refresh_token`/`refreshToken`, …).
- Transport failures never cross a layer boundary as exceptions — they are
  mapped to `Result<T>.Failure(...)`.
