# The TDD & Testing Mandate

## 1. Test-Driven Development (TDD) is Absolute
The application must remain a mathematically strict, deterministic engine. Unverified code is considered broken code.
* **The TDD Cycle:** You must strictly follow the Red-Green-Refactor cycle. Tests must be written *before* the implementation code.
* **No Exceptions:** UI logic, Application Use Cases, and Domain Entities all require tests.

## 2. 100% Branch Coverage Mandate
* **Hard Requirement:** The project operates under a strict **100% branch coverage** mandate.
* Every `if`, `else`, `switch`, and edge case (e.g., exact rollover timing constraints, calculation conversion limits) must be explicitly proven via automated tests.
* You are not permitted to bypass this requirement or write "placeholder" tests.

## 3. Testing Rules & Tooling
* **Mocking:** Use standard mocking frameworks (e.g., Moq, NSubstitute) to isolate logic.
* **Infrastructure Isolation:** External APIs and database interactions (regardless of the underlying database technology) must be completely abstracted via interfaces (e.g., `IDataRepository` or `IExternalIntegrationService`) in the Application layer, allowing them to be fully mocked during unit testing.
* **Domain Testing:** Because the Domain is pure C# (adhering to the Zero-Dependency Rule), it must be tested instantly and exhaustively without mocking frameworks whenever possible. Instantiate the Entities and assert their state transitions directly.