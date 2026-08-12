# High Level Requirements: New Albums Discovery

## 1. Overview
The New Albums Discovery application is a cross-platform background worker (TSR / Windows Service / Linux Daemon) built with .NET 10. It is designed to run periodically (e.g., monthly) to aggregate user music preferences from a local SQLite database, leverage Gemini AI to discover new album releases matching those precise preferences, and notify the user via Telegram with YouTube search links for easy listening.

## 2. Core Features

### Feature 1: Music Preference Aggregator
The app analyzes the `LovedTracks` table (maintained externally) to generate highly specific "buckets" of music interests. The aggregation follows a cascading logic to find the optimal level of granularity without creating buckets that are too large or too sparse.

- **Level 1 (Country/Region):** Groups tracks by `CountryOrRegion`. If the count is smaller than `CountryRegionThreshold`, it forms a bucket. Otherwise, it cascades to the next level.
- **Level 2 (Language):** Groups the remaining tracks by `LanguagesJson`. A track with multiple languages is counted once for each language. If the count is smaller than `CountryRegionLanguageThreshold`, it forms a bucket. Otherwise, it cascades to the next level.
- **Level 3 (Genre):** Groups the remaining tracks by `GenresJson`. A track with multiple genres is counted once for each genre. This level always forms the final buckets.
- **Filtering & Persistence:** Any generated bucket with a track count smaller than `MinimumBucketThreshold` is discarded. Valid buckets are saved to a new SQLite table (e.g., `AggregatedBuckets`) in the same database.

### Feature 2: AI New Album Discovery
The app uses the buckets generated in Feature 1 to query the Gemini AI for recent album releases.

- **Dynamic Prompts:** Depending on the bucket type (Country, Country/Language, or Country/Language/Genre), a specific prompt template is used.
- **Configurability:** The timeframe for "recent" releases (e.g., "last month") is configurable via application settings.
- **Execution & Resilience:** Requests to the Gemini API are executed sequentially with configurable downtime between requests and a retry strategy to handle rate limits/transient errors.
- **Data Handling:** The AI returns results in JSON format (Album Name, Artist). The app deduplicates the results and saves them to a SQLite table (e.g., `DiscoveredAlbums`). Saved records maintain a reference to their originating bucket and are assigned a `Status` of `Pending`.

### Feature 3: Notification & Delivery
The app notifies the user of the newly discovered albums via Telegram.

- **Notification Generation:** Iterates through all albums in the database with a `Pending` status.
- **Link Formatting:** Generates a YouTube search URL for each album using the format: `https://www.youtube.com/results?search_query={{artist}}+{{album}}` (properly URL-encoded).
- **Delivery:** Sends a single, consolidated Telegram message containing all generated URLs.
- **State Update:** Updates the `Status` of all processed albums from `Pending` to `SendForReview`.

## 3. Technical Constraints & Architecture
- **Framework:** .NET 10 Worker Service.
- **Datastore:** SQLite.
- **AI Integration:** Gemini API.
- **Notifications:** Telegram Bot API.
- **Architecture Guidelines:** Must strictly adhere to the repository's `constitution` (SOLID, DDD, Zero-Dependency Domain, and 100% TDD branch coverage).
