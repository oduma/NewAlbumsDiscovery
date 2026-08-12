# Domain-Driven Design (DDD) Principles & Guardrails

## 1. Domain Purity
The domain model represents the absolute truth of the business rules. It must remain deterministic, mathematically rigid, and isolated from technical implementation details.

* **The Zero-Dependency Rule:** The `Domain` project must consist of pure C# only. It is strictly forbidden to add UI frameworks (MAUI), ORMs (EF Core), database drivers (SQLite), or infrastructure-specific NuGet packages to the Domain.

## 2. Strategic Design: Bounded Contexts
Maintain strict modular separation between different systems to prevent the "Big Ball of Mud" anti-pattern. Models should only share concepts via explicit integration mapping.
* **Example Contexts:**
    * `CoreOperations`: Handles overarching workflows, primary structural timelines, and high-level orchestration.
    * `RulesEngine`: Handles deterministic business math, scoring, constraints, and state-based escalations/penalties.
    * `AuditLog`: Handles immutable raw data entry, enforcing absolute timestamping and mutually exclusive execution paths.

## 3. Tactical Design Patterns
State and data consistency are maintained through strict adherence to DDD building blocks.

* **Entities:** Use for objects with a distinct identity that persists over time, even if their properties change (e.g., `User`, `Account`). Two entities with the same ID are considered the same entity.
* **Value Objects:** Use for descriptive, point-in-time measurements or cohesive concepts that lack identity. They **must be strictly immutable** (e.g., a `Measurement` containing value, unit, and timestamp). Two value objects with identical properties are considered identical.
* **Aggregates & Aggregate Roots:** 
    * Transactional boundaries and data consistency are enforced exclusively through Aggregate Roots.
    * External entities may only hold references to the Aggregate Root, never to its internal child entities.
    * For example, if a `TimelineEvent` is an aggregate root, any underlying child states or penalties live strictly within it and are mutated only through methods exposed by the root.
* **Domain Events:** Side-effects across the domain must be handled via Domain Events to maintain decoupling. When an aggregate changes state, it raises an event rather than directly calling other services.

## 4. Red flags the concept is in the wrong context if:

*  An entity has a string SourceType field discriminating between two or more contexts - it is crossing context boundaries and belongs in a neutral context
* A context's repository is injected into another context's domain layer
* A context owns entities that describe things from two different user-facing concerns