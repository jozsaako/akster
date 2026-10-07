---
name: implement-feature-slice
description: Implements or extends a backend + frontend feature in the Akster/PetSitting app end to end (domain, EF migration, repository, MediatR command/query slice, controller, Angular service/component). Use when adding a new use case, endpoint, entity or screen, or when asked "how do I add X" in this repo.
---

# Implement a feature slice (Akster)

Full detail and rationale: `docs/DOCUMENTATION.md`, section "Implementing a feature end to end". Architecture rules: `backend/AGENTS.md`, `frontend/AGENTS.md` (authoritative, do not contradict). Read the guide section for the files you are about to touch; do not re-derive the layout.

## Order (inside-out; each step must build)
1. **Plan in one sentence:** use case, failure cases, command vs query, which role may call it, which resource must belong to the caller. Name it `<Verb><Noun>`.
2. **Domain** `PetSitting.Domain/<Area>/`: entity with rules as methods, value objects, UTC times. Never renumber existing enums (stored as int).
3. **Persistence** `PetSitting.Infrastructure/Persistence/`: `DbSet` + relationships in `AppDbContext`, then `dotnet ef migrations add <Name> -p PetSitting.Infrastructure -s PetSitting.Api`. **Read the generated migration.** It auto-applies at API startup, including production.
4. **Repository** (aggregate roots only, no generic `IRepository<T>`): interface in `PetSitting.Application/Abstractions/Repositories/`, implementation in `Infrastructure/Persistence/Repositories/`, and **`AddScoped` in `PetSitting.Api/Program.cs`** (repositories/services are NOT auto-registered; handlers and validators are).
5. **Slice** `PetSitting.Application/Features/<Area>/<UseCase>/`: `...Command|Query.cs`, `...Handler.cs`, `...Validator.cs` (input shape only, no DB), plus the area `...Response.cs` with DTO and `ToDto()`. Follow `Features/Pets/CreatePet` as the template.
6. **API** `PetSitting.Api/`: request record in `Contracts/Requests.cs`, thin `[Authorize]` controller inheriting `ApiControllerBase`; it only builds the command and calls `_mediator.Send`.
7. **Frontend** `frontend/src/app/`: model in `models/`, service following `services/pet.service.ts`, component with reactive form and signals, route in `app.routes.ts`. New features follow the frontend guide (feature folder + facade). Handle loading, error, 401.
8. **Verify:** `dotnet build backend/backend.slnx`, `npm run build` in `frontend/`, try the endpoint via `backend/backend.http` (success, each failure, 401, other user's data). CI runs no tests.

## Non-negotiables
- `UserId` comes from the JWT (`ApiControllerBase.UserId`), **never** from the request body.
- Check ownership/role in the handler; return "X not found." for both missing and foreign records.
- Use the words "not found" only for real 404s (`Respond()` maps that phrase to 404, else 400).
- Expected failures: return `Success=false` + message (current convention). Exceptions only for unexpected errors.
- Rules about an entity live on the entity, not in the handler. Handlers orchestrate; do not `Send` from inside a handler.
- Return DTOs, never EF entities (`User` has `PasswordHash`).
- No secrets in code, `appsettings.json` or environment files; use Key Vault / App Service settings, placeholder in `.env.example`.
- Merge to `main` deploys to production and runs migrations: keep breaking schema changes two-step.

## Finish
Update `docs/DOCUMENTATION.md` (API table, data model, functional section) and tell the user which files were created or changed.
