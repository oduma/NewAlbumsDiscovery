# Technical Specifications: New Albums Discovery

## 1. Application Architecture
- **Framework:** .NET 10 Worker Service.
- **Hosting Modes:** Configured to run as a background service with native integration for both OS platforms using `.UseWindowsService()` and `.UseSystemd()`.
- **Feature Orchestration:** Features 1 (Aggregation), 2 (Discovery), and 3 (Notification) are implemented as independent modules. A central orchestrator coordinates their execution sequentially to guarantee they do not step on each other's toes.

## 2. Dual Database Architecture (SQLite)
The application decouples external integration data from internal application state using two separate SQLite databases:

### A. Integration Database (Read-Only)
- **Database File:** `loved-tracks.db`
- **Location:** Located in the `DB` folder under the user's home directory (`%USERPROFILE%\DB\loved-tracks.db` or `$HOME/DB/loved-tracks.db`).
- **Purpose:** Produced and updated by an external product. Contains the `LovedTracks` table.
- **Access Pattern:** Read-only access by New Albums Discovery via `LovedTrackRepository`.

### B. Application Database (Read-Write)
- **Database File:** `new-albums-discovery.db`
- **Location:** Located in a `Database` subfolder under the application's running folder (`<AppBaseDirectory>/Database/new-albums-discovery.db`).
- **Purpose:** Private storage for application state (`AggregatedBuckets`, `DiscoveredAlbums`).
- **Management & Migrations:** Maintained exclusively by this application using **EF Core Migrations** (`AppDbContext`). Completely isolated from external schema changes.

## 3. Configuration & Security
- **Environment Variables:** All configuration and secrets are stored in machine-level environment variables to support execution as a Windows Service without requiring a user profile.
- **Configuration Tree Binding:** Maps environment variables using the .NET double-underscore convention:
  - `NewAlbumsDiscovery__Database__LovedTracksDbPath`
  - `NewAlbumsDiscovery__Database__AppDbPath`
  - `NewAlbumsDiscovery__Gemini__ApiKey`
  - `NewAlbumsDiscovery__Telegram__BotToken`
- **Options Pattern:** Strongly bound to `IOptions<T>` classes during DI initialization.

## 4. Domain-Driven Design (Bounded Contexts)
Adhering strictly to `docs/constitution/DDD-architecture.md` and `coding-principles.md`:

### A. MusicAggregator Context (Feature 1)
- **Responsibility:** Reading `LovedTracks` from `loved-tracks.db` and executing the cascading threshold logic to generate valid search scopes.
- **Domain Entities:** `AggregatedBucket` (BucketName, BucketType, Country, Language, Genre, TrackCount).
- **Infrastructure:** Reads `LovedTracks` from `loved-tracks.db`, persists `AggregatedBuckets` to `new-albums-discovery.db` via `AppDbContext` / atomic transaction.

### B. AIDiscovery Context (Feature 2)
- **Responsibility:** Iterating over valid buckets, applying prompt templates, querying Gemini sequentially with retry policies, and handling deduplication.
- **Domain Entities:** `DiscoveredAlbum` (AlbumName, Artist, ReferenceBucketId, Status: Pending).
- **Infrastructure:** Reads `AggregatedBuckets` from `new-albums-discovery.db`, calls Gemini API, and persists `DiscoveredAlbums` to `new-albums-discovery.db`.

### C. Notification Context (Feature 3)
- **Responsibility:** Consuming pending albums, generating URL-encoded YouTube search queries, and dispatching a single Telegram notification.
- **Domain Entities:** `NotificationBatch` (collection of album links).
- **Infrastructure:** Queries `DiscoveredAlbums` (Pending) from `new-albums-discovery.db`, dispatches via Telegram Bot API, and updates status to `SendForReview`.

## 5. Testing Mandate
- As per `tdd.md`, all domain models and application use cases must achieve 100% branch coverage.
- Repositories (`LovedTrackRepository`, `AggregatedBucketRepository`, `DiscoveredAlbumRepository`), Gemini API, and Telegram API must be abstracted behind interfaces for exhaustive unit testing.
