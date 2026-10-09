# 5. Reviews

## Goal
After a completed booking, each side can review the other. Reviews are tied to a real booking and are revealed at the same time.

## New subsystem: `Reviews`

## Domain
`Review` aggregate root: `BookingId`, `AuthorId`, `SubjectId`, `SubjectRole` (Sitter | Owner), `Rating` (1–5), `Comment?`, `CreatedAt`, `IsPublished`.
Invariants: one review per author per booking; author is a participant; subject is the other participant; rating 1–5; booking is Completed; review window (e.g. 14 days after completion).
Reveal rule (double-blind): a review stays hidden until the counterpart also reviews or the window closes; then both are published.

## Application
`IReviewsManager`: `CreateReviewAsync`, `GetReviewsForUserAsync(userId, role, page)`, `GetPendingReviewsAsync(userId)`, `GetRatingSummaryAsync(userId)` (average + count per role). Booking facts come from the `IBookingsManager` DTO.
Publishing at window close: lazy on read, background job later.

## Integration
- Search results (plan 3) show the sitter's average rating and count via the Reviews manager; sort-by-rating becomes possible.
- Sitter public profile lists reviews; owner ratings are visible to the sitter on a booking request.

## API
`POST /bookings/{id}/review` · `GET /users/{id}/reviews?role=` · `GET /reviews/pending`

## Frontend
"Rate your stay" prompt on completed bookings, star input + comment, reviews list on the sitter profile, rating on search cards, owner rating on a booking request.

## Tasks (outline)
1. Domain `Review` + invariants + tests
2. Repository + migration
3. Manager + controller
4. Wire rating summary into search and public profile
5. Frontend: review form, list, rating display

## Open decisions
- Window length, and whether an unanswered review is published alone when the window closes.
- Sitter replies to reviews: later.
- Moderation/reporting: later.
