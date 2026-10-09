# Backend Architecture Decisions — PetSitting App

> **Purpose of this file:** This document is the source of truth for backend architectural decisions on this project. Any AI assistant (Claude, Copilot, ChatGPT, etc.) generating or reviewing backend code MUST follow these decisions. Do not suggest alternative architectures (e.g. CQRS/MediatR, generic Repository/UoW-per-entity, a project per subsystem) unless explicitly asked to reconsider the architecture itself.

> **History:** The architecture was changed from CQRS + MediatR to Controller → Manager → Repository, and the code migration is complete (all three subsystems, MediatR removed). Decision record: `docs/DOCUMENTATION.md`, section "Why Controller → Manager → Repository".

## Stack
- C# / ASP.NET Core
- Entity Framework Core
- DataAnnotations on the API request records for input validation (checked by `[ApiController]` before the action runs)

## Context
- Solo developer, long-term project (multi-year horizon).
- Domain has genuinely complex business rules (scheduling conflicts, cancellation policies, pricing, sitter-pet matching), not just CRUD.
- Priority: fast navigation and low cognitive overhead when returning to code after weeks/months away, over strict multi-developer governance.

## Core Architectural Decisions

### 1. Subsystems (vertical split by area)
The app is split into **subsystems**, one per business area: `Users` (accounts, authentication, profile, avatar, refresh tokens), `Pets`, `Availability`, and later `Bookings`, etc. A subsystem owns its data, its rules and its use cases.

Subsystems are **folders inside the existing layer projects**, not separate projects:

```
PetSitting.Domain/<Subsystem>/            entities, aggregate roots, value objects
PetSitting.Application/<Subsystem>/       manager (+ interface), repository interfaces, DTOs, engines/helpers
PetSitting.Infrastructure/Persistence/Repositories/<Subsystem>/   repository implementations
PetSitting.Api/Controllers/               one controller per subsystem (or per sub-area, e.g. AuthController)
```

**Why:** Navigating by area keeps related code together; keeping the four projects avoids csproj and DI ceremony and keeps one EF migration history. The boundary is enforced by the rules in decision 3, by convention and review, not by the compiler.

### 2. Request flow: Controller → Manager → Repository
```
Controller        HTTP only: bind and shape-validate the request (DataAnnotations), [Authorize], take UserId from the JWT, call the manager, map Result to HTTP status
Manager           one per subsystem (UsersManager, PetsManager, ...): use-case orchestration, business/database checks, ownership checks, logging
Engine / Helper   pure business logic shared by 2+ managers (e.g. PricingEngine). Created only when sharing is real, never up front
Repository        EF Core access for one aggregate root
Domain            aggregates enforce their own invariants
```
- A manager has **one public method per use case** (`CreatePetAsync`, `CancelBookingAsync`). If a manager grows past roughly 15 methods or mixes unrelated areas, split it by sub-area (e.g. `AuthManager` and `ProfileManager` inside `Users`), not into a god class.
- Managers are registered explicitly in `PetSitting.Api/Program.cs`. Nothing is found by assembly scan.
- **Naming:** a manager is injected as a field named after its interface without the `I`, in camelCase: `IPetsManager` → `_petsManager`, `IUsersManager` → `_usersManager` (constructor parameter `petsManager`). A repository is injected as `_<subsystem>Repository`: `IPetRepository` → `_petsRepository`, `IUserRepository` → `_usersRepository`, `ISitterAvailabilityRepository` → `_availabilityRepository`. Never a bare noun like `_pets` or `_users` (it reads like a collection), and never a generic `_repository`. Other injected services follow the same rule (field = interface name without the `I`, camelCase): `IBlobService` → `_blobService`, `IJwtTokenService` → `_jwtTokenService`.
- No mediator, no dispatcher, no pipeline behaviors. Cross-cutting concerns live where they belong: input-shape validation on the request records (DataAnnotations), authentication/authorization via ASP.NET attributes, exception-to-HTTP mapping in the global exception handler, logging via `ILogger`.

