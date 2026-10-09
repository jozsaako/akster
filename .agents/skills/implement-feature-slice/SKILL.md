---
name: implement-feature-slice
description: Implements or extends a backend + frontend feature in the Akster/PetSitting app end to end (domain, EF migration, repository, subsystem manager method, controller, Angular service/component). Use when adding a new use case, endpoint, entity or screen, or when asked "how do I add X" in this repo.
---

# Implement a feature (Akster)

Full detail and rationale: `docs/DOCUMENTATION.md`, section "Implementing a feature end to end". Architecture rules: `backend/AGENTS.md`, `frontend/AGENTS.md` (authoritative, do not contradict). Read the guide section for the files you are about to touch; do not re-derive the layout.

Architecture in one line: **Controller → Manager (one per subsystem) → Repository (one per aggregate root)**. No MediatR, no per-use-case classes.

## Order (inside-out; each step must build)
1. **Plan in one sentence:** use case, failure cases, owning subsystem (`Users`, `Pets`, `Availability`, ...), which role may call it, which resource must belong to the caller. Name the manager method `<Verb><Noun>Async`.
2. **Domain** `PetSitting.Domain/<Subsystem>/`: entity with rules as methods, value objects, UTC times. Never renumber existing enums (stored as int). Reference other subsystems by id only.
3. **Persistence** `PetSitting.Infrastructure/Persistence/`: `DbSet` + relationships in `AppDbContext`, then `dotnet ef migrations add <Name> -p PetSitting.Infrastructure -s PetSitting.Api`. **Read the generated migration.** It auto-applies at API startup, including production.
4. **Repository** (aggregate roots only, none for child entities, no generic `IRepository<T>`): interface in `PetSitting.Application/<Subsystem>/`, implementation in `Infrastructure/Persistence/Repositories/<Subsystem>/`, and **`AddScoped` in `PetSitting.Api/Program.cs`** (nothing is auto-registered). Returns materialized results, persists its own aggregate.
5. **Manager** `PetSitting.Application/<Subsystem>/`: add the method to `I<Subsystem>Manager` and `<Subsystem>Manager`, plus a validator in `Validators/` (input shape only, no DB) that the method calls first. Returns `Result<Dto>`. Use other subsystems only through their manager interface.
6. **API** `PetSitting.Api/`: request record in `Contracts/Requests.cs`, thin `[Authorize]` controller inheriting `ApiControllerBase`; it only passes `UserId` and the request to the manager and maps the `Result`.
7. **Frontend** `frontend/src/app/`: model in `models/`, service following `services/pet.service.ts`, component with reactive form and signals, route in `app.routes.ts`. New features follow the frontend guide (feature folder + facade). Handle loading, error, 401.
8. **Verify:** `dotnet build backend/backend.slnx`, `npm run build` in `frontend/`, try the endpoint via `backend/backend.http` (success, each failure, 401, other user's data). CI runs no tests.

## Non-negotiables
- `UserId` comes from the JWT (`ApiControllerBase.UserId`), **never** from the request body.
- Check ownership/role in the manager; return a `NotFound` failure for both missing and foreign records.
- Expected failures: `Result.Failure(kind, message)` with a kind (`NotFound`, `Validation`, `Forbidden`, `Conflict`). Never pick the HTTP status from message text. Exceptions only for unexpected errors.
- Rules about an entity live on the entity, not in the manager. Managers orchestrate.
- Name injected managers `_<subsystem>Manager` (`_petsManager`, ctor param `petsManager`) repositories `_<subsystem>Repository` (`_petsRepository`) and other services after their interface (`_blobService`), never `_pets`, `_repository` or `_blobs`.
- Never inject another subsystem's repository or use its entities; call its manager interface.
- Return DTOs, never EF entities (`User` has `PasswordHash`).
- No secrets in code, `appsettings.json` or environment files; use Key Vault / App Service settings, placeholder in `.env.example`.
- Merge to `main` deploys to production and runs migrations: keep breaking schema changes two-step.

## Finish
Update `docs/DOCUMENTATION.md` (API table, data model, functional section) and tell the user which files were created or changed.
