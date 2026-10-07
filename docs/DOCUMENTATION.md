# Akster (PetPal) — Documentation

> Part 1 is the technical documentation, Part 2 is a short overview of the app, and Part 3 is a lighter functional documentation.
> Section 1.6 (Azure) is based on the real `az resource list` output; a short list of unconfirmed details is at its end.

---

# Part 1 — Technical Documentation

## 1.1 System overview

```
 Browser (Angular SPA, static files)
        │  HTTPS, JSON + multipart, Authorization: Bearer <JWT>
        ▼
 Azure App Service "akster-frontend"  (nginx container)          ← serves static SPA
        │
        │  (browser calls the API directly, not via the frontend server)
        ▼
 Azure App Service "akster-backend"   (ASP.NET Core 10 container)
        │            │                         │
        │            │                         └─► Azure Blob Storage (avatars, pet pictures)
        │            └─► Azure Key Vault "akster-vault" (JWT signing key, config secrets)
        ▼
 SQL Server database (Azure SQL / SQL Server, EF Core migrations)

 GitHub Actions ──► build/test ──► push images to Azure Container Registry ──► deploy to both App Services
 Third party: Google Maps JavaScript API (address picker, called from the browser)
```

Repository layout:

| Path | Content |
|---|---|
| `backend/PetSitting.Api` | ASP.NET Core host: `Program.cs`, thin controllers, request contracts |
| `backend/PetSitting.Application` | Use cases (vertical slices), MediatR behaviors, abstractions (interfaces) |
| `backend/PetSitting.Domain` | Entities, enums, `Result` type |
| `backend/PetSitting.Infrastructure` | EF Core `AppDbContext`, repositories, migrations, JWT and Blob services |
| `frontend/` | Angular 21 app (Nx-wrapped), nginx config, Dockerfile |
| `.github/workflows/deploy.yaml` | CI/CD pipeline |
| `docker-compose.yml` | Local stack (SQL Server + API + frontend) |
| `.claude/`, `.agents/`, `skills-lock.json` | AI-assistant skills/config, not part of the product |
| `backend/{Identity,Pets,Availability,Controllers,Models,Program.cs,DbContext.cs,backend.csproj}` | **Legacy** pre-refactor service-style backend, superseded by `PetSitting.*` (see 1.9) |

## 1.2 Architecture decisions (from `backend/AGENTS.md` and `frontend/AGENTS.md`, the stated source of truth)

**Backend:** Vertical Slice architecture for the Application layer, tactical DDD in the Domain, CQRS through MediatR, repositories only for aggregate roots (no generic `IRepository<T>`), `Result<T>` for expected failures. Rejected alternatives: iDesign, generic Repository/Unit-of-Work, classic N-tier.
**Frontend:** single Angular app (no monorepo apps), feature-folder structure, standalone components, Signals instead of NgRx, one Facade per feature, smart/dumb component split.

> Note: the guides describe the *target* architecture. The code currently follows the backend guide closely; the frontend is still flatter than the guide (no `features/` folders or facades yet, see 1.9).

## 1.3 Backend

### Technologies

