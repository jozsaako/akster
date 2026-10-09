# 3. Search by distance + availability

## Goal
An owner finds active sitters near a location who can take their pets on given dates, sorted by distance.

## Query
Inputs: location (own saved one or typed city/postal code), radius km (default 25), date range, pet types/count, optional service filter.
Filters: sitter active, within radius, accepts the pet type(s), `MaxPets` leaves room after Accepted bookings in the range, schedule covers the dates, not the searcher themself.
Sort: distance (default); rating and price later.
Paging: required from the start (country-wide).

## Backend
- Lives in **Availability** (sitter profiles are its data). Booked capacity per sitter for a date range comes from **Bookings** via `IBookingsManager` (no cross-repository access), e.g. a method returning occupied counts per sitter.
- `IAvailabilityManager.SearchSittersAsync(criteria)` → paged `SitterSearchResultDto` (name, photo, city/county, rounded distance km, bio snippet, services, accepted types, rating placeholder).
- Distance: bounding-box prefilter on lat/lng index, then haversine; or SQL Server `geography.STDistance` if plan 1 moves to spatial. Decide here.
- Privacy: never return exact street or coordinates, only city/county and rounded distance.

## API
`GET /sitters/search?lat&lng|postalCode&radiusKm&from&to&petType&petCount&service&page&pageSize` · `GET /sitters/{id}` (public profile).

## Frontend
Search page: filters bar (location, dates, pet, radius), result cards, friendly empty state, link to sitter profile → request booking (plan 2).

## Tasks (outline)
1. Decide distance strategy; add index/migration if needed
2. Occupied-capacity query on the Bookings manager
3. `SearchSittersAsync` + DTOs + tests (radius, capacity, dates, self-exclusion)
4. Controller endpoints + public sitter profile DTO
5. Frontend search page + sitter profile page

## Open decisions
- Resolve a searched city/postal code with the same local lookup as plan 1.
- `ScheduleJson` is free-form; "schedule covers the dates" may need a typed schedule model first.
