# 2. Bookings

## Goal
An owner requests a stay with a sitter; the sitter accepts or declines; the booking moves through its lifecycle with enforced rules.

## New subsystem: `Bookings`
Domain `Bookings/`, Application `Bookings/` (`IBookingsManager`, `IBookingRepository`, DTOs), Infrastructure repository, `BookingsController`.

## Domain
`Booking` aggregate root:
- `OwnerId`, `SitterId` (ids only, no navigations), `PetIds` (the owner's pets), `Service` type, `DateRange` (value object: start/end dates), `Notes`, `Status`, timestamps.
- Status flow: `Requested → Accepted | Declined | Expired`, `Accepted → Completed`, plus `Cancelled` (from Requested/Accepted).
- Methods enforce rules: `Accept()`, `Decline()`, `Cancel(by, now)`, `Expire(now)`, `Complete(now)`.
- Invariants: owner ≠ sitter; end ≥ start; start not in the past; transitions only from valid states.

## Business rules (manager, using other managers' DTOs)
- Pets belong to the owner (Pets manager).
- Sitter profile active; pet types accepted; pet count ≤ `MaxPets`; dates fit the sitter's schedule (Availability manager).
- No overlap with the sitter's other **Accepted** bookings beyond `MaxPets` capacity.
- Request expires after N hours without a sitter response (start with a lazy check on read; background job later).
- Cancellation policy: one simple rule to start (e.g. free cancel until 48h before start); per-sitter policies later.

## API (outline)
`POST /bookings` (request) · `GET /bookings?as=owner|sitter` · `GET /bookings/{id}` · `POST /bookings/{id}/accept|decline|cancel`. `Completed` is set automatically after the end date (decide vs manual confirm).

## Frontend
- Request form on the sitter profile (dates, pets, service, notes)
- Bookings list with tabs: "My requests" (as owner) / "Requests for me" (as sitter)
- Booking detail with status + actions

## Tasks (outline)
1. Domain: `Booking`, `DateRange`, status + transitions, unit tests for every transition
2. Repository + EF mapping + migration (booking-pets join table)
3. Manager: create / accept / decline / cancel / list / get, capacity + overlap checks
4. Controller + request records + register in `Program.cs`
5. Frontend: service, list, detail, request form

## Open decisions
- Date-only (boarding, house sitting) vs date+time (walks, drop-ins). Leaning: dates only for the first version.
- Who marks `Completed` (automatic vs confirmation).
- Expiry mechanism (lazy vs background service).
