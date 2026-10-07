# Features (Vertical Slices)

Each feature here is a self-contained use case with everything needed:
- Command/Query
- Handler
- Validator
- Request/Response DTOs

Example structure for a feature like "CreateBooking":
```
Features/
  Bookings/
	CreateBooking/
	  CreateBookingCommand.cs
	  CreateBookingCommandHandler.cs
	  CreateBookingCommandValidator.cs
	CancelBooking/
	  CancelBookingCommand.cs
	  CancelBookingCommandHandler.cs
	GetBooking/
	  GetBookingQuery.cs
	  GetBookingQueryHandler.cs
```

This keeps related code co-located and easy to navigate.
