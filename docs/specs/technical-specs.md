# Technical Specifications: New Albums Discovery

## 1. Application Architecture
- **Framework:** .NET 10 Worker Service.
- **Hosting Modes:** Configured to run as a background service with native integration for both OS platforms using `.UseWindowsService()` and `.UseSystemd()`.
- **Feature Orchestration:** Features 1 (Aggregation), 2 (Discovery), and 3 (Notification) must be implemented as independent, self-contained modules. A central orchestrator (e.g., a master `BackgroundService` or MediatR saga) will coordinate their execution sequentially to guarantee they do not step on each other's toes or create race conditions.

## 2. Configuration & Security
- **Environment Variables:** Because the app will run as a Windows Service, it will not have access to user-profile secrets. All configuration and secrets must be stored in machine-level environment variables.
- **Configuration Tree Binding:** The application will rely on the .NET double-underscore environment variable provider to map values into the configuration tree. 
  - *Example:* `NewAlbumsDiscovery__Gemini__ApiKey`
  - *Example:* `NewAlbumsDiscovery__Telegram__BotToken`
- **Options Pattern:** These configurations will be strongly bound to `IOptions<T>` records during dependency injection.

## 3. Domain-Driven Design (Bounded Contexts)
Adhering strictly to the `DDD-architecture.md` and `coding-principles.md` rules, the domain is split into specific bounded contexts:

### A. MusicAggregator Context (Feature 1)
- **Responsibility:** Reading `LovedTracks` and cascading through the threshold logic to generate valid search scopes.
- **Domain Entities:** `Bucket` (contains rules for Region, Language, Genre, and Track Count).
- **Infrastructure:** Reads from `LovedTracks` and persists to `AggregatedBuckets` in SQLite.

### B. AIDiscovery Context (Feature 2)
- **Responsibility:** Iterating over valid buckets, applying the specific prompt templates, querying Gemini with a resilient retry policy, and handling deduplication.
- **Domain Entities:** `DiscoveredAlbum` (AlbumName, Artist, ReferenceBucketId, Status: Pending).
- **Infrastructure:** Reads `AggregatedBuckets`, executes HTTP calls to Gemini API, and persists to `DiscoveredAlbums`.

### C. Notification Context (Feature 3)
- **Responsibility:** Consuming pending albums, generating URL-encoded YouTube search queries, and dispatching a single batched message.
- **Domain Entities:** `NotificationBatch` (a collection of links to be dispatched).
- **Infrastructure:** Queries `DiscoveredAlbums` for `Pending`, executes HTTP call to Telegram Bot API, and updates records to `SendForReview`.

## 4. Testing Mandate
- As per the `tdd.md` constitution, all domain models (buckets, albums) and application use cases (the orchestration logic) must achieve 100% branch coverage.
- The SQLite database, Gemini API, and Telegram API must all be abstracted behind interfaces so the independent features can be fully unit tested via mocking.
