# Akster (PetPal) — Documentation

> Part 1 is the technical documentation (including how AI is used in the project, 1.10), Part 2 is a short overview of the app, and Part 3 is a lighter functional documentation.
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
| `backend/PetSitting.Application` | Per-subsystem managers (use cases), validators, DTOs, repository and service interfaces |
| `backend/PetSitting.Domain` | Entities, aggregate roots, enums, `Result` type, grouped by subsystem |
| `backend/PetSitting.Infrastructure` | EF Core `AppDbContext`, repositories, migrations, JWT and Blob services |
| `frontend/` | Angular 21 app (Nx-wrapped), nginx config, Dockerfile |
| `.github/workflows/deploy.yaml` | CI/CD pipeline |
| `docker-compose.yml` | Local stack (SQL Server + API + frontend) |
| `.claude/`, `.agents/`, `skills-lock.json` | AI-assistant skills/config, not part of the product |

## 1.2 Architecture decisions (from `backend/AGENTS.md` and `frontend/AGENTS.md`, the stated source of truth)

**Backend:** subsystems (`Users`, `Pets`, `Availability`) as folders in the layer projects, Controller → Manager → Repository, tactical DDD in the Domain, one repository per aggregate root (no generic `IRepository<T>`), FluentValidation called from managers, `Result<T>` with an error kind for expected failures. Rejected alternatives: CQRS/MediatR (the previous design), generic Repository/Unit-of-Work, a repository per table, a project per subsystem.
**Frontend:** single Angular app (no monorepo apps), feature-folder structure, standalone components, Signals instead of NgRx, one Facade per feature, smart/dumb component split.

> Note: the guides describe the *target* architecture. The backend follows its guide (the MediatR migration is complete, see "Migrating from MediatR" in 1.3). The frontend is flatter than its guide; the frontend is still flatter than the guide (no `features/` folders or facades yet, see 1.9).

## 1.3 Backend

### Technologies

| Technology | Version | Why it is used | Purpose in this app |
|---|---|---|---|
| .NET / C# | 10 (`net10.0`) | Current LTS-line runtime, strong typing, good Azure integration | Whole backend |
| ASP.NET Core Web API (controllers) | 10 | Mature, simple routing and model binding | HTTP API under `/api/*` |
| ASP.NET Core OpenAPI | 10.0.2 | Built-in API description, no Swashbuckle needed | `MapOpenApi()`, Development only |
| Entity Framework Core + SqlServer provider | 10.0.3 | Code-first model, migrations, LINQ | Persistence, schema evolution |
| EF Core Design | 10.0.3 | Tooling for `dotnet ef migrations` | Dev-time only (`PrivateAssets=all`) |
| FluentValidation (+ DI extensions) | 11.x | Declarative, testable validation separated from handlers | One validator per input record, called explicitly at the top of the manager method |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.8 | Standard bearer-token auth | Validates JWTs (issuer, audience, lifetime, signing key) |
| System.IdentityModel.Tokens.Jwt | 8.9.0 | JWT creation | `JwtTokenService` issues access tokens |
| Azure.Identity | 1.21.0 | `DefaultAzureCredential`: managed identity in Azure, developer login locally, no secrets in code | Authenticates to Key Vault |
| Azure.Security.KeyVault.Secrets | 4.9.0 | Read/write secrets | Loads (or generates) `Jwt--Key` |
| Azure.Extensions.AspNetCore.Configuration.Secrets | 1.5.0 | Key Vault as a configuration provider | Vault secrets appear as normal config (e.g. connection string, blob settings) |
| Azure.Storage.Blobs | 12.28.0 | Official blob SDK | Stores avatars and pet pictures |

### Layering and dependency direction

`Api → Application → Domain` and `Infrastructure → Application, Domain`. The Application layer only knows interfaces (repositories, `IJwtTokenService`, `IBlobService`); Infrastructure implements them; `Program.cs` wires everything explicitly in DI (managers, repositories and `BlobService` scoped, `JwtTokenService` singleton). Only validators are auto-discovered (`AddValidatorsFromAssemblyContaining`).

**Subsystems.** The code is split by business area, as folders inside each layer project:

| Subsystem | Owns | Aggregate roots (each has one repository) |
|---|---|---|
| `Users` | Accounts, login/register/refresh/logout, profile, avatar, role | `User` (owns `RefreshToken`) |
| `Pets` | Owner's pets and their pictures | `Pet` (owns `PetPicture`) |
| `Availability` | Sitter schedule, services, capacity | `SitterAvailability` |