| Technology | Version | Why it is used | Purpose in this app |
|---|---|---|---|
| .NET / C# | 10 (`net10.0`) | Current LTS-line runtime, strong typing, good Azure integration | Whole backend |
| ASP.NET Core Web API (controllers) | 10 | Mature, simple routing and model binding | HTTP API under `/api/*` |
| ASP.NET Core OpenAPI | 10.0.2 | Built-in API description, no Swashbuckle needed | `MapOpenApi()`, Development only |
| Entity Framework Core + SqlServer provider | 10.0.3 | Code-first model, migrations, LINQ | Persistence, schema evolution |
| EF Core Design | 10.0.3 | Tooling for `dotnet ef migrations` | Dev-time only (`PrivateAssets=all`) |
| MediatR | 12.4.0 (Api/Application) | Decouples controllers from handlers, supports pipeline behaviors | CQRS: every use case is a `Command`/`Query` + `Handler` |
| FluentValidation (+ DI extensions) | 11.x | Declarative, testable validation separated from handlers | One `*Validator` per command, run by `ValidationBehavior` |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.8 | Standard bearer-token auth | Validates JWTs (issuer, audience, lifetime, signing key) |
| System.IdentityModel.Tokens.Jwt | 8.9.0 | JWT creation | `JwtTokenService` issues access tokens |
| Azure.Identity | 1.21.0 | `DefaultAzureCredential`: managed identity in Azure, developer login locally, no secrets in code | Authenticates to Key Vault |
| Azure.Security.KeyVault.Secrets | 4.9.0 | Read/write secrets | Loads (or generates) `Jwt--Key` |
| Azure.Extensions.AspNetCore.Configuration.Secrets | 1.5.0 | Key Vault as a configuration provider | Vault secrets appear as normal config (e.g. connection string, blob settings) |
| Azure.Storage.Blobs | 12.28.0 | Official blob SDK | Stores avatars and pet pictures |

### Layering and dependency direction

`Api → Application → Domain` and `Infrastructure → Application, Domain`. The Application layer only knows interfaces (`IUserRepository`, `IPetRepository`, `ISitterAvailabilityRepository`, `IJwtTokenService`, `IBlobService`); Infrastructure implements them; `Program.cs` wires them in DI (repositories scoped, `JwtTokenService` singleton, `BlobService` scoped).

### Request flow

`Controller → IMediator.Send(command) → ValidationBehavior (FluentValidation) → Handler → Repository → EF Core → SQL Server`
- Controllers are thin; `ApiControllerBase` provides `UserId` (from the `NameIdentifier` claim), `Respond()` (200 / 404 for "not found" messages / 400) and `ToUpload()`.
- A `ValidationException` is converted by the global exception handler in `Program.cs` into `400 { success:false, message }`; any other exception becomes `500 { success:false, message:"An unexpected error occurred." }`.
- Handlers return response records (`AuthResponse`, `PetResponse`, `AvailabilityResponse`) carrying `Success`/`Message`. A `Result<T>` type exists in the Domain but handlers currently use these response records.

### Startup behaviour (`Program.cs`)