### 3. Subsystem boundaries
- Each subsystem exposes **one public contract**: `I<Subsystem>Manager` plus the DTOs it returns. That interface is what controllers and other subsystems use.
- A manager may call **another subsystem's manager interface**. It must NOT use another subsystem's repository, its domain entities, or its `DbSet`s. Use the DTO the other manager returns.
- No circular manager dependencies. If A needs B and B needs A, the shared logic belongs in a third place (an engine, or a new subsystem).
- Foreign keys across subsystems are allowed in the database (e.g. `Pet.UserId`), but new code must not add navigation properties across subsystems; reference the other aggregate by id. (The existing `User.Pets`, `Pet.User` and `SitterAvailability.User` navigations predate this rule; remove them when you next change those entities, with a migration check.)
- One shared `AppDbContext` and one migration history.

### 4. Domain-Driven Design (tactical patterns only, not full strategic DDD)
The `Domain` project contains:
- **Entities & Aggregate Roots** (e.g. `Booking`, `User`, `Pet`) that enforce their own invariants. Business rules live as methods on the aggregate (e.g. `booking.Cancel()` returns failure if within the no-cancellation window), not scattered across managers.
- **Value Objects** for concepts without identity (e.g. `DateRange`, `Money`) instead of primitive obsession.
- **No domain events** for now. They were tied to MediatR notifications and nothing used them. Add them (with a small in-process dispatcher) when a real side effect needs decoupling.

**Why:** The domain complexity (conflicts, policies, matching) justifies explicit modeling. Business rules live in the aggregate; managers orchestrate (load, call domain methods, save), they do not decide.

### 5. Repository pattern — one per Aggregate Root ONLY
- A repository exists **only for an aggregate root** (`PetsRepository` for `Pet`, which owns `PetPicture`; `UserRepository` for `User`, which owns `RefreshToken`). Child entities have no repository: load the root, call a method on it, save the root.
- **Not** a generic `IRepository<T>`. Write the specific methods the use cases need.
- Return materialized results (`Task<Pet?>`, `Task<List<Pet>>`), never `IQueryable`. Read-only screens may return DTO projections (`.Select(...)`) from the same repository.
- The interface lives in `Application/<Subsystem>/`, the implementation in `Infrastructure`. The interface is required by the dependency direction (Application cannot reference Infrastructure), not by test fakes.
- **A repository method persists its own aggregate** (calls `SaveChangesAsync`). The aggregate is the transaction boundary. No separate Unit of Work abstraction. If a future use case must change two aggregates atomically, decide it then (e.g. a single `SaveChanges` through a small transaction abstraction) and record it here.

**Explicitly rejected:** Generic Repository + Unit of Work around every entity. `DbContext`/`DbSet` already provides this.

### 6. Validation
- Input-shape rules (required, lengths, ranges, formats, enum values) are DataAnnotations on the request records in `Api/Contracts/Requests.cs`, with the user-facing `ErrorMessage`. `[ApiController]` rejects a bad body with 400 before the action runs; `InvalidModelStateResponseFactory` in `Program.cs` turns it into `{ success:false, message }` with the first error. Attributes on a record go on the constructor **parameter** (`[property:]` validation attributes throw at runtime); only `[property: JsonConverter]` needs `property:`.
- Enum fields are typed enums (nullable + `[Required]` on the request so a missing value is not silently the first member) and are mapped to the manager input in the controller. Managers receive already-valid input and do not re-validate its shape.
- Business and database-dependent checks stay in the manager and return a `Validation`/`Conflict`/... failure `Result`.
- A caller that bypasses the controller (a future job, another manager) must validate its own input; the manager does not.

### 7. Result pattern for expected failures
- Manager methods return `Result<T>` (or `Result`). `Failure` carries an **error kind** (`NotFound`, `Validation`, `Unauthorized`, `Forbidden`, `Conflict`) and a message. The controller maps the kind to HTTP status (404, 400, 401, 403, 409) in one shared place in `ApiControllerBase` (`ToAction`). The success message and JSON wrapper (`{ success, message, ... }`, records in `Api/Contracts/Responses.cs`) are built in the controller, so managers return plain DTOs.
- Never decide the HTTP status by searching the message text.
- Exceptions are only for unexpected failures (database down, programming errors); the global exception handler turns them into `500`.

