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

### MediatR in detail

**What it is.** MediatR is an in-process mediator: the caller builds a small request object (a `record` implementing `IRequest<TResponse>`) and calls `_mediator.Send(request)`. MediatR finds the single `IRequestHandler<TRequest,TResponse>` for that type and runs it. The caller never references the handler class, and the handler never knows who called it. It adds no network hop or message broker; it is a typed dispatch table inside the API process.

**How it is wired here**
- Registration (`Program.cs`): `AddMediatR(c => c.RegisterServicesFromAssemblyContaining(typeof(LoginCommand)))` scans the Application assembly and registers every handler automatically, so adding a use case needs no DI edits. `AddOpenBehavior(typeof(ValidationBehavior<,>))` adds the pipeline step described below.
- Packages: version 12.4.0 in `PetSitting.Api` and `PetSitting.Application`.
- Naming: every use case is a folder `Features/<Area>/<UseCase>/` containing `XCommand` or `XQuery` (the request), `XCommandHandler` (the logic) and optionally `XCommandValidator`. 17 handlers exist today: Identity (8), Pets (7) and Availability (2).
- Commands change state (`Login`, `Register`, `CreatePet`, `UpsertAvailability`, ...); queries only read (`GetPets`, `GetPet`, `GetAvailability`, `GetCurrentUser`). That is the CQRS split from the architecture guide, here at the code-organisation level only (same database for reads and writes).

**What a request goes through**

```
AuthController.Login(command)
   └─ mediator.Send(command)
        └─ ValidationBehavior<LoginCommand,AuthResponse>   ← pipeline step
             ├─ runs every IValidator<LoginCommand> (FluentValidation)
             ├─ any failure → throws ValidationException → Program.cs handler → 400 { success:false, message }
             └─ ok → LoginCommandHandler.Handle(...) → repositories → EF Core
```

`ValidationBehavior` is the only pipeline behavior today. Because it wraps *every* request, no handler repeats validation code, and a new command gets validation just by having a validator class next to it.

**Why we use it (the reasons in this project)**
1. **Thin controllers.** A controller action is "build request, send, map result to HTTP status". Business logic cannot leak into controllers, which the guide requires.
2. **One slice per use case.** Request, validator and handler are one folder, so the whole flow of "upload pet picture" is readable in one place. MediatR's one-request-one-handler rule is what makes that folder structure natural, instead of large `PetService` / `AuthService` classes. This repo actually shows the before and after: the legacy `backend/Identity/Services/AuthService.cs` and `Pets/Services/PetService.cs` were service classes holding many methods; the new code splits them into small handlers.
3. **Cross-cutting concerns in one place.** Validation today; logging, authorization checks or transactions can be added later as further behaviors without touching any handler (the guide lists these as intended).
4. **Testability.** A handler depends only on repository/service interfaces, so it can be unit-tested with fakes, with no HTTP stack.
5. **Low ceremony for a solo developer.** Auto-registration and convention naming mean very little wiring to maintain.

**Why it was "needed"**
It is not technically required: the same app could be written with plain services and direct calls. It was chosen deliberately in `backend/AGENTS.md` (decision 3, "CQRS via MediatR"), together with vertical slices, because the planned domain (bookings, scheduling conflicts, cancellation policies, pricing, matching) will grow many use cases that need shared cross-cutting rules. The guide also lists it as part of the stack for domain events (`INotification`) so aggregates can raise events like `BookingConfirmed` without knowing about emails or notifications.

**Where the current code does not yet use its full value**
- No domain events are published yet. `DomainEvent` exists in the Domain project, but it does not implement `INotification` and nothing calls `Publish`.
- Only one behavior exists. There is no logging, authorization or transaction behavior (the behaviors README lists them as planned).
- Handlers return response records (`AuthResponse`, `PetResponse`, `AvailabilityResponse`) with `Success`/`Message`; the guide's `Result<T>` exists in the Domain but is unused by handlers. Controllers also infer 404 vs 400 by searching the message text for "not found", which is fragile; typed failure results would fix it.
- Authorization of ownership (a user may only touch their own pets) is repeated inside each handler (`pet.UserId != request.UserId`), a good candidate for a behavior or a shared check.
- Several handlers (`Login`, `Register`, `ChangeRole`...) are called by exactly one controller action, so the indirection mainly buys consistency, not reuse.

