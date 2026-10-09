# 1. Location model + additive roles

## Goal
- Users have a structured, geocoded location so distance search is possible later.
- A user can be an owner (has pets) and a sitter (active sitter profile) at the same time. No fixed role.

## Current state
- `User.Address` is a free-text string; `User.Role` (`Owner`/`Sitter`, stored as int) is used in register, JWT claims (`JwtTokenService`), `UserDto`, `UsersManager.ChangeRoleAsync`, `POST /api/auth/change-role`, and the frontend navbar (switch button), profile, home and `user.model.ts`.
- `SitterAvailability` is the sitter profile (schedule, services, accepted pet types, `MaxPets`, `Bio`), 1:1 with `User`.
- The profile page already loads Google Maps JS (Places autocomplete, draggable marker); the geocoding is client-side and only the formatted string is saved.
- Database is SQL Server. No test project (tests are out of scope for this plan).

## Decisions (agreed)
1. **Geocoding:** local Romanian locality dataset stored in a DB table (county, city, postal code, lat, lng); the backend looks up a centroid from it. No external geocoding call on save. Dataset: GeoNames `RO.zip` (CC BY 4.0, attribution link to geonames.org required); checked, see "Dataset check".
2. **Google Maps stays.** The profile keeps the map showing the user's location, and it will be reused later for showing sitters on a map in search. Key is referrer-restricted.
3. **Roles are not exclusive.** No role on `User`. "Sitter" = active sitter profile. "Owner" = a per-user flag chosen with a checkbox. Both can be on at once. The profile shows the sections for whatever is on.
4. **`IsSitter` is not on `UserDto`** (avoids a Users <-> Availability manager cycle). The frontend reads sitter state from `GET /api/availability`.
5. **Mode/visibility is stored on the server**, not in localStorage.
6. **Legacy `Address` text is kept**, moved into `Street`; county/city/postal code are nullable until the user fills the form.
7. **Existing Role=Sitter users without a saved profile stay inactive** until they save one.
8. **Rename `SitterAvailability` -> `SitterProfile` now** (entity, table, repository, DTO). The `Availability` subsystem folder, manager and `/api/availability` route keep their names.
9. **No tests in this plan.**

## Part A — Location
**Domain (Users):** value object `Location` (owned type on `User`): `County`, `City`, `PostalCode` (6 digits), `Street`, `Latitude`, `Longitude`. Replaces `Address`.
- Exact street is private; public views only expose city + county (+ approximate distance).
- Invariants (factory returns `Result<Location>`): county in the 42-entry list; postal code 6 digits; lat/lng both present or both absent and inside Romania's bounding box (~43.6-48.3 N, 20.2-29.7 E). Legacy rows (street only, no county/city) are allowed only through the migration, not through the factory.
- `User.SetLocation(Location)`.

**Dataset:** table `RomanianLocalities` (Id, County, City, PostalCode, Latitude, Longitude), seeded by a migration/seed script. Read-only reference data, not an aggregate: accessed through a small `ILocalityLookup` (Application/Users, implementation in Infrastructure) rather than a repository.

**Application:** `UsersManager.UpdateLocationAsync(userId, UpdateLocationInput)`: look up the centroid by postal code (fallback county + city), build `Location`, `user.SetLocation`, save. No match -> `Validation("Address not found")`. `UserDto` gets `Location`. `UpdateProfileInput` drops `Address`.

**API:** `PUT /api/auth/me/location` + `UpdateLocationRequest` (DataAnnotations: required county/city, `\d{6}` postal code). `UpdateProfileRequest` drops `Address`. Register the lookup in `Program.cs`.

**Frontend:** replace the single address field with county dropdown, city, postal code, street. The map stays and geocodes the full address client-side for display (as today); stored coordinates are the centroid. Street-level precision for search pins (backend Google geocoding after save) is a later step, not in this plan.

**Migration:** add owned columns, create `RomanianLocalities` and seed it, copy `Address` -> `Location_Street`, drop `Address`, index on (`Latitude`, `Longitude`).

## Part B — Additive roles
**Domain:** remove `UserRole` and `User.Role`. Add `User.IsOwner` (bool, default true). Rename `SitterAvailability` -> `SitterProfile`, add `IsActive`, `Activate()`, `Deactivate()`.

**Application/Users:** drop role from register, `UserDto`, JWT claims; delete `ChangeRoleAsync` and `change-role`. Add `SetOwnerAsync(userId, bool)` + `PUT /api/auth/me/owner`; `UserDto.IsOwner`. Add `HasLocationAsync(userId)` to `IUsersManager` (next to `ExistsAsync`).
**Application/Availability:** `ActivateSitterProfileAsync` / `DeactivateSitterProfileAsync`. Activation calls `IUsersManager.HasLocationAsync` and fails with `Validation` if no location. `AvailabilityDto` gets `IsActive`. Dependency direction: Availability -> Users only.

**Frontend:** remove `UserRole`, `changeRole`, navbar switch button and role badges. Profile gets two toggles: "I have pets" (owner flag) and "I'm a sitter" (activate/deactivate, disabled with a hint until a location is saved). Pets section shows if `isOwner`; sitter section if the profile is active. Home page text updated. Navbar shows a "Become a sitter" link when there is no active profile.

**Migration:** add `IsActive` to the sitter table; set it to 1 for users with Role=Sitter who have a row; add `IsOwner` (1 for Role=Owner, 0 for Role=Sitter, to preserve what they see today); rename table; drop `Role`.

## Tasks (outline)
1. Roles slice, one green build: `IsOwner`, `SitterProfile` rename + `IsActive`, activate/deactivate, remove `UserRole` from backend/JWT/frontend, migration with data fix, profile toggles
2. `Location` value object + EF owned mapping + locality table/seed + migration
3. `UpdateLocationAsync` + contracts; activation requires location
4. Profile UI: structured address form, map kept

## Risks
- Removing `Role` touches auth end to end; do it in one slice with a green build before moving on.
- Existing JWTs carry the role claim; it just becomes ignored, no forced logout needed.
- Locality dataset quality/licence decides how good postal-code lookup is; rural postal codes may be missing, hence the county + city fallback.
- Slice 1 lands before locations exist, so activation's location check is added in task 3.

## Dataset check (GeoNames RO.txt, downloaded 2026-10-09)
- 37,915 rows, 37,914 distinct 6-digit postal codes, ~13.9k distinct place+county pairs, all 42 counties (incl. Bucureşti). Coordinates all inside Romania's bounding box (43.66-48.24 N, 20.32-29.65 E), so the invariant box above holds.
- Accuracy: 25,671 rows matched to a GeoNames place (4), 4,575 estimated (1), 7,669 with the column blank. Fine for city-level distance.
- Bucharest (12.4k rows) is stored as county `Bucureşti`, place `Bucureşti NN`, with `Sector N` in admin3; all rows of a sector share one coordinate. Lookup by postal code works; city "București" without a code resolves to a sector centroid at best.
- Names use cedilla diacritics (ş, ţ), not the correct comma-below forms (ș, ț); the import must normalize them, and matching must be diacritic-insensitive.
- Seed: import county + place + postal code + lat/lng into `RomanianLocalities` (drop duplicates), keep the attribution notice.

## Open items
- None blocking. `IsOwner` is a server-side checkbox ("I have pets"), ON by default for new users (decided).
