# Backend Architecture Decisions — PetSitting App

> **Purpose of this file:** This document is the source of truth for backend architectural decisions on this project. Any AI assistant (Claude, Copilot, ChatGPT, etc.) generating or reviewing backend code MUST follow these decisions. Do not suggest alternative architectures (e.g. iDesign, generic Repository/UoW-per-entity, traditional N-layer/3-tier) unless explicitly asked to reconsider the architecture itself.

## Stack
- C# / ASP.NET Core
- Entity Framework Core
- MediatR (CQRS + pipeline behaviors)
- FluentValidation (or similar) for request validation

## Context
- Solo developer, long-term project (multi-year horizon).
- Domain has genuinely complex business rules (scheduling conflicts, cancellation policies, pricing, sitter-pet matching), not just CRUD.
- Priority: fast navigation and low cognitive overhead when returning to code after weeks/months away, over strict multi-developer governance.

## Core Architectural Decisions

### 1. Vertical Slice Architecture (Application layer)
Organize the Application layer by **feature/use case**, not by technical layer (no generic `Services/`, `Controllers/`-only-thinking across the whole app).

Each use case is a self-contained slice containing everything needed to execute it: its Command/Query, its Handler, its Validator, and any request/response DTOs specific to it.

**Why:** For a solo developer maintaining this for years, being able to open one folder and see the entire flow for a use case (e.g. "cancel a booking") beats jumping across multiple layered projects. This is the single biggest maintainability lever for this project.

### 2. Domain-Driven Design (tactical patterns only, not full strategic DDD)
The `Domain` project contains:
- **Entities & Aggregate Roots** (e.g. `Booking`, `Sitter`, `PetProfile`) that enforce their own invariants. Business rules live as methods on the aggregate (e.g. `booking.Cancel()` throws/returns failure if within the no-cancellation window), not scattered across handlers.
- **Value Objects** for concepts without identity (e.g. `DateRange`, `Money`) instead of primitive obsession (raw `DateTime` pairs, raw `decimal`).
- **Domain Events** for side effects (e.g. `BookingConfirmed`) so aggregates don't need to know about notifications, emails, etc. Dispatched via MediatR's `INotification`.

**Why:** The domain complexity here (conflicts, policies, matching) justifies explicit modeling. This is where business rules should live — not in Application handlers, not in Infrastructure.

### 3. CQRS via MediatR
Commands (writes) and Queries (reads) are separate, each with their own handler. Controllers/minimal API endpoints are thin — they only construct a Command/Query and dispatch it via MediatR.

Cross-cutting concerns (validation, logging, authorization checks) are implemented as **MediatR pipeline behaviors**, not repeated per-handler.

**Why:** Pairs naturally with vertical slices; keeps endpoints trivial; avoids duplicating cross-cutting logic across every handler.

### 4. Repository pattern — scoped to Aggregate Roots ONLY
Repositories exist **only** for aggregate roots (e.g. `IBookingRepository`, `ISitterRepository`), not one per entity, and not as a generic `IRepository<T>` wrapper around `DbSet<T>`.

**Explicitly rejected:** Generic Repository + Unit of Work around every entity. `DbContext`/`DbSet` already provides this; wrapping it generically adds indirection without real benefit.

**Why this scoping instead:** A repository per aggregate root is the DDD-correct usage — it hides persistence details and ensures the aggregate is loaded/saved as a consistency boundary, rather than being a pointless pass-through.

### 5. Result pattern for expected domain failures
Use a `Result<T>` (or similar) return type for expected failure cases (e.g. "sitter unavailable," "invalid cancellation window"). Reserve exceptions for truly exceptional/unexpected failures (e.g. database unavailable, programming errors).

**Why:** Expected domain failures are part of normal control flow and read far more clearly as explicit return values than as caught exceptions, especially when revisiting code long after writing it.

## Explicitly Rejected Alternatives (and why)

| Alternative | Why it was rejected for this project |
|---|---|
| **iDesign** (Manager/Engine/Resource Access, volatility-based decomposition) | Considered and compared directly. iDesign's core strength — strict layer discipline preventing *other developers* from taking shortcuts — has less payoff for a solo developer. Its cost — extra layers to traverse for every change — has more cost for a solo developer doing all the navigating. Familiarity with iDesign from another project was acknowledged as a legitimate reason someone might still prefer it, but was not enough to outweigh the navigation-friction cost here. |
| **Generic Repository + Unit of Work (per entity)** | Redundant on top of EF Core's built-in `DbContext`/`DbSet`; adds indirection without corresponding benefit. |
| **Traditional layered (N-tier) architecture organized purely by technical layer** | Would scatter each feature's logic across many top-level folders, increasing navigation cost as the app grows over years. |

## Suggested Project Structure

```
PetSitting.Domain/
    Bookings/
        Booking.cs                (aggregate root)
        BookingStatus.cs
        DateRange.cs               (value object)
        Events/BookingConfirmed.cs
    Sitters/
    Pets/

PetSitting.Application/
    Features/
        Bookings/
            CreateBooking/
                CreateBookingCommand.cs
                CreateBookingHandler.cs
                CreateBookingValidator.cs
            CancelBooking/
                CancelBookingCommand.cs
                CancelBookingHandler.cs
        Sitters/
        Pets/
    Common/
        Behaviors/                 (MediatR pipeline behaviors: validation, logging)
        Result.cs

PetSitting.Infrastructure/
    Persistence/
        AppDbContext.cs
        Repositories/
            BookingRepository.cs   (implements IBookingRepository from Domain/Application)
    ExternalServices/

PetSitting.Api/
    Controllers/ or Endpoints/     (thin, dispatch to MediatR only)
```

## Rules for AI Assistants Working on This Codebase
1. New use cases go in `Application/Features/{FeatureArea}/{UseCaseName}/` as a self-contained slice.
2. Business rules and invariants belong on the Domain aggregate, not in the handler.
3. Do not introduce a generic `IRepository<T>`. Only add repositories for aggregate roots.
4. Do not introduce NgRx-style state management concepts here — this is backend-only guidance (see `frontend-architecture.md` for frontend rules).
5. Do not propose switching to iDesign, a full N-tier layered architecture, or re-introducing generic Repository/UoW unless the user explicitly asks to revisit the architecture itself.
6. Use `Result<T>` for expected failures; use exceptions only for unexpected/exceptional situations.