Rules between subsystems (from `backend/AGENTS.md`): a subsystem's public face is `I<Subsystem>Manager` plus its DTOs. A manager may call another subsystem's *manager interface*, never its repository, entities or `DbSet`. Cross-subsystem references are by id only (e.g. `Pet.UserId`), no navigation properties. Boundaries are enforced by convention and review, not by the compiler (one project per layer, one `AppDbContext`, one migration history).

### Request flow

`Controller → I<Subsystem>Manager → Repository → EF Core → SQL Server`

- **Controller** (`PetSitting.Api/Controllers/`): thin. Binds the request record, is `[Authorize]`, reads `UserId` from the JWT via `ApiControllerBase.UserId`, calls one manager method and maps the returned `Result` to an HTTP status through one shared helper in `ApiControllerBase`.
- **Manager** (`PetSitting.Application/<Subsystem>/`): one public method per use case. Wherever a manager is injected (controllers, other managers) the field is named `_<subsystem>Manager`, e.g. `_petsManager`, with constructor parameter `petsManager`. Repositories are named the same way: `_petsRepository`, `_usersRepository`, `_availabilityRepository`. Other services take their interface name: `_blobService`, `_jwtTokenService`. Order inside a method: validate input (FluentValidation, `IValidator<T>` injected) → load through the repository → check ownership/role → call methods on the aggregate → persist through the repository → return a `Result<Dto>`.
- **Engine / Helper**: pure business logic shared by two or more managers (for example pricing). Only created when the sharing is real.
- **Repository** (`PetSitting.Infrastructure/Persistence/Repositories/<Subsystem>/`): EF Core for one aggregate root. Returns materialized results, never `IQueryable`. A repository method persists its own aggregate (the aggregate is the transaction boundary).
- **Failures:** expected failures are `Result.Failure(kind, message)` with a kind (`NotFound`, `Validation`, `Unauthorized`, `Forbidden`, `Conflict`). `ApiControllerBase.ToAction` maps kind → 404/400/401/403/409, so there is no text matching. Success bodies keep the frontend's `{ success, message, ... }` shape via the records in `Api/Contracts/Responses.cs`, built in the controller. Unexpected exceptions are turned into `500 { success:false, message:"An unexpected error occurred." }` by the global exception handler in `Program.cs`.
- **Cross-cutting:** authentication/authorization via ASP.NET attributes, validation inside the manager, logging via `ILogger`. There is no pipeline behavior layer.

### Why Controller → Manager → Repository (decision record)

The previous design was CQRS with MediatR: each use case was a command/query record, a handler and a validator, dispatched through `IMediator` with a `ValidationBehavior`. It was replaced because, for a single-process app with one developer, it cost more than it paid:
- Three to four files per use case plus implicit behaviors, and *Go to Definition* on `Send(...)` does not reach the handler.
- No other component ever dispatched these requests, so the decoupling was unused; domain events (`INotification`) were never published.
- Each handler was called from a single controller action, so the indirection bought consistency, not reuse.
- MediatR 13+ is commercially licensed for larger companies.

What replaced it keeps the good parts: thin controllers, one place per use case (a manager method), validators separate from logic, domain rules on aggregates, repositories per aggregate root, `Result` for expected failures.

Trade-offs to know:
- A manager can grow into a god class. Mitigation: one manager per subsystem, split by sub-area (e.g. `AuthManager` / `ProfileManager` in `Users`) when it passes about 15 methods.
- Validation is no longer automatic: every manager method must call its validator. A missing call is a bug a reviewer must catch; keep the call as the first line of the method.
- Subsystem boundaries are by convention. If they get violated repeatedly, an architecture test (for example NetArchTest) or a project per subsystem is the next step.

### Navigating and debugging one request (worked example: "add a pet")

Paths are relative to `backend/`. Every use case follows the same stops; replace `Pets/CreatePet` with any other method.