### 8. Security rules
- `UserId` comes from the JWT (`ApiControllerBase.UserId`), passed to the manager as a parameter. Never from a request body.
- The manager checks that the loaded record belongs to that user. Return "not found" for both missing and foreign records so ids cannot be probed.
- Never return EF entities from a manager: return DTOs (`User` has `PasswordHash`).

## Explicitly Rejected Alternatives (and why)

| Alternative | Why it was rejected for this project |
|---|---|
| **CQRS + MediatR** (previous architecture) | Per use case it needs a request, a handler, a validator and implicit pipeline behaviors, and "go to definition" on `Send` does not reach the handler. It decouples callers in a single-process app where nothing else dispatches the requests. MediatR 13+ is commercially licensed. The only real benefit (validation as a pipeline step) is replaced by one explicit call in the manager. |
| **Generic Repository + Unit of Work (per entity)** | Redundant on top of EF Core's built-in `DbContext`/`DbSet`; adds indirection without corresponding benefit. |
| **One repository per table** | Lets any code insert child rows and bypass the aggregate's invariants. |
| **A project per subsystem** | Compiler-enforced boundaries, but more csproj files, cross-references and DI wiring than a solo project needs. Revisit if boundaries are repeatedly violated. |
| **Strict iDesign** (volatility decomposition, closed layers) | Not adopted as a doctrine. The Manager/Engine terms are borrowed, the strict layer rules are not. |

## Structure

```
PetSitting.Domain/
    Users/        User.cs, RefreshToken.cs, UserRole.cs
    Pets/         Pet.cs, PetPicture.cs, PetGender.cs, PetType.cs
    Availability/ SitterAvailability.cs
    Common/       Result.cs

PetSitting.Application/
    Pets/
        IPetsManager.cs            (public contract of the subsystem)
        PetsManager.cs
        IPetRepository.cs
        PetDto.cs, PetInput.cs
        Engines/                   (only if logic is shared by 2+ managers)
    Users/        ...same shape
    Availability/ ...same shape
    Common/       FileUpload, IBlobService, IJwtTokenService

PetSitting.Infrastructure/
    Persistence/
        AppDbContext.cs
        Repositories/Pets/PetRepository.cs
        Repositories/Users/UserRepository.cs
    Services/     BlobService, JwtTokenService
    Migrations/

PetSitting.Api/
    Controllers/  PetsController, AuthController, AvailabilityController (thin)
    Contracts/Requests.cs, Responses.cs
    ApiControllerBase.cs
    Program.cs
```

## Rules for AI Assistants Working on This Codebase
1. A new use case is a new method on the owning subsystem's manager (and interface), plus a request record with DataAnnotations in `Api/Contracts/Requests.cs` if it takes input. Do not create per-use-case classes or folders.
2. Business rules and invariants belong on the Domain aggregate, not in the manager or controller. Managers orchestrate.
3. Controllers are thin: no logic, no repository access, no EF.
4. Never use another subsystem's repository or entities. Call its `I<Subsystem>Manager`.
5. Do not introduce a generic `IRepository<T>`, a repository for a child entity, or a Unit of Work abstraction.
6. Do not reintroduce MediatR, a mediator, or pipeline behaviors; do not propose a project per subsystem, unless the user explicitly asks to revisit the architecture itself.
7. Do not add an Engine or Helper until two managers actually need the same logic. Do not add an interface for anything that is not a manager or a repository.
8. Use `Result<T>` with an error kind for expected failures; exceptions only for unexpected ones. Never infer HTTP status from message text.
9. Register every new manager, repository and service in `PetSitting.Api/Program.cs`. Nothing is discovered automatically.
10. Take `UserId` from the JWT (`ApiControllerBase.UserId`), never from a request body, and check that the loaded record belongs to that user. Return DTOs, never entities.
11. Name injected managers `_<subsystem>Manager` (`_petsManager`) repositories `_<subsystem>Repository` (`_petsRepository`) and other services after their interface (`_blobService`), never `_pets`/`_users`/`_repository`/`_blobs`.
12. This is backend-only guidance; see `frontend/AGENTS.md` for frontend rules.
