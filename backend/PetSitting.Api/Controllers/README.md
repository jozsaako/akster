namespace PetSitting.Api.Controllers;

/// <summary>
/// Controllers are thin - they only:
/// 1. Construct a Command/Query
/// 2. Dispatch it via MediatR
/// 3. Return the result
/// 
/// All business logic lives in handlers, not here.
/// </summary>
