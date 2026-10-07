# Frontend Architecture Decisions — PetSitting App

> **Purpose of this file:** This document is the source of truth for frontend architectural decisions on this project. Any AI assistant (Claude, Copilot, ChatGPT, etc.) generating or reviewing frontend code MUST follow these decisions. Do not suggest alternative architectures (e.g. NgRx, a monorepo/Nx workspace, NgModules) unless explicitly asked to reconsider the architecture itself.

## Stack
- Angular (standalone components, current/modern APIs — not NgModule-based)
- Angular Signals for state management

## Context
- Solo developer, long-term project (multi-year horizon).
- Single Angular application — no plans for multiple separate frontend apps (e.g. no separate sitter portal / owner portal / admin app planned).
- Priority: low ceremony, fast to reason about alone, without sacrificing structure as the app grows.

## Core Architectural Decisions

### 1. Single repository, not a monorepo
Standard single Angular application repo/workspace. No Nx or multi-app monorepo tooling.

**Why:** Monorepo tooling (Nx, workspace boundary enforcement, shared library setup) earns its cost when multiple separate apps share code. This project has one frontend app for the foreseeable future, so that tooling would add setup/build complexity with no corresponding payoff.

**Note for future reconsideration:** If a genuinely separate frontend app becomes necessary later (e.g. a dedicated sitter-facing app), migrating from feature folders to an Nx monorepo at that point is a manageable one-time refactor — this decision is not a permanent lock-in, just the right starting point now.

### 2. Feature-based folder structure
Organize by feature/domain area, not by technical type (no top-level `components/`, `services/`, `models/` catch-alls spanning the whole app).

```
src/app/
    features/
        bookings/
            booking-list/
            booking-detail/
            bookings.facade.ts
            booking.model.ts
        pet-profiles/
        messaging/
        sitters/
    shared/
        ui/                 (dumb/presentational reusable components)
        utils/
    core/
        services that are truly app-wide (auth, http interceptors)
```

**Why:** Scales cleanly as more features are added over years; keeps everything related to one feature discoverable in one place; supports lazy loading per feature route.

### 3. Standalone components (not NgModules)
All new components are standalone. No `NgModule` declarations for feature areas.

**Why:** Less boilerplate, simpler dependency injection, and the current/future direction of Angular itself.

### 4. Signals for state management (not NgRx)
Component and feature state is managed with Angular Signals, not NgRx.

**Explicitly rejected:** NgRx (actions/reducers/effects/selectors boilerplate). Its main benefit — predictable state management for large teams collaborating on shared state — has less payoff for a solo developer, while its ceremony cost remains high regardless of team size.

### 5. Facade pattern per feature
Each feature exposes a **Facade service** (e.g. `BookingsFacade`) that:
- Wraps signals for that feature's state (e.g. `bookings`, `isLoading`, `error`)
- Exposes methods for actions (e.g. `createBooking()`, `cancelBooking()`)
- Internally handles HTTP calls and updates its own signals

Components consume the facade only — they never call HTTP services or manage state directly.

**Why:** Gives most of NgRx's real benefit (components decoupled from data-fetching/state details, easy to test components in isolation, state logic swappable later) without the reducer/action/effect boilerplate.

### 6. Smart (container) vs. Dumb (presentational) components
- **Smart/page components** (in `features/{feature}/`) — talk to the Facade, handle routing, own the "what happens."
- **Dumb/presentational components** (in `shared/ui/`) — receive `@Input()`s, emit `@Output()`s, own only "how it looks." No direct facade or HTTP access.

**Why:** Presentational components stay reusable and trivially testable; business/data logic stays isolated to one layer per feature.

## Explicitly Rejected Alternatives (and why)

| Alternative | Why it was rejected for this project |
|---|---|
| **NgRx** | Ceremony (actions/reducers/effects/selectors) pays off most with large teams sharing complex global state. Solo developer with Signals available gets most of the practical benefit (structure, testability) at much lower cost. |
| **Monorepo (Nx multi-app workspace)** | No planned second frontend app. Would add workspace/build tooling complexity with no current payoff. Revisit if a second app becomes concretely planned. |
| **NgModule-based architecture** | Standalone components are the modern default with less boilerplate and align with Angular's direction going forward. |

## Rules for AI Assistants Working on This Codebase
1. New features go in `src/app/features/{feature-name}/` with their own facade service.
2. Components must be standalone — do not generate or suggest NgModules for feature areas.
3. Do not introduce NgRx (`@ngrx/store`, actions, reducers, effects) unless the user explicitly asks to revisit state management.
4. Do not restructure the workspace into an Nx monorepo unless the user explicitly asks to revisit that decision.
5. Keep presentational components in `shared/ui/` free of direct HTTP or facade/state access — they should only receive data via `@Input()`/emit via `@Output()`.
6. Backend-related guidance (API design, C# patterns) lives in `backend-architecture.md` — do not duplicate or contradict it here.
