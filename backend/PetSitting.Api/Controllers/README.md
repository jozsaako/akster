# Controllers

Controllers are thin. They only:
1. Bind the request and require `[Authorize]`.
2. Pass `UserId` (from the JWT) and the request to the subsystem's manager.
3. Map the returned `Result` to an HTTP status via the shared helper in `ApiControllerBase`.

No business logic, no repositories, no EF. Logic lives in managers and domain aggregates.