1. Registers controllers, OpenAPI, CORS (default policy), `AppDbContext` (connection string `ConnectionStrings:DefaultConnection`), repositories, services, MediatR (+ `ValidationBehavior`), validators.
2. **JWT key bootstrap:** if `Jwt:Key` is not configured, read secret `Jwt--Key` from Key Vault (`KeyVaultUri`, default `https://akster-vault.vault.azure.net/`); if missing, generate 64 random bytes, store them in Key Vault and use them. If Key Vault is unreachable, startup continues and authentication is simply not registered.
3. Adds the Key Vault configuration provider.
4. **Auto-migration:** `db.Database.Migrate()` on startup with up to 10 retries, 5 s apart (lets the DB container come up first).
5. HTTPS redirection is skipped in Development (a redirect would strip the `Authorization` header from the frontend's `http://localhost:5072` calls).

### REST API

All routes are under `/api`. `[Authorize]` = requires `Authorization: Bearer <jwt>`.

| Method & route | Auth | Use case |
|---|---|---|
| `POST /auth/register` | – | Create account (always starts as Owner); returns user + JWT + refresh token |
| `POST /auth/login` | – | Verify credentials; returns user + JWT + refresh token |
| `POST /auth/refresh` | – | Rotate refresh token (old one revoked), new JWT |
| `POST /auth/logout` | – | Revoke the supplied refresh token |
| `GET /auth/me` | ✔ | Current user |
| `PATCH /auth/me` | ✔ | Update first/last name, email (must be unique), address; returns a new JWT |
| `POST /auth/me/avatar` | ✔ | Upload profile picture (multipart `file`) |
| `POST /auth/change-role` | ✔ | Switch between `Owner` and `Sitter`; returns a new JWT with the new role claim |
| `GET /pets` · `GET /pets/{id}` | ✔ | List / get the caller's pets |
| `POST /pets` · `PUT /pets/{id}` · `DELETE /pets/{id}` | ✔ | Create / update / delete a pet (ownership enforced: other users' pets look like "Pet not found") |
| `POST /pets/{id}/pictures` · `DELETE /pets/{id}/pictures/{pictureId}` | ✔ | Add / remove a pet picture |
| `GET /availability` · `PUT /availability` | ✔ | Read / upsert the sitter's availability profile |

### Validation and business rules (as implemented)

- Register: valid email, password ≥ 8 chars, first/last name ≥ 2 chars; duplicate email rejected.
- Pet: name required, age ≥ 0, gender ∈ {Male, Female}, type ∈ {Cat, Dog}.
- Availability: `MaxPets` 1–10; the handler silently drops unknown days (valid: Mon–Sun), slots (Morning, Afternoon, Evening), services (DogWalking, DropInVisits, HomeBoarding, HouseSitting, Daycare) and pet types (Dog, Cat).
- Uploads: max 5 MB; JPEG/PNG/GIF/WebP only; blob path `avatars/{userId}/{guid}.ext` or `pets/{userId}/{petId}/{guid}.ext`.
- Tokens: access token 60 min, refresh token 7 days (`Jwt:ExpiresMinutes`, `Jwt:RefreshTokenExpiresDays`), refresh tokens are single-use (rotation).

### Data model (SQL Server, EF Core code-first)

| Table | Key columns | Relations |
|---|---|---|
| `Users` | Id, Email, PasswordHash, FirstName, LastName, Role (enum Owner/Sitter), IsEmailConfirmed, ProfilePictureUrl, Address, CreatedAt, UpdatedAt | root of all below |
| `RefreshTokens` | Id, UserId, Token, ExpiresAt, IsRevoked | N:1 User, cascade delete |
| `Pets` | Id, UserId, Name, Age, Gender (int), Type (int), SpecialNeeds, CreatedAt, UpdatedAt | N:1 User, cascade delete |
| `PetPictures` | Id, PetId, Url, UploadedAt | N:1 Pet, cascade delete |
| `SitterAvailabilities` | Id, UserId (unique), ScheduleJson, ServicesJson, AcceptedPetTypesJson, MaxPets, Bio, CreatedAt, UpdatedAt | 1:1 User, cascade delete |
| `Messages` | Id, Text | unused example entity |

Schedule/services/pet types are stored as JSON text in `nvarchar(max)` columns (simple, no join tables; not queryable by SQL for sitter search without JSON functions).
Migrations (chronological): InitialCreate → AddUserTable → AddRefreshTokenTable → AddUserRoleColumn → AddProfilePictureUrl → AddPetsTable → AddAddressToUser → AddSitterAvailabilityTable.

## 1.4 Frontend

| Technology | Version | Why it is used | Purpose in this app |
|---|---|---|---|
| Angular | 21.2 | Full framework, strong typing, standalone components | The SPA |
| TypeScript | 5.9 | Type safety | All frontend code |
| Angular Signals | built in | Lightweight reactive state (chosen over NgRx, see guide) | Auth state, form/loading flags, per-screen state |
| Angular Router | 21 | Client-side routing and guards | `/login`, `/home`, `/profile`, `authGuard` / `noAuthGuard` |
| Angular Forms (reactive) | 21 | Typed, validated forms | Login, register, profile, pet, availability |
| Angular `HttpClient` | 21 | HTTP calls, JSON/multipart | `UserService`, `PetService`, `AvailabilityService` |
| RxJS | 7.8 | Required by Angular HTTP; `firstValueFrom` bridges to async/await | HTTP streams |
| Angular SSR / platform-server + Express 5 | 21 / 5.1 | Server-side rendering and prerender support | Configured (`server.ts`, `app.routes.server.ts` prerender); the production image does **not** run the Node server (see 1.9) |
| Angular CDK | 21.2 | Accessibility/UI primitives | Installed dependency |
| ngx-scanner-qrcode | 1.8 | QR scanning | Installed but **not referenced in `src/`** (candidate for removal) |
| Nx (`@nx/angular`, `@nx/workspace`) | 23.2.1 | Task runner/caching around the Angular build (`nx serve/build/test`) | Build orchestration; Nx Cloud id present in `nx.json` |
| Vitest + jsdom | 4 / 28 | Fast unit test runner (Angular's `unit-test` builder) | `*.spec.ts` |
| Prettier | 3.8 | Consistent formatting | `.prettierrc` |
| Google Maps JavaScript API (+ Places) | loaded at runtime | Address autocomplete, map pin and reverse geocoding | Address field on the profile page |
| nginx (alpine) | – | Tiny, fast static server | Serves the built SPA in production |

### Structure and behaviour

- Routes: `/login` (guarded by `noAuthGuard`), then a `LayoutComponent` (navbar + outlet) guarded by `authGuard` containing `/home` and `/profile`; unknown routes redirect to `/home` or `/login`.
- Components: `AuthPage` (login/register), `Login`, `Register`, `Layout`, `Navbar` (role switch, avatar dropdown, logout), `Home` (welcome), `Profile` (account, pets, availability — the largest component, ~600 lines TS + ~450 lines HTML).
- Auth state: `TokenService` keeps the access and refresh tokens in signals mirrored to `localStorage`; `UserService` keeps the current user the same way and exposes `isLoggedIn`. Each service adds the `Authorization` header itself (no HTTP interceptor), and there is no automatic refresh-on-401 flow yet.
- Environments: `environment.ts` → `http://localhost:5072/api`; `environment.prod.ts` (swapped by `fileReplacements`) → `https://akster-backend.azurewebsites.net/api`.
- Build budgets: initial bundle warning 500 kB / error 1 MB.

## 1.5 DevOps and runtime packaging

| Technology | Why | Purpose |
|---|---|---|
| Docker (multi-stage) | Identical build everywhere, small runtime images | Backend: `dotnet/sdk:10.0` build → `aspnet:10.0` runtime (publishes `PetSitting.Api` only, listens on 8080). Frontend: `node:20-alpine` `nx build` → `nginx:alpine` serving `dist/frontend/browser` |
| nginx config | SPA needs fallback routing + cache control | `try_files … /index.html`; static assets cached 1 year (`immutable`); `index.html` `no-cache` |
| docker-compose | One-command local stack | SQL Server 2022 (healthchecked) + API on `:5000` + frontend on `:4200` |
| GitHub Actions (`deploy.yaml`) | CI/CD next to the code | 3 jobs, see below |

**Pipeline** (`push`/`pull_request` on `main`):
1. **Build & Test** — .NET 10 restore/build of `backend.slnx`; Node 20 `npm ci` + production build of the frontend. (No test step is run in CI today.)
2. **Push Docker Images** (main only) — logs into ACR, builds `backend` and `frontend` images, tags with the commit SHA and `latest`, pushes both.
3. **Deploy** (environment `production`) — `azure/login` with `AZURE_CREDENTIALS`, then `azure/webapps-deploy` of the SHA-tagged image to `akster-backend` and `akster-frontend`.

Required GitHub secrets: `ACR_LOGIN_SERVER`, `ACR_USERNAME`, `ACR_PASSWORD`, `AZURE_CREDENTIALS`.

## 1.6 Azure resources

*Source: `az resource list` output, cross-checked with `deploy.yaml`, `Program.cs` and `BlobService.cs`. Resource group `akster`, all resources in state Succeeded.*

| Resource | Type | Region | Why | Purpose |
|---|---|---|---|---|
| `aksterregistry` | Container Registry (Basic) | East US | Private image store that integrates with App Service | Holds the `backend` and `frontend` images (`:<commit-sha>` and `:latest`); its login server is the `ACR_LOGIN_SERVER` GitHub secret |
| `akster-plan` | App Service Plan (B1, Basic) | West Europe | One shared compute plan keeps cost low | Hosts both web apps |
| `akster-backend` | App Service (container), system-assigned managed identity | West Europe | Managed hosting, no VM management | Runs the ASP.NET Core API (`https://akster-backend.azurewebsites.net`). App settings: `ConnectionStrings__DefaultConnection`, `WEBSITES_PORT`, `WEBSITES_ENABLE_APP_SERVICE_STORAGE`, `DOCKER_REGISTRY_SERVER_URL` |
| `akster-frontend` | App Service (container) | West Europe | Same | Runs the nginx container serving the Angular SPA |
| `akster-vault` | Key Vault | West Europe | Keeps secrets out of source control and app settings | Secret `Jwt--Key` (auto-generated by the API if absent); also feeds other config through the Key Vault configuration provider |
| `aksterr` | SQL logical server | Sweden Central | Hosts Azure SQL databases | Server for the app database |
| `aksterr/aksterdb` | SQL Database (General Purpose, serverless Gen5) | Sweden Central | Relational store matching the EF Core SqlServer provider | The application database; schema is created by EF migrations on API startup |
| `aksterr/master` | SQL system database | Sweden Central | Created automatically | Not used by the app |
| `aksterstorage` | Storage Account (Standard_RAGRS) | East US | Cheap file storage so images are not kept in SQL | Blob container `avatars` (default, `AzureBlobStorage:ContainerName`) with avatars and pet pictures, public blob read |
| `DefaultWorkspace-3d4d4999-…-EUS` | Log Analytics workspace (group `DefaultResourceGroup-EUS`) | East US | Auto-created by Azure | Not referenced by the app; created by Azure Monitor / Container Insights |
| `MSCI-eastus-akster-aks` | Data Collection Rule | East US | Auto-created by Container Insights | Leftover from an AKS cluster that is no longer in the list; not used by the app |

Not in the resource list but used: a **service principal** whose JSON is in the `AZURE_CREDENTIALS` GitHub secret (CI/CD deploy identity; it lives in Entra ID).

**Observations**
- Resources are spread over three regions (West Europe: compute and vault; Sweden Central: SQL; East US: registry and storage). That adds latency between the API, database and storage, plus cross-region traffic cost. Moving SQL and storage to West Europe is the main improvement.
- There is no Application Insights resource, so the app has no telemetry beyond default App Service logs.
- `MSCI-eastus-akster-aks` and the default Log Analytics workspace look like leftovers from an earlier AKS experiment; probably safe to delete after checking nothing depends on them.

**Confirmed from your CLI output**
- `akster-backend` has a **system-assigned managed identity**, which is what `DefaultAzureCredential` uses to reach Key Vault.
- The SQL connection string is an **App Service setting** (`ConnectionStrings__DefaultConnection`), not a Key Vault secret. `WEBSITES_PORT` tells App Service the container listens on 8080.
- There is **no `AzureBlobStorage__*` app setting**, so the blob connection string must come from Key Vault (secrets `AzureBlobStorage--ConnectionString` / `--ContainerName` through the configuration provider). Otherwise uploads would fail with "not configured".
- No `DOCKER_REGISTRY_SERVER_USERNAME/PASSWORD` settings exist, so the app most likely pulls from `aksterregistry` using its managed identity (or the admin account configured outside app settings).
- **SKUs:** ACR Basic, App Service plan B1, SQL General Purpose serverless (Gen5), storage Standard_RAGRS.

**Cost/operations notes from the SKUs**
- SQL serverless can auto-pause when idle; the first request after a pause may be slow or fail, which the API's 10x5 s migration retry loop partly absorbs at startup.
- `RAGRS` (geo-redundant with read access) is the most expensive redundancy tier; for avatars and pet photos LRS or ZRS is usually enough.
- B1 has no deployment slots and no autoscale; deploys replace the running container directly.

**Also confirmed**
- **Key Vault** uses **Azure RBAC** (`enableRbacAuthorization = true`), so the backend's managed identity needs a role such as *Key Vault Secrets Officer* (the API both reads and creates `Jwt--Key`; *Secrets User* would be enough only if the key is pre-created).
- **Registry pulls** use the managed identity (`acrUseManagedIdentityCreds = true`), so no registry password is stored on the app. The identity needs the *AcrPull* role on `aksterregistry`.
- **SQL firewall** has two rules: `AllowAllWindowsAzureIps` (0.0.0.0, lets any Azure service in, including other tenants' resources) and a single developer IP rule created 2026-05-18. The developer IP is a home/office address that will go stale; remove it when no longer needed, and consider replacing the Azure-wide rule with a private endpoint or the App Service outbound IPs.

**Role assignments and vault contents (from CLI)**
- Key Vault secrets present: `AzureBlobStorage--ConnectionString` (confirms the blob setup), `Jwt--Key`, `SpotifyClientId`, `SpotifyClientSecret`. Nothing in this repository references Spotify, so those two belong to another project or a removed feature and can be deleted if unused.
- Role assignments found: **AcrPull** on `aksterregistry` and **Key Vault Secrets User** on `akster-vault`, both for principal `f9c5afed-9287-46dd-9035-a650729bec46`.
- **Mismatch (unresolved):** `f9c5afed-…` is neither the backend identity (`ed85e1f6-…`) nor the frontend identity (`24a65916-…`, also system-assigned). It is most likely the identity of the earlier AKS cluster (its leftover data collection rule is still in the group), or a user-assigned identity. As listed, **neither web app has a role on the registry or the vault**. That conflicts with the apps working (image pulls with `acrUseManagedIdentityCreds=true`, vault reads), so either the `--assignee` query returned another principal's rows (it did return `f9c5…` rows when asked for `ed85…`, which suggests a CLI/lookup quirk) or access is granted some other way. Verify at resource scope instead, which does not depend on the assignee filter:
  `az role assignment list --scope $(az keyvault show -n akster-vault --query id -o tsv) -o table`
  `az role assignment list --scope $(az acr show -n aksterregistry --query id -o tsv) -o table`
  and look for `ed85e1f6-…` / `24a65916-…`. The backend logs "Loaded Jwt:Key from Key Vault." when vault access works.
- *Key Vault Secrets User* is read-only, so the API cannot create `Jwt--Key` itself; the secret must stay pre-created (it is), otherwise the "generate and store" fallback in `Program.cs` would fail.

## 1.7 Configuration reference

| Key | Where | Meaning |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | env / Key Vault | SQL Server connection (compose: `ConnectionStrings__Default`, see 1.9) |
| `Jwt:Issuer` / `Audience` | appsettings | `akster` / `akster_users` |
| `Jwt:Key` | Key Vault `Jwt--Key` | HMAC-SHA256 signing key |
| `Jwt:ExpiresMinutes`, `Jwt:RefreshTokenExpiresDays` | appsettings | 60 / 7 |
| `KeyVaultUri` | appsettings | Vault URL |
| `AzureBlobStorage:ConnectionString`, `:ContainerName` | env / Key Vault | Blob access; container defaults to `avatars` |
| Local ports | launchSettings | API `http://localhost:5072` (https `7192`), Angular dev server `4200` |

## 1.8 Running locally

```bash
# Database + API + frontend (see caveat on the frontend build path in 1.9)
docker compose up --build

# Or separately
cd backend/PetSitting.Api && dotnet run          # http://localhost:5072
cd frontend && npm ci && npm start               # http://localhost:4200
```
Local backend runs need a SQL Server connection string and (for uploads) `AzureBlobStorage:ConnectionString`; Key Vault access falls back gracefully when you are not logged in to Azure.

## 1.9 Known issues and risks found while documenting

Ordered by importance. None were changed.

1. **Password hashing is unsalted SHA-256** (`RegisterCommandHandler`, `LoginCommandHandler`). Should become PBKDF2/Argon2/bcrypt (e.g. ASP.NET `PasswordHasher`). Existing hashes need a migration path.
2. **CORS is `AllowAnyOrigin`**; restrict to the frontend origin(s).
3. **A Google Maps API key is committed** in `frontend/src/environments/*.ts` (and in git history). Restrict it by HTTP referrer and quota in Google Cloud, and consider rotating it.
4. **`docker-compose.yml` is stale:** the frontend builds from `./frontend/frontend` (path does not exist, should be `./frontend`), serves on 80 but maps nothing matching `4200`, and the API gets `ConnectionStrings__Default` while the code reads `ConnectionStrings:DefaultConnection`. It also contains a hard-coded SA password (fine for local only).
5. **`Program.cs` logs the connection string** at startup, which would leak DB credentials into App Service logs.
6. **Public blob container** (`PublicAccessType.Blob`): every uploaded image is world-readable by URL. Acceptable for avatars; reconsider for anything sensitive.
7. **Tokens in `localStorage`** (XSS exposure) and **no refresh-on-401 / HTTP interceptor**: after 60 minutes API calls fail until the user logs in again.
8. **Email is not verified** (`IsEmailConfirmed` is never set to true); **any user can switch to Sitter** freely via `change-role` (by design today, but a trust issue once sitters are searchable).
9. **No tests run in CI**, and the backend has no test project.
10. **Legacy duplicate backend code** in `backend/` root (`Identity/`, `Pets/`, `Availability/`, `Controllers/`, `Models/`, `Program.cs`, `DbContext.cs`, `backend.csproj` with older package versions such as MediatR 14 and FluentValidation 12) is not built by the Dockerfile or `backend.slnx` and can be deleted after confirming nothing references it. `Message` entity / `Messages` table is also unused.
11. **SSR is configured but not used in production** (nginx serves static files, and `RenderMode.Prerender` for `**` conflicts with auth-guarded, `localStorage`-dependent pages). Either drop SSR packages or run the Node server image.
12. **Frontend does not yet follow its own guide** (no `features/` folders or facades; the profile component is a ~600-line component that does HTTP via services directly).
13. `ngx-scanner-qrcode` is unused; `.github/copilot-instructions.md` points to `/docs/architecture/*.md` files that do not exist (the guides live in `backend/AGENTS.md` and `frontend/AGENTS.md`).

---

# Part 2 — Brief overview of the app

**Akster / PetPal** is a pet-sitting platform that connects **pet owners** with **pet sitters**. Today it covers the foundation: accounts, roles, owner pet profiles, and sitter availability profiles. The architecture guides anticipate the next layer of features (bookings, scheduling conflicts, cancellation policies, pricing, sitter-pet matching, messaging), which are not yet implemented.

A single account can act as either role and switch between them at any time, so someone who owns a dog can also offer sitting services.

---

# Part 3 — Functional documentation (non-technical)

## Who uses it
- **Owner** — has pets and (eventually) looks for someone to care for them.
- **Sitter** — offers pet-care services and says when they are available.

## What you can do today

**Account**
- Sign up with email, password, first and last name; sign in and out. New accounts start as Owners.
- Stay signed in between visits.
- Edit your name, email and address (address has map-based autocomplete and a pin you can drag/click on a map).
- Upload a profile picture (JPEG, PNG, GIF, WebP up to 5 MB).
- Switch between **Owner** and **Sitter** with one click from the top navigation bar.

**As an Owner — "My Pets"**
- Add a pet: name, age, type (cat or dog), gender, and any special needs.
- Edit or remove a pet.
- Add and remove pictures for each pet.

**As a Sitter — "My Availability"**
- Pick the weekly schedule: for each day, Morning / Afternoon / Evening.
- Choose services offered: Dog walking, Drop-in visits, Home boarding, House sitting, Daycare.
- Choose accepted pet types (dogs, cats) and the maximum number of pets at a time (1–10).
- Write a short "About me" bio.

**Home page** — a welcome screen showing your name and current role.

## Typical flows
1. *New owner:* register → land on Home → open Profile → add address and pets with photos.
2. *Becoming a sitter:* switch role in the navbar → open Profile → set schedule, services, pet types, capacity and bio.
3. *Returning user:* open the site → still signed in → continue; sign out from the avatar menu.

## Not available yet
Searching for sitters, booking requests and confirmation, calendar conflict checks, pricing and payments, cancellations, reviews, in-app messaging, notifications, email verification and password reset.
