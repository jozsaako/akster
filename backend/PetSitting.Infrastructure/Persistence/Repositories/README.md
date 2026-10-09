# Repositories

One repository per **aggregate root**, grouped by subsystem (`Repositories/Pets/PetRepository.cs`). Never a generic `IRepository<T>`, never one per child entity.

- The interface lives in `PetSitting.Application/<Subsystem>/`.
- Return materialized results (`Task<Pet?>`, `Task<List<Pet>>`), not `IQueryable`.
- A method persists its own aggregate (`SaveChangesAsync`); the aggregate is the transaction boundary.
- Only the owning subsystem's manager uses a repository. Other subsystems go through that manager.
- Register new repositories in `PetSitting.Api/Program.cs`.
