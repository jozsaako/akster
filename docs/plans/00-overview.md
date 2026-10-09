# Feature plans — overview

Market: **Romania first**, country-wide marketplace. **No payments yet.** One account can be both owner and sitter.

Each plan is a surface-level outline. We refine them one by one before building. Order matters; each depends on the ones above.

| # | Plan | Subsystem(s) | Depends on |
|---|------|--------------|------------|
| 1 | [Location + additive roles](01-location-and-additive-roles.md) | Users, Availability | — |
| 2 | [Bookings](02-bookings.md) | Bookings (new) | 1 |
| 3 | [Search by distance + availability](03-search.md) | Availability (+ Bookings read) | 1, 2 |
| 4 | [Booking chat](04-chat.md) | Messaging (new) | 2 |
| 5 | [Reviews](05-reviews.md) | Reviews (new) | 2 |

Out of scope for now: payments, stay updates (photo feed), care sheets, push notifications, verification badges, maps UI.

All plans follow `backend/AGENTS.md` (Controller → Manager → Repository, `Result<T>`, no cross-subsystem repositories/entities, `UserId` from JWT) and `frontend/AGENTS.md`.