**Trade-offs to know**
- *Indirection:* "go to definition" on `Send(...)` does not jump to the handler; you find it by naming convention or search. Mitigated by the folder convention.
- *Hidden dependencies:* behaviors run implicitly, so a request can fail before reaching its handler; the exception-to-400 mapping in `Program.cs` is what makes that visible to clients.
- *Licensing:* MediatR 13 and later moved to a commercial license model for larger companies, while 12.x remains Apache-2.0. The Api/Application projects pin 12.4.0, but the legacy root `backend.csproj` references 14.2.0. Check the current license terms before upgrading, or consider a small hand-written dispatcher (the pattern is only a few dozen lines) if that becomes a concern.
- *Runtime failures:* a request without a registered handler only fails when sent, not at compile time.

### Navigating and debugging one request (worked example: "add a pet")

Paths are relative to `backend/`. Every use case follows the same ten stops; replace `Pets/CreatePet` with any other slice.

| # | Stop | File | What to look at / where to put a breakpoint |
|---|---|---|---|
| 0 | Browser call | `frontend/src/app/services/pet.service.ts` (`createPet`), triggered from `frontend/src/app/profile/profile.component.ts` | URL, JSON body, `Authorization` header. Check the Network tab first: status code and response JSON tell you which stop to jump to. |
| 1 | Pipeline (auth) | `PetSitting.Api/Program.cs` | CORS, `UseAuthentication`/`UseAuthorization`. A `401` never reaches the controller: bad/expired token, or JWT key/issuer/audience mismatch. |
| 2 | Controller | `PetSitting.Api/Controllers/PetsController.cs` → `CreatePet` | **Best first breakpoint.** Confirms the request arrived and `UserId` (from `ApiControllerBase.UserId`, the JWT claim) is right. |
| 3 | Request body shape | `PetSitting.Api/Contracts/Requests.cs` (`PetRequest`) | If a field is null/default, the JSON names do not match this record (model binding failed before your code ran, a `400` with ASP.NET's own error shape). |
| 4 | The request object | `PetSitting.Application/Features/Pets/CreatePet/CreatePetCommand.cs` | The record the controller builds. It declares its response type: `IRequest<PetResponse>`, which is your pointer to the handler's return type. |
| 5 | Validation | `.../CreatePet/CreatePetCommandValidator.cs`, run by `PetSitting.Application/Common/Behaviors/ValidationBehavior.cs` | Breakpoint in `ValidationBehavior.Handle` to see all validators and failures. Failure throws `ValidationException`, mapped to `400 { success:false, message }` by the exception handler in `Program.cs` (the handler is never reached). |
| 6 | **The handler** | `.../CreatePet/CreatePetCommandHandler.cs` → `Handle` | **Where the logic is.** Business decisions and "Pet not found"-style failures are returned from here as `PetResponse(false, ...)`. |
| 7 | Repository interface | `PetSitting.Application/Abstractions/Repositories/IPetRepository.cs` | The contract the handler calls. |
| 8 | Repository implementation | `PetSitting.Infrastructure/Persistence/Repositories/PetRepository.cs` | The EF Core query/`SaveChangesAsync`. SQL errors surface here. |
| 9 | Model and schema | `PetSitting.Domain/Pets/Pet.cs`, `PetSitting.Infrastructure/Persistence/AppDbContext.cs`, `PetSitting.Infrastructure/Migrations/` | Entity shape, relations, delete behavior; a missing column means a missing migration. |
| 10 | Response | `PetSitting.Application/Features/Pets/PetResponse.cs` (contains `PetDto` and `ToDto()`), then back in the controller | The controller maps `Success`/`Message` to HTTP status (`Respond()` in `ApiControllerBase.cs`: message containing "not found" → 404, else 400). |

**How to find any handler without remembering the layout**
- From a controller action, take the command name (e.g. `CreatePetCommand`) and use *Go to Definition* on it; its folder holds the handler and validator side by side. `Shift+F12` / *Find All References* on the command class lists the controller call plus the handler.
- Search for the class name `CreatePetCommandHandler` (Visual Studio: `Ctrl+T`; VS Code: `Ctrl+P`), or search for `IRequestHandler<CreatePetCommand`.
- In a handler, *Go to Definition* on `_pets.AddAsync` lands on the interface; use *Go to Implementation* (`Ctrl+F12`) to reach `PetRepository`.
- Handler folders are named after the controller action: action `UploadPicture` → `UploadPetPicture`, `GetMe` → `GetCurrentUser`, `UpdateMe` → `UpdateProfile`, `UpsertAvailability` → `UpsertAvailability`. The controller is the index of the feature.

**Debugging checklist by symptom**

| Symptom | Likely stop | Check |
|---|---|---|
| 401 | 1 | Token missing/expired, `Jwt:*` settings, `Jwt--Key` loaded? (backend log "Loaded Jwt:Key from Key Vault.") |
| 400 with `{ success:false, message }` | 5 or 6 | Message text: validator rule (step 5) or handler failure (step 6). Search the repo for that exact string; it points to the file. |
| 400 with ASP.NET `errors` object | 3 | Body does not match the request record |
| 404 | 6 / 10 | Handler returned a "... not found." message (also what a wrong-owner pet looks like) |
| 500 "An unexpected error occurred." | 6-8 | Exception swallowed by the global handler. Run locally and read the console, or break on exceptions; the message never reaches the client. |
| Works locally, fails in Azure | config | App settings / Key Vault (section 1.6), DB connectivity and migrations |

**Debugging tips specific to MediatR**
- Put breakpoints at stop 2 and stop 6 and step with *Step Into* from `Send`: it goes through `ValidationBehavior` and then into the handler.
- Behaviors run for every request; a breakpoint in `ValidationBehavior.Handle` with a condition such as `request.GetType().Name == "CreatePetCommand"` isolates one use case.
- "Handler not found" (`InvalidOperationException: No service for type IRequestHandler<...>`) means the handler is not in the scanned assembly (`PetSitting.Application`) or its class does not implement the interface.
- Handlers are registered by scanning, so a typo in the generic types compiles fine and fails only when the request is sent.

**Adding a new use case (same map in reverse)**
1. Create folder `Application/Features/<Area>/<UseCase>/` with `...Command.cs` (or `...Query.cs`), `...Handler.cs`, optional `...Validator.cs`. No DI registration needed.
2. Add the controller action in `Api/Controllers/<Area>Controller.cs`, and a request record in `Api/Contracts/Requests.cs` if it takes a body.
3. If it needs new data access, add the method to the repository interface (`Application/Abstractions/Repositories`) and implement it in `Infrastructure/Persistence/Repositories`.
4. If the schema changes, add a migration (`dotnet ef migrations add <Name>` with `PetSitting.Infrastructure` as the project and `PetSitting.Api` as the startup project).
5. Add the call in the frontend service (`frontend/src/app/services/`) and the model in `models/user.model.ts`.

### Implementing a feature end to end

The worked example is **illustrative** ("an owner requests a booking from a sitter"): nothing named `Booking` exists yet. It was picked because it touches every layer, including a real domain rule. Paths are relative to `backend/` unless prefixed `frontend/`. Follow the order below, inside-out, so each step compiles on its own and you can stop after any step.

#### Before you start (5 minutes, saves hours)
1. **Write down the use case in one sentence and its failure cases.** "Owner requests a booking for a pet from a sitter for a date range. Fails if: sitter not found or not a Sitter, pet not yours, dates in the past or end before start, sitter not available, overlaps an accepted booking."
2. **Decide command or query.** Changes data → command. Only reads → query.
3. **Decide who may call it.** Which role, and which resource must belong to the caller. Authorization is part of the use case, not an afterthought.
4. **Name the slice** `<Verb><Noun>` (`RequestBooking`) and the area folder (`Bookings`). Everything below is named from it.
5. **Check the architecture guides** (`backend/AGENTS.md`, `frontend/AGENTS.md`): a new area means a new `Features/<Area>/` folder; rules on aggregates; no generic repository.

#### Step 1: Domain (`PetSitting.Domain/Bookings/`)
- Create the aggregate `Booking.cs` (Id, OwnerId, SitterId, PetId, start/end, `BookingStatus`, CreatedAt, UpdatedAt) and `BookingStatus.cs` (enum: Requested, Accepted, Declined, Cancelled, Completed).
- Put **rules on the entity**, not in the handler: methods such as `Accept()` and `Cancel()` that refuse illegal state changes. (Today's entities only have public setters; for bookings prefer methods so the rules cannot be bypassed.)
- Use a value object for concepts without identity (`DateRange`, which enforces "start < end" in its constructor) rather than two loose `DateTime`s.
- Add navigation properties or foreign keys to `User` / `Pet` only if you need them.
- *Don't forget:* store times in **UTC** (the code uses `DateTime.UtcNow`); enums are stored as **int**, so never reorder or renumber existing enum members.

#### Step 2: Persistence (`PetSitting.Infrastructure/Persistence/`)
1. `AppDbContext.cs`: add `DbSet<Booking> Bookings` and the relationship configuration in `OnModelCreating` (foreign keys, `OnDelete` behavior). **Think about delete behavior:** deleting a user or pet that has bookings should not silently cascade away booking history; prefer `Restrict` and decide the product rule.
2. Generate the migration:
   ```bash
   dotnet ef migrations add AddBookingsTable -p PetSitting.Infrastructure -s PetSitting.Api
   ```
3. **Open the generated migration file and read it.** Check that it only contains your change, plus the column types (`datetime2`, `nvarchar`) and indexes. Add indexes for columns you will filter on (`SitterId`, date range).
4. *Don't forget:* the API runs `Database.Migrate()` **automatically at startup, including in Azure**. A bad or destructive migration is applied to the production database on the next deploy. Test it on a local database first, and make breaking changes in two steps (add, deploy, then remove).

#### Step 3: Application contracts and data access
- `PetSitting.Application/Abstractions/Repositories/IBookingRepository.cs`: only the methods the use cases need (`GetByIdAsync`, `AddAsync`, `HasOverlapAsync(sitterId, range)`). One repository per **aggregate root**, no generic `IRepository<T>`.
- `PetSitting.Infrastructure/Persistence/Repositories/BookingRepository.cs`: the implementation (`Include` what the handler needs; return `null` when not found).
- **Register it in `PetSitting.Api/Program.cs`:** `builder.Services.AddScoped<IBookingRepository, BookingRepository>();`. Handlers, validators and MediatR behaviors are found by assembly scanning, but **repositories and services are not**. A missing line compiles fine and fails at runtime with "Unable to resolve service for type IBookingRepository".

#### Step 4: The slice (`PetSitting.Application/Features/Bookings/RequestBooking/`)
Create these files (namespaces follow the folder):
1. `RequestBookingCommand.cs`: `record RequestBookingCommand(int UserId, int SitterId, int PetId, DateTime StartUtc, DateTime EndUtc) : IRequest<BookingResponse>;`
   - **`UserId` comes from the JWT, never from the request body** (the controller fills it in). This is the most important security rule in the app.
2. `RequestBookingCommandValidator.cs` (FluentValidation): **shape and format only**: ids > 0, end after start, start not in the past, text lengths. Anything that needs the database belongs in the handler.
3. `RequestBookingCommandHandler.cs`: load what you need through repositories and check, in this order, using the same style as today (`new BookingResponse(false, "...")`):
   1. the caller owns the pet (return "Pet not found." for both missing and foreign pets, so ids cannot be probed),
   2. the target user exists and has role Sitter,
   3. the sitter's availability and capacity allow it (reuse `ISitterAvailabilityRepository`, do not copy the data),
   4. no overlapping accepted booking,
   5. create the aggregate through its constructor or factory so the domain rules run, `AddAsync`, log with `ILogger`, return success with a DTO.
   - Keep the handler an **orchestrator**: load, call domain methods, save. Rules about the booking itself live on `Booking`. Don't call other handlers via `Send` from inside a handler.
4. `Features/Bookings/BookingResponse.cs`: the response record (`Success`, `Message`, `BookingDto? Booking`), the `BookingDto`, and a `ToDto()` mapping extension, exactly like `Features/Pets/PetResponse.cs`. **Never return the entity**: it carries navigation properties and sensitive fields (for example `PasswordHash` on `User`).
- A query (`GetMyBookings`) is the same minus the validator: the handler reads and returns DTOs. Never modify state in a query.
- Failure convention: return `Success=false` with a message. **Use the words "not found" only for genuine 404 cases**, because `ApiControllerBase.Respond()` maps that phrase to 404 and everything else to 400.

#### Step 5: API (`PetSitting.Api/`)
1. `Contracts/Requests.cs`: add `BookingRequest(int SitterId, int PetId, DateTime StartUtc, DateTime EndUtc)`. Property names are the JSON contract with the frontend (camelCase on the wire).
2. `Controllers/BookingsController.cs`: `[ApiController]`, `[Route("api/[controller]")]`, **`[Authorize]`** (add a role restriction if only Owners may call), inherit `ApiControllerBase`, inject `IMediator`. The action only builds the command with `UserId`, sends it, and maps the result: `CreatedAtAction` for creates, `Respond(...)` for lookups and updates. No logic here.
3. Try it before writing any frontend: add the call to `backend/backend.http` (or use OpenAPI in Development) and test the success case, each failure case, no token (401), and someone else's data.
   - No CORS or DI changes are needed for a new route. If the endpoint accepts files, set explicit size limits and validate the content type like `BlobService` does.

#### Step 6: Frontend (`frontend/src/app/`)
1. `models/user.model.ts` (or a new model file when the feature grows): types mirroring `BookingDto` and the request/result shapes. Keep names identical to the JSON (camelCase).
2. `services/booking.service.ts`: follow `pet.service.ts` (URL from `environment.apiRootUrl`, `Authorization` header, `Observable` return). If you add more services, consider one HTTP interceptor for the header (see issue 7 in 1.9) instead of repeating it.
3. UI: a component with a reactive form (`Validators` mirroring the server rules for fast feedback, but **the server remains the authority**) and `signal`s for `isSaving`, `errorMessage` and the result. Show the server's `message` on failure. Register the route in `app.routes.ts` under the authenticated layout, and add a navbar link if needed.
4. The frontend guide's target structure is `features/bookings/` with a facade that owns the signals and HTTP calls, and components that only talk to the facade. Existing code is flatter; **new features should follow the guide**, and existing screens can stay until you refactor them deliberately.
5. Handle loading, empty, error and success states, plus 401 (token expired, send to login) explicitly; never leave a spinner running on failure.

#### Step 7: Verify and ship
- Run `dotnet build backend/backend.slnx` and, in `frontend/`, `npm run build` (the same two things CI runs). **CI does not run tests**, so your manual verification is the safety net.
- Add tests for domain rules (`Booking.Cancel()`, `DateRange`) when you build them. There is no test project yet, so create one (for example `PetSitting.Domain.Tests` with xUnit) when the first real rules appear. Handlers are easy to test with fake repositories.
- Do a manual end-to-end pass in the browser: happy path, each failure message, a page refresh, and a second user trying to reach the first user's data.
- New configuration (keys, URLs) goes to Key Vault or App Service settings, **never** into `appsettings.json` or the environment files; add a placeholder to `.env.example` for local Docker.
- Commit in small slices (domain, persistence, slice, API, frontend). A merge to `main` builds, pushes images and **deploys straight to production**, running the migration on startup.
- Update this document (API table, data model, functional section) and the `AGENTS.md` files if you changed an architectural rule.

#### Quick checklist (copy into the PR description)
- [ ] Use case and failure cases written down; command vs query decided
- [ ] Domain rule lives on the entity or value object, not only in the handler
- [ ] `DbSet`, relationships and **migration generated, read and tested locally**
- [ ] Repository interface, implementation and **`AddScoped` in `Program.cs`**
- [ ] Command/query, validator, handler, response, DTO and `ToDto()` in one folder
- [ ] `UserId` from the JWT; **ownership and role checked**; no entities returned
- [ ] "not found" wording only for real 404s
- [ ] Controller is `[Authorize]` and thin; request record added
- [ ] Tried through `backend.http`, including 401 and other-user cases
- [ ] Frontend model, service, component, route; all states handled
- [ ] Both builds pass; secrets and config not committed; docs updated

#### Common mistakes
| Mistake | What happens |
|---|---|
| Repository not registered in `Program.cs` | Runtime DI error on the first request, not at build time |
| Taking `UserId` from the request body | Any user can act as another user |
| Checking that the record exists but not that it belongs to the caller | Cross-user data access (the existing pet handlers show the right pattern) |
| Reordering enum members | Existing rows silently change meaning (stored as int) |
| Returning the EF entity | Leaks fields and can cause JSON cycles |
| Database-dependent checks in the validator | Validators should only check input shape |
| Editing a migration after it was deployed | Production and the migration history diverge; add a new migration instead |
| `DateTime.Now` | Wrong across time zones; use `DateTime.UtcNow` |
| Forgetting the frontend error state | The user sees a frozen UI on 400/401 |

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

1. **Password hashing is unsalted SHA-256** (`RegisterCommandHandler`, `LoginCommandHandler`). Should become PBKDF2/Argon2/bcrypt (e.g. ASP.NET `PasswordHasher`). Existing hashes need a migration path. *Still SHA-256.*
2. **CORS is `AllowAnyOrigin`**; restrict to the frontend origin(s). *Still `AllowAnyOrigin`.*
3. **A Google Maps API key is committed** in `frontend/src/environments/*.ts` (and in git history). Restrict it by HTTP referrer and quota in Google Cloud, and consider rotating it. *Still present in both environment files.*
4. ~~`docker-compose.yml` is stale~~ **Fixed (uncommitted):** frontend now builds from `./frontend` on `4200:80`, API on `5072:8080` with `ConnectionStrings__DefaultConnection` and `Jwt__Key`, and the SA password moved to `.env` (`.env.example` added, `.env` git-ignored, `.gitignore` paths corrected). Remaining: the old password `Strong!Passw0rd123` stays in git history (local-only, low risk), and compose does not pass `AzureBlobStorage__ConnectionString`, so uploads fail locally unless you add it.
5. ~~`Program.cs` logs the connection string~~ **Fixed (uncommitted)** in both `PetSitting.Api/Program.cs` and the legacy `backend/Program.cs`.
6. **Public blob container** (`PublicAccessType.Blob`): every uploaded image is world-readable by URL. Acceptable for avatars; reconsider for anything sensitive.
7. **Tokens in `localStorage`** (XSS exposure) and **no refresh-on-401 / HTTP interceptor**: after 60 minutes API calls fail until the user logs in again.
8. **Email is not verified** (`IsEmailConfirmed` is never set to true); **any user can switch to Sitter** freely via `change-role` (by design today, but a trust issue once sitters are searchable).
9. **No tests run in CI**, and the backend has no test project.
10. **Legacy duplicate backend code** in `backend/` root (`Identity/`, `Pets/`, `Availability/`, `Controllers/`, `Models/`, `Program.cs`, `DbContext.cs`, `backend.csproj` with older package versions such as MediatR 14 and FluentValidation 12) is not built by the Dockerfile or `backend.slnx` and can be deleted after confirming nothing references it. `Message` entity / `Messages` table is also unused. *Still present (`Identity/`, `Pets/`, `Availability/`, `Controllers/`, `Models/`, `Data/`, root `Program.cs`, `DbContext.cs`, `backend.csproj`).*
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
3. **`backend/AGENTS.md` and `frontend/AGENTS.md`** are the architecture decision records (vertical slices, CQRS/MediatR, aggregate-only repositories, `Result<T>`; Angular standalone components, Signals, facades, no NgRx/Nx apps). They end with explicit "Rules for AI Assistants" and a table of rejected alternatives, so the assistant does not re-propose them.
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
