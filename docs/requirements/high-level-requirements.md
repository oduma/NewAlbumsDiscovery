# High Level Requirements: New Albums Discovery

## 1. Overview
The New Albums Discovery application is a cross-platform background worker (TSR / Windows Service / Linux Daemon) built with .NET 10. It is designed to run periodically (e.g., monthly) to aggregate user music preferences from an external read-only SQLite database (`loved-tracks.db`), leverage Gemini AI to discover new album releases matching those precise preferences, store application state in its own dedicated read-write SQLite database (`new-albums-discovery.db`), and notify the user via Telegram with YouTube search links for easy listening.

## 2. Dual Database Architecture
The application separates integration data from private application state across two distinct SQLite databases:

1. **Input Read-Only Database (`loved-tracks.db`)**:
   - **Location:** Located in a `DB` subfolder under the current user's home directory (e.g., `%USERPROFILE%\DB\loved-tracks.db` or `$HOME/DB/loved-tracks.db`).
   - **Owner:** Produced and maintained by an external product.
   - **Access:** Strictly Read-Only access by New Albums Discovery.
   - **Contains:** `LovedTracks` table.

2. **Internal Application Database (`new-albums-discovery.db`)**:
   - **Location:** Stored in a `Database` subfolder under the application's running folder (e.g., `<AppBaseDirectory>/Database/new-albums-discovery.db`).
   - **Owner:** Owned exclusively by New Albums Discovery.
   - **Access:** Read-Write access.
   - **Maintenance:** Managed and updated exclusively using this application's EF Core Migrations.
   - **Contains:** `AggregatedBuckets`, `DiscoveredAlbums`, and future internal application tables.

## 3. Core Features

### Feature 1: Music Preference Aggregator
The app analyzes the `LovedTracks` table in `loved-tracks.db` to generate highly specific "buckets" of music interests. The aggregation follows a cascading logic to find the optimal level of granularity without creating buckets that are too large or too sparse.

- **Level 1 (Country/Region):** Groups tracks by `CountryOrRegion`. If the count is smaller than `CountryRegionThreshold`, it forms a bucket. Otherwise, it cascades to the next level.
- **Level 2 (Language):** Groups the remaining tracks by `LanguagesJson`. A track with multiple languages is counted once for each language. If the count is smaller than `CountryRegionLanguageThreshold`, it forms a bucket. Otherwise, it cascades to the next level.
- **Level 3 (Genre):** Groups the remaining tracks by `GenresJson`. A track with multiple genres is counted once for each genre. This level always forms the final buckets.
- **Filtering & Persistence:** Any generated bucket with a track count smaller than `MinimumBucketThreshold` is discarded. Valid buckets are saved to the `AggregatedBuckets` table in the internal application database (`new-albums-discovery.db`).

### Feature 2: AI New Album Discovery
The app uses the buckets generated in Feature 1 to query the Gemini AI for recent album releases.

- **Dynamic Prompts:** Depending on the bucket type (Country, Country/Language, or Country/Language/Genre), a specific prompt template is used.
- **Configurability:** The timeframe for "recent" releases (e.g., "last month") is configurable via application settings.
- **Execution & Resilience:** Requests to the Gemini API are executed sequentially with configurable downtime between requests and a retry strategy to handle rate limits/transient errors.
- **Data Handling:** The AI returns results in JSON format (Album Name, Artist). The app deduplicates the results and saves them to the `DiscoveredAlbums` table in the internal application database (`new-albums-discovery.db`). Saved records maintain a reference to their originating bucket and are assigned a `Status` of `Pending`.

### Feature 3: Notification & Delivery
The app notifies the user of the newly discovered albums via Telegram.

- **Notification Generation:** Iterates through all albums in `new-albums-discovery.db` with a `Pending` status.
- **Link Formatting:** Generates a YouTube search URL for each album using the format: `https://www.youtube.com/results?search_query={{artist}}+{{album}}` (properly URL-encoded).
- **Delivery:** Sends a single, consolidated Telegram message containing all generated URLs.
- **State Update:** Updates the `Status` of all processed albums in `new-albums-discovery.db` from `Pending` to `SendForReview`.

## 4. Technical Constraints & Architecture
- **Framework:** .NET 10 Worker Service.
- **Datastore:** SQLite (Dual database architecture).
- **ORM / Migrations:** EF Core Migrations for the internal application database.
- **AI Integration:** Gemini API.
- **Notifications:** Telegram Bot API.
- **Architecture Guidelines:** Must strictly adhere to the repository's `constitution` (SOLID, DDD, Zero-Dependency Domain, and 100% TDD branch coverage).
