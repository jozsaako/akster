# 4. Booking chat

## Goal
Owner and sitter talk inside a booking. No general inbox or DMs: contact stays tied to a real booking, which limits spam.

## New subsystem: `Messaging`
Replaces the placeholder `Common/Message` entity (an unused example; delete it).

## Domain
`Conversation` aggregate root: `BookingId` (1:1), `OwnerId`, `SitterId`, owns `Message` children (`SenderId`, `Text`, `SentAt`, `ReadAt?`).
Invariants: only the two participants post; text length cap; read-only once the booking is Declined/Expired; allowed for Requested/Accepted/Completed (decide a grace window after completion).

## Application
`IMessagingManager`: `GetConversationAsync(bookingId, userId)`, `SendMessageAsync`, `MarkReadAsync`, `GetUnreadCountsAsync`. Participation is checked through the `IBookingsManager` DTO (no Bookings repository). Conversation is created lazily on first message.

## Delivery
First version: plain REST with polling (open thread every few seconds, unread count periodically). SignalR later if polling hurts. No push notifications yet.

## API
`GET /bookings/{id}/messages?after=` · `POST /bookings/{id}/messages` · `POST /bookings/{id}/messages/read` · `GET /messages/unread`

## Frontend
Chat panel on the booking detail page; unread badge in the navbar and bookings list.

## Tasks (outline)
1. Domain: `Conversation` + `Message` with invariants, tests
2. Repository + migration; remove `Common/Message`
3. Manager + controller
4. Frontend chat panel with polling + unread badge

## Open decisions
- Filtering phone/email in message text: skip for now (no payments to circumvent), revisit later.
- Create the conversation at request time vs on first message.