| # | Stop | File | What to look at / where to put a breakpoint |
|---|---|---|---|
| 0 | Browser call | `frontend/src/app/services/pet.service.ts` (`createPet`), triggered from `frontend/src/app/profile/profile.component.ts` | URL, JSON body, `Authorization` header. Check the Network tab first: status code and response JSON tell you which stop to jump to. |
| 1 | Pipeline (auth) | `PetSitting.Api/Program.cs` | CORS, `UseAuthentication`/`UseAuthorization`. A `401` never reaches the controller: bad/expired token, or JWT key/issuer/audience mismatch. |
| 2 | Controller | `PetSitting.Api/Controllers/PetsController.cs` → `CreatePet` | **Best first breakpoint.** Confirms the request arrived and `UserId` (from `ApiControllerBase.UserId`, the JWT claim) is right. |
| 3 | Request body shape | `PetSitting.Api/Contracts/Requests.cs` (`PetRequest`; login/register bind `LoginInput`/`RegisterInput` directly) | If a field is null/default, the JSON names do not match this record (model binding failed before your code ran, a `400` with ASP.NET's own error shape). |
| 4 | **The manager method** | `PetSitting.Application/Pets/PetsManager.cs` → `CreatePetAsync` | **Where the logic is.** Validation, ownership checks and "not found"-style failures are returned from here as `Result.Failure(...)`. *Go to Definition* on the interface call in the controller lands on `IPetsManager`; use *Go to Implementation* (`Ctrl+F12`). |
| 5 | Validator | `PetSitting.Application/Pets/Validators/PetInputValidator.cs` (shared by create and update) | Input shape rules. A failure comes back as a `Validation` result → `400 { success:false, message }`. |
| 6 | Repository interface | `PetSitting.Application/Pets/IPetRepository.cs` | The contract the manager calls. |
| 7 | Repository implementation | `PetSitting.Infrastructure/Persistence/Repositories/Pets/PetRepository.cs` | The EF Core query/`SaveChangesAsync`. SQL errors surface here. |
| 8 | Model and schema | `PetSitting.Domain/Pets/Pet.cs`, `PetSitting.Infrastructure/Persistence/AppDbContext.cs`, `PetSitting.Infrastructure/Migrations/` | Entity shape, relations, delete behavior; a missing column means a missing migration. |
| 9 | Response | `PetSitting.Application/Pets/PetDto.cs` (DTO and `ToDto()`), `PetSitting.Api/Contracts/Responses.cs` (wire wrapper) | The controller builds the wrapper and `ApiControllerBase.ToAction` maps the `Result` to the HTTP status. |

**How to find any use case:** the controller is the index of the subsystem. The action calls one manager method; open it, and the validator, repository interface and DTO are in the same subsystem folder.

**Debugging checklist by symptom**

| Symptom | Likely stop | Check |
|---|---|---|
| 401 | 1 | Token missing/expired, `Jwt:*` settings, `Jwt--Key` loaded? (backend log "Loaded Jwt:Key from Key Vault.") |
| 400 with `{ success:false, message }` | 4 or 5 | Message text: validator rule (5) or manager failure (4). Search the repo for that exact string; it points to the file. |
| 400 with ASP.NET `errors` object | 3 | Body does not match the request record |
| 404 | 4 | Manager returned a `NotFound` failure (also what a wrong-owner pet looks like) |
| 500 "An unexpected error occurred." | 4-7 | Exception swallowed by the global handler. Run locally and read the console, or break on exceptions; the message never reaches the client. |
| "Unable to resolve service for type ..." | `Program.cs` | New manager/repository/service is not registered. |
| Works locally, fails in Azure | config | App settings / Key Vault (section 1.6), DB connectivity and migrations |

**Adding a new use case to an existing subsystem (same map in reverse)**
1. Add the method to `I<Subsystem>Manager` and implement it in `<Subsystem>Manager`; add a validator in `Validators/` if it takes input (and call it first).
2. Add the controller action in `Api/Controllers/<Subsystem>Controller.cs`, and a request record in `Api/Contracts/Requests.cs` if it takes a body.
3. If it needs new data access, add the method to the repository interface (`Application/<Subsystem>/`) and implement it in `Infrastructure/Persistence/Repositories/<Subsystem>/`.
4. If the schema changes, add a migration (`dotnet ef migrations add <Name>` with `PetSitting.Infrastructure` as the project and `PetSitting.Api` as the startup project).
5. Add the call in the frontend service (`frontend/src/app/services/`) and the model in `models/user.model.ts`.

### Migrating from MediatR (done)

The code was migrated from MediatR to managers subsystem by subsystem (Availability, Pets, Users). What changed, for anyone reading old commits:
- [x] `Result`/`Result<T>` got an `ErrorKind`; `ApiControllerBase.ToAction` maps it to HTTP status (replaces `Respond()` text matching). Login failures are `Unauthorized` (401), duplicate email/profile email are `Conflict` (409, previously 400), missing records are `NotFound` (404, `UpdateMe`/`ChangeRole` for a missing user were 400), everything else expected is `Validation` (400).
- [x] Each handler became a manager method; the 17 handlers, `Features/`, `ValidationBehavior`, `Common/Behaviors/`, `Abstractions/`, `DomainEvent` and the MediatR packages are gone. The two identical Create/Update pet validators became one `PetInputValidator`.
- [x] Repository interfaces moved into `Application/<Subsystem>/`, implementations into `Infrastructure/Persistence/Repositories/<Subsystem>/`. `IBlobService`, `IJwtTokenService` and `FileUpload` moved to `Application/Common/`.
- [x] `Identity` was renamed `Users` (namespaces, folders, migration snapshots). `dotnet ef migrations has-pending-model-changes` reports no model drift; the database schema is untouched.
- [x] The wire format is unchanged: success bodies are still `{ success, message, user|pet|pets|availability, token, refreshToken }` (records in `Api/Contracts/Responses.cs`); failures are `{ success:false, message }`.
- [x] The legacy root-level backend (`Identity/`, `Pets/`, `Availability/`, `Controllers/`, `Models/`, `Program.cs`, `DbContext.cs`, `backend.csproj`) was deleted.

Known leftovers: the cross-subsystem EF navigation properties (`User.Pets`, `Pet.User`, `SitterAvailability.User`) and the unused `Message` entity still exist; entities still have public setters instead of rule methods; there is still no test project.

### Implementing a feature end to end

The worked example is **illustrative** ("an owner requests a booking from a sitter"): nothing named `Booking` exists yet. It was picked because it touches every layer, including a real domain rule and a cross-subsystem call. Paths are relative to `backend/` unless prefixed `frontend/`. Follow the order below, inside-out, so each step compiles on its own and you can stop after any step.

#### Before you start (5 minutes, saves hours)
1. **Write down the use case in one sentence and its failure cases.** "Owner requests a booking for a pet from a sitter for a date range. Fails if: sitter not found or not a Sitter, pet not yours, dates in the past or end before start, sitter not available, overlaps an accepted booking."
2. **Decide which subsystem owns it.** A new business area (`Bookings`) is a new subsystem: new folders in Domain, Application and Infrastructure, a new manager and a new controller. Otherwise add to the existing subsystem.
3. **Decide who may call it.** Which role, and which resource must belong to the caller. Authorization is part of the use case, not an afterthought.
4. **Name the method** `<Verb><Noun>Async` (`RequestBookingAsync`).
5. **Check the architecture guides** (`backend/AGENTS.md`, `frontend/AGENTS.md`): aggregate rules, no generic repository, subsystem boundaries.

#### Step 1: Domain (`PetSitting.Domain/Bookings/`)
- Create the aggregate `Booking.cs` (Id, OwnerId, SitterId, PetId, start/end, `BookingStatus`, CreatedAt, UpdatedAt) and `BookingStatus.cs` (enum: Requested, Accepted, Declined, Cancelled, Completed).
- Put **rules on the entity**, not in the manager: methods such as `Accept()` and `Cancel()` that refuse illegal state changes. (Today's entities only have public setters; for bookings prefer methods so the rules cannot be bypassed.)
- Use a value object for concepts without identity (`DateRange`, which enforces "start < end" in its constructor) rather than two loose `DateTime`s.
- Reference other subsystems by id (`SitterId`, `PetId`), not by navigation property.
- *Don't forget:* store times in **UTC** (the code uses `DateTime.UtcNow`); enums are stored as **int**, so never reorder or renumber existing enum members.

#### Step 2: Persistence (`PetSitting.Infrastructure/Persistence/`)
1. `AppDbContext.cs`: add `DbSet<Booking> Bookings` and the relationship configuration in `OnModelCreating` (foreign keys, `OnDelete` behavior). **Think about delete behavior:** deleting a user or pet that has bookings should not silently cascade away booking history; prefer `Restrict` and decide the product rule.
2. Generate the migration:
   ```bash
   dotnet ef migrations add AddBookingsTable -p PetSitting.Infrastructure -s PetSitting.Api
   ```
3. **Open the generated migration file and read it.** Check that it only contains your change, plus the column types (`datetime2`, `nvarchar`) and indexes. Add indexes for columns you will filter on (`SitterId`, date range).
4. *Don't forget:* the API runs `Database.Migrate()` **automatically at startup, including in Azure**. A bad or destructive migration is applied to the production database on the next deploy. Test it on a local database first, and make breaking changes in two steps (add, deploy, then remove).

#### Step 3: Repository (aggregate root only)
- `PetSitting.Application/Bookings/IBookingRepository.cs`: only the methods the use cases need (`GetByIdAsync`, `AddAsync`, `HasOverlapAsync(sitterId, range)`). One repository per **aggregate root**, no generic `IRepository<T>`, none for child entities.
- `PetSitting.Infrastructure/Persistence/Repositories/Bookings/BookingRepository.cs`: the implementation (`Include` what the manager needs; return `null` when not found; return materialized results; persist the aggregate with `SaveChangesAsync`).
- **Register it in `PetSitting.Api/Program.cs`:** `builder.Services.AddScoped<IBookingRepository, BookingRepository>();`. Only validators are auto-discovered. A missing line compiles fine and fails at runtime with "Unable to resolve service for type IBookingRepository".

#### Step 4: The manager (`PetSitting.Application/Bookings/`)
1. `BookingDto.cs`: the DTO and a `ToDto()` mapping extension, exactly like `Pets/PetDto.cs`. **Never return the entity**: it carries navigation properties and sensitive fields (for example `PasswordHash` on `User`).
2. `Validators/RequestBookingValidator.cs` (FluentValidation): **shape and format only**: ids > 0, end after start, start not in the past, text lengths. Anything that needs the database belongs in the manager.
3. `IBookingsManager.cs` and `BookingsManager.cs`, method `RequestBookingAsync(int userId, RequestBookingInput input, ...)` returning `Result<BookingDto>`. `userId` comes from the JWT (the controller passes it), never from the body. Inside, in this order:
   1. validate the input with the injected `IValidator<RequestBookingInput>`,
   2. the caller owns the pet: ask `IPetsManager`, not `IPetRepository` (return "Pet not found." for both missing and foreign pets, so ids cannot be probed),
   3. the target user exists and has role Sitter: ask `IUsersManager`,
   4. the sitter's availability and capacity allow it: ask `IAvailabilityManager`, do not copy the data,
   5. no overlapping accepted booking (own repository),
   6. create the aggregate through its constructor or factory so the domain rules run, persist via `IBookingRepository`, log with `ILogger`, return `Result.Success(dto)`.
   - Keep the manager an **orchestrator**: load, call domain methods, save. Rules about the booking itself live on `Booking`.
   - A read use case (`GetMyBookingsAsync`) is the same minus the validator: read and return DTOs. Never modify state in a read.
4. Failure convention: return `Result.Failure(kind, message)`. Use `NotFound` only for genuine 404 cases.

#### Step 5: API (`PetSitting.Api/`)
1. `Contracts/Requests.cs`: add `BookingRequest(int SitterId, int PetId, DateTime StartUtc, DateTime EndUtc)`. Property names are the JSON contract with the frontend (camelCase on the wire).
2. `Controllers/BookingsController.cs`: `[ApiController]`, `[Route("api/[controller]")]`, **`[Authorize]`** (add a role restriction if only Owners may call), inherit `ApiControllerBase`, inject `IBookingsManager`. The action only passes `UserId` and the request to the manager and maps the result: `CreatedAtAction` for creates, the shared `Result` helper for lookups and updates. No logic here.
3. Register the manager in `Program.cs` (`AddScoped<IBookingsManager, BookingsManager>()`).
4. Try it before writing any frontend: add the call to `backend/backend.http` (or use OpenAPI in Development) and test the success case, each failure case, no token (401), and someone else's data.
   - No CORS or DI changes are needed beyond the registrations above. If the endpoint accepts files, set explicit size limits and validate the content type like `BlobService` does.

#### Step 6: Frontend (`frontend/src/app/`)
1. `models/user.model.ts` (or a new model file when the feature grows): types mirroring `BookingDto` and the request/result shapes. Keep names identical to the JSON (camelCase).
2. `services/booking.service.ts`: follow `pet.service.ts` (URL from `environment.apiRootUrl`, `Authorization` header, `Observable` return). If you add more services, consider one HTTP interceptor for the header (see issue 7 in 1.9) instead of repeating it.
3. UI: a component with a reactive form (`Validators` mirroring the server rules for fast feedback, but **the server remains the authority**) and `signal`s for `isSaving`, `errorMessage` and the result. Show the server's `message` on failure. Register the route in `app.routes.ts` under the authenticated layout, and add a navbar link if needed.
4. The frontend guide's target structure is `features/bookings/` with a facade that owns the signals and HTTP calls, and components that only talk to the facade. Existing code is flatter; **new features should follow the guide**, and existing screens can stay until you refactor them deliberately.
5. Handle loading, empty, error and success states, plus 401 (token expired, send to login) explicitly; never leave a spinner running on failure.

#### Step 7: Verify and ship
- Run `dotnet build backend/backend.slnx` and, in `frontend/`, `npm run build` (the same two things CI runs). **CI does not run tests**, so your manual verification is the safety net.
- Add tests for domain rules (`Booking.Cancel()`, `DateRange`) when you build them. There is no test project yet, so create one (for example `PetSitting.Domain.Tests` with xUnit) when the first real rules appear. Managers are easy to test with fake repositories or SQLite in memory.
- Do a manual end-to-end pass in the browser: happy path, each failure message, a page refresh, and a second user trying to reach the first user's data.
- New configuration (keys, URLs) goes to Key Vault or App Service settings, **never** into `appsettings.json` or the environment files; add a placeholder to `.env.example` for local Docker.
- Commit in small slices (domain, persistence, manager, API, frontend). A merge to `main` builds, pushes images and **deploys straight to production**, running the migration on startup.
- Update this document (API table, data model, functional section) and the `AGENTS.md` files if you changed an architectural rule.

#### Quick checklist (copy into the PR description)
- [ ] Use case and failure cases written down; owning subsystem decided
- [ ] Domain rule lives on the entity or value object, not only in the manager
- [ ] `DbSet`, relationships and **migration generated, read and tested locally**
- [ ] Repository only for an aggregate root; interface, implementation and **`AddScoped` in `Program.cs`**
- [ ] Manager method calls its validator first; other subsystems used only through their manager interface
- [ ] `UserId` from the JWT; **ownership and role checked**; DTOs returned, no entities
- [ ] `Result.Failure` uses the right error kind; `NotFound` only for real 404s
- [ ] Controller is `[Authorize]` and thin; request record added; manager registered
- [ ] Tried through `backend.http`, including 401 and other-user cases
- [ ] Frontend model, service, component, route; all states handled
- [ ] Both builds pass; secrets and config not committed; docs updated

#### Common mistakes
| Mistake | What happens |
|---|---|
| Manager or repository not registered in `Program.cs` | Runtime DI error on the first request, not at build time |
| Forgetting to call the validator in a manager method | Invalid input reaches the domain and the database |
| Naming an injected dependency `_pets` / `_users` / `_repository` / `_blobs` | Reads like a collection and hides what the dependency is; use `_petsManager`, `_petsRepository`, `_blobService` |
| Injecting another subsystem's repository | Bypasses that subsystem's rules and ownership checks; call its manager |
| A repository for a child entity (for example `PetPictureRepository`) | Lets code change children without going through the aggregate root's rules |
| Taking `UserId` from the request body | Any user can act as another user |
| Checking that the record exists but not that it belongs to the caller | Cross-user data access |
| Reordering enum members | Existing rows silently change meaning (stored as int) |
| Returning the EF entity | Leaks fields and can cause JSON cycles |
| Database-dependent checks in the validator | Validators should only check input shape |
| Editing a migration after it was deployed | Production and the migration history diverge; add a new migration instead |
| `DateTime.Now` | Wrong across time zones; use `DateTime.UtcNow` |
| Forgetting the frontend error state | The user sees a frozen UI on 400/401 |

### Startup behaviour (`Program.cs`)

1. Registers controllers, OpenAPI, CORS (default policy), `AppDbContext` (connection string `ConnectionStrings:DefaultConnection`), managers, repositories, services and validators (explicit registrations; validators are found by assembly scan).
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
- Availability: `MaxPets` 1–10; the manager silently drops unknown days (valid: Mon–Sun), slots (Morning, Afternoon, Evening), services (DogWalking, DropInVisits, HomeBoarding, HouseSitting, Daycare) and pet types (Dog, Cat).
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
| docker-compose | One-command local stack | SQL Server 2022 (healthchecked) + API on `:5072` + frontend (nginx) on `:4200`; secrets come from a git-ignored `.env` (template: `.env.example` with `MSSQL_SA_PASSWORD`, `JWT_KEY`) |
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
| `ConnectionStrings:DefaultConnection` | env / Key Vault | SQL Server connection (compose sets `ConnectionStrings__DefaultConnection`) |
| `Jwt:Issuer` / `Audience` | appsettings | `akster` / `akster_users` |
| `Jwt:Key` | Key Vault `Jwt--Key` | HMAC-SHA256 signing key |
| `Jwt:ExpiresMinutes`, `Jwt:RefreshTokenExpiresDays` | appsettings | 60 / 7 |
| `KeyVaultUri` | appsettings | Vault URL |
| `AzureBlobStorage:ConnectionString`, `:ContainerName` | env / Key Vault | Blob access; container defaults to `avatars` |
| Local ports | launchSettings | API `http://localhost:5072` (https `7192`), Angular dev server `4200` |

## 1.8 Running locally

```bash
# Copy .env.example to .env and set MSSQL_SA_PASSWORD and JWT_KEY first
docker compose up --build   # Database + API + frontend

# Or separately
cd backend/PetSitting.Api && dotnet run          # http://localhost:5072
cd frontend && npm ci && npm start               # http://localhost:4200
```
Local backend runs need a SQL Server connection string and (for uploads) `AzureBlobStorage:ConnectionString`; Key Vault access falls back gracefully when you are not logged in to Azure.

## 1.9 Known issues and risks found while documenting

Re-checked against the working tree on 2026-10-07. Items 4 and 5 are fixed (changes are still uncommitted); everything else still applies.

1. **Password hashing is unsalted SHA-256** (register and login in the Users subsystem). Should become PBKDF2/Argon2/bcrypt (e.g. ASP.NET `PasswordHasher`). Existing hashes need a migration path. *Still SHA-256.*
2. **CORS is `AllowAnyOrigin`**; restrict to the frontend origin(s). *Still `AllowAnyOrigin`.*
3. **A Google Maps API key is committed** in `frontend/src/environments/*.ts` (and in git history). Restrict it by HTTP referrer and quota in Google Cloud, and consider rotating it. *Still present in both environment files.*
4. ~~`docker-compose.yml` is stale~~ **Fixed (uncommitted):** frontend now builds from `./frontend` on `4200:80`, API on `5072:8080` with `ConnectionStrings__DefaultConnection` and `Jwt__Key`, and the SA password moved to `.env` (`.env.example` added, `.env` git-ignored, `.gitignore` paths corrected). Remaining: the old password `Strong!Passw0rd123` stays in git history (local-only, low risk), and compose does not pass `AzureBlobStorage__ConnectionString`, so uploads fail locally unless you add it.
5. ~~`Program.cs` logs the connection string~~ **Fixed (uncommitted)** in both `PetSitting.Api/Program.cs` and the legacy `backend/Program.cs`.
6. **Public blob container** (`PublicAccessType.Blob`): every uploaded image is world-readable by URL. Acceptable for avatars; reconsider for anything sensitive.
7. **Tokens in `localStorage`** (XSS exposure) and **no refresh-on-401 / HTTP interceptor**: after 60 minutes API calls fail until the user logs in again.
8. **Email is not verified** (`IsEmailConfirmed` is never set to true); **any user can switch to Sitter** freely via `change-role` (by design today, but a trust issue once sitters are searchable).
9. **No tests run in CI**, and the backend has no test project.
10. ~~**Legacy duplicate backend code**~~ **Fixed:** the old service-style backend in `backend/` root was deleted during the MediatR migration. Previously: root (`Identity/`, `Pets/`, `Availability/`, `Controllers/`, `Models/`, `Program.cs`, `DbContext.cs`, `backend.csproj` with older package versions such as MediatR 14 and FluentValidation 12) is not built by the Dockerfile or `backend.slnx` and can be deleted after confirming nothing references it. `Message` entity / `Messages` table is also unused. *Still present (`Identity/`, `Pets/`, `Availability/`, `Controllers/`, `Models/`, `Data/`, root `Program.cs`, `DbContext.cs`, `backend.csproj`).*
11. **SSR is configured but not used in production** (nginx serves static files, and `RenderMode.Prerender` for `**` conflicts with auth-guarded, `localStorage`-dependent pages). Either drop SSR packages or run the Node server image.
12. **Frontend does not yet follow its own guide** (no `features/` folders or facades; the profile component is a ~600-line component that does HTTP via services directly).
13. `ngx-scanner-qrcode` is unused; `.github/copilot-instructions.md` points to `/docs/architecture/*.md` files that do not exist (the guides live in `backend/AGENTS.md` and `frontend/AGENTS.md`). *Still true; `.vs/` (115 files) and `frontend/.claude/settings.local.json` are still tracked.*

## 1.10 AI-assisted development

AI is a development tool here, not a product feature: nothing in the running app calls an AI service. The setup lives in the repository so every session starts with the same rules.

### Tools

| Tool | How it is used |
|---|---|
| **Claude (Claude Pro subscription), via Claude Code** | Main coding assistant: exploring the code, implementing features, refactoring, reviewing, generating documentation like this file. Runs in the Claude desktop app (Code tab) and/or the terminal against the local repo. |
| **GitHub Copilot** | Secondary assistant, used from Visual Studio / VS Code. Evidence: `.github/copilot-instructions.md`, `frontend/.github/copilot-instructions.md`, and Copilot snapshot files under `.vs/CopilotSnapshots/`. |
| **Angular CLI MCP server** | `frontend/.vscode/mcp.json` registers `npx @angular/cli mcp`, giving the assistant Angular-aware tools and current docs inside VS Code. |

### How the rules reach the assistant

1. **`CLAUDE.md` (root)** tells the assistant to read `backend/CLAUDE.md` and `frontend/CLAUDE.md` before making architectural decisions or generating code, and calls them authoritative.
2. **`backend/CLAUDE.md` and `frontend/CLAUDE.md`** each contain only `@AGENTS.md`, an import of the real guide. Keeping the content in `AGENTS.md` means the same file is understood by other agents too, not just Claude.
3. **`backend/AGENTS.md` and `frontend/AGENTS.md`** are the architecture decision records (subsystems, Controller → Manager → Repository, aggregate-only repositories, `Result<T>`; Angular standalone components, Signals, facades, no NgRx/Nx apps). They end with explicit "Rules for AI Assistants" and a table of rejected alternatives, so the assistant does not re-propose them.
4. **Copilot instruction files** carry the equivalent Angular/TypeScript rules for Copilot (standalone by default, Signals, accessibility). The root `.github/copilot-instructions.md` still points at `/docs/architecture/*.md`, which do not exist (the guides are the `AGENTS.md` files).

### Skills

`.claude/skills/` (mirrored in `.agents/skills/`) holds **25 engineering-workflow skills** installed from the open-source `addyosmani/agent-skills` repository. `skills-lock.json` pins each one to its source path and a content hash so versions are reproducible. A skill is a `SKILL.md` that the assistant loads on demand when the task matches its description, so the process is applied consistently instead of improvised each time.

| Phase | Skills |
|---|---|
| Define | `idea-refine`, `interview-me`, `spec-driven-development`, `planning-and-task-breakdown` |
| Build | `incremental-implementation`, `test-driven-development`, `api-and-interface-design`, `frontend-ui-engineering`, `source-driven-development`, `context-engineering` |
| Verify | `debugging-and-error-recovery`, `browser-testing-with-devtools`, `doubt-driven-development`, `constraint-driven-development` |
| Review | `code-review-and-quality`, `code-simplification`, `security-and-hardening`, `performance-optimization` |
| Ship | `git-workflow-and-versioning`, `ci-cd-and-automation`, `shipping-and-launch`, `observability-and-instrumentation`, `deprecation-and-migration`, `documentation-and-adrs` |
| Meta | `using-agent-skills` (how to pick the right skill) |

### Plugins and agents

- **Plugin:** `.claude/settings.json` enables the `ponytail` plugin (from the git marketplace `DietrichGebert/ponytail`). It biases the assistant toward the smallest working solution (no speculative abstractions, reuse before writing, one runnable check for non-trivial logic) and adds review/audit commands for over-engineering. It matches the "solo developer, low ceremony" stance of the architecture guides.
- **Agents:** no custom agents are defined in the repo (no `.claude/agents/`). Claude Code's built-in subagents are available but not configured here. The `.agents/` folder only mirrors the skills for other agent tools.
- **Permissions:** `frontend/.claude/settings.local.json` is a local allowlist of pre-approved commands (reading the backend, `dotnet build`, directory listings) so routine read-only actions do not prompt.

### Working agreement (suggested)

- The `AGENTS.md` files are the single source of truth; change architecture there first, then code.
- The assistant proposes, the developer reviews every diff and runs the build/tests; CI only builds today (no tests), so review matters.
- Never put secrets or real credentials in prompts, `CLAUDE.md`/`AGENTS.md` or skills. The app's secrets live in Key Vault and App Service settings.

### Housekeeping found

- `.vs/CopilotSnapshots/` (115 files of IDE-generated snapshots) is committed. Add `.vs/` to `.gitignore` and remove it from the index.
- `.claude/skills` and `.agents/skills` are duplicates; keep one if your tools allow, or accept the mirror as the cost of supporting several agents.
- `frontend/.claude/settings.local.json` is a *local* settings file and normally should not be committed.

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
