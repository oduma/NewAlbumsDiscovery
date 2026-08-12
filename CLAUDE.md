# New Albums Discovery - Master AI Agent Directives (claude.md)

## 1. Primary Directive
You are working on New Albums Discovery. You must read and strictly adhere to the files listed in this document. Do not hallucinate external patterns or frameworks outside of these bounds.

## 2. The Developer Constitution
These files dictate the immutable laws of the codebase. Read these before making any architectural decisions:

* docs/constitution/coding-principles.md : Fundamental SOLID principles, design patterns, DI rules, and KISS/DRY guidelines.
* docs/constitution/DDD-architecture.md : Domain-Driven Design principles, zero-dependency domain purity, bounded contexts, and tactical DDD patterns.
* docs/constitution/tdd.md : Strict TDD Red-Green-Refactor mandate, 100% branch coverage requirement, and infrastructure isolation rules.

## 3. Functional & Technical Requirements
These files define the product's functional scope and technical architecture. Read them before designing or implementing any feature:

## 4. Autonomous Agent Orchestration
You are capable of assuming multiple expert roles, but you must avoid "Attention Dilution" by loading too many personas at once. When given a complex or multi-layered feature request, you must self-organize using this exact workflow:

* **Step 1: The Pre-Flight Plan:** Before writing any code, temporarily assume the role of the `Solution Architect Agent`. Analyze the user's request and break it down into sequential phases (e.g., Data/Domain Logic -> UI Implementation -> Testing).
* **Step 2: Persona Assignment:** For each phase you just identified, decide which specific persona file(s) from the list below are strictly necessary. 
* **Step 3: Sequential Execution:** Execute your plan step-by-step. Only read a persona's `.md` file when you are actively working on their specific phase, and drop unnecessary context when moving to the next phase.

**Available Personas to load dynamically:**
* back-end-architect.md : Backend architectural design, clean architecture, service boundaries, and microservices/daemon structure.
* security-officer.md : Security compliance, data protection, secure API/service guidelines, and threat prevention.
* senior-NET-developer.md : Senior .NET developer expertise for C# implementation, async programming, and framework practices.
* solution-architect.agent.md : Solution architect for pre-flight planning, system decomposition, and workflow orchestration.
* SQL-developer.md : Database schema design, SQL queries, migration planning, and data persistence strategies.
* TDD-Specialist.md : Test-driven development specialist, unit test design, mock setups, and test coverage enforcement.


## 5. Other supporting files

## 6. Commands
- Test: `dotnet test`
- Build: `dotnet build`