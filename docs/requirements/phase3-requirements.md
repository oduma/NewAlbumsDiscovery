# Phase 3 Requirements: Feature 1 - Music Preference Aggregator Engine & Storage

## 1. Goal
Implement Feature 1 (Music Preference Aggregator) as a background-threaded, deterministic domain calculation engine that reads `LovedTracks` from the external read-only `loved-tracks.db` database, executes a 3-level cascading threshold algorithm in memory, and atomically replaces the output in the `AggregatedBuckets` table inside the application's internal `new-albums-discovery.db` database.

---

## 2. Execution Context & Threading
- **Background Threading:** The calculation must run on a background thread (`Task.Run` or a background MediatR handler in `src/NewAlbumsDiscovery.Application/MusicAggregator`) isolated from the main worker host thread.
- **Cancellation Support:** Must accept and honor the host's `CancellationToken`. If a stop signal is received mid-calculation, the process aborts cleanly without writing partial results to SQLite.

---

## 3. Dual Database Setup & Read-Only Source (`loved-tracks.db`)
- **Read-Only Database Location:** Located in the `DB` folder under the user's home directory (`%USERPROFILE%\DB\loved-tracks.db` or `$HOME/DB/loved-tracks.db`).
- **Source Table (`LovedTracks`):** Maintained externally and treated as strictly read-only.
- **Table Columns Used:** `CountryOrRegion` (TEXT), `LanguagesJson` (TEXT containing a JSON string array, e.g. `["English", "Swahili"]`), `GenresJson` (TEXT containing a JSON string array, e.g. `["Indie Rock", "Post-Punk"]`).
- All records in `LovedTracks` are evaluated during each run.

---

## 4. Cascading Bucket Aggregation Engine (In-Memory)
The domain engine recalculates all buckets from scratch on every run using the following 3-level cascading algorithm:

```
[ All LovedTracks ]
        │
        ├── Level 1: Group by CountryOrRegion
        │       ├── Count < CountryRegionThreshold (e.g. 10)  ➔ Create Bucket: "Country"
        │       └── Count >= CountryRegionThreshold          ➔ Cascade to Level 2
        │
        ├── Level 2: Group by LanguagesJson (unpacked array)
        │       ├── Count < CountryRegionLanguageThreshold   ➔ Create Bucket: "Country/Language"
        │       └── Count >= CountryRegionLanguageThreshold  ➔ Cascade to Level 3
        │
        └── Level 3: Group by GenresJson (unpacked array)
                └── All Groups                               ➔ Create Bucket: "Country/Language/Genre"
```

### Level 1: CountryOrRegion Grouping
- Group all tracks by `CountryOrRegion` and calculate the track count for each country.
- **Threshold Rule (`CountryRegionThreshold`, default: 10):**
  - If `TrackCount < CountryRegionThreshold`: Form a **Country-level bucket** (e.g. `BucketName = "Romania"`, `BucketType = "Country"`, `Country = "Romania"`, `Language = null`, `Genre = null`).
  - If `TrackCount >= CountryRegionThreshold`: These tracks **do not** form a country bucket. They cascade down to Level 2 for further division.

### Level 2: Language Grouping (for cascading countries)
- For tracks from countries that exceeded Level 1 threshold, group them by `LanguagesJson`.
- **Multi-Language Expansion:** `LanguagesJson` contains a JSON string array of spoken languages (e.g. `["English", "Swahili"]`). If a track lists $N$ languages, it MUST be counted once for each language in that array (i.e. counted $N$ times total).
- **Threshold Rule (`CountryRegionLanguageThreshold`, default: 10):**
  - If `TrackCount < CountryRegionLanguageThreshold`: Form a **CountryLanguage-level bucket** (e.g. `BucketName = "Netherlands/Swahili"`, `BucketType = "CountryLanguage"`, `Country = "Netherlands"`, `Language = "Swahili"`, `Genre = null`).
  - If `TrackCount >= CountryRegionLanguageThreshold`: These tracks **do not** form a country-language bucket. They cascade down to Level 3.

### Level 3: Genre Grouping (for cascading country-languages)
- For tracks from `(Country, Language)` pairs that exceeded Level 2 threshold, group them by `GenresJson`.
- **Multi-Genre Expansion:** `GenresJson` contains a JSON string array of genres (e.g. `["Indie Rock", "Post-Punk"]`). If a track lists $M$ genres, it MUST be counted once for each genre in that array (i.e. counted $M$ times total).
- **Final Level Rule:** EVERY `(Country, Language, Genre)` combination at Level 3 forms a **CountryLanguageGenre-level bucket** (e.g. `BucketName = "UK/English/IndieRock"`, `BucketType = "CountryLanguageGenre"`, `Country = "UK"`, `Language = "English"`, `Genre = "IndieRock"`). No further cascading occurs.

### Minimum Bucket Threshold Filtering
- After evaluating Levels 1, 2, and 3, inspect all generated buckets.
- **Filter Rule (`MinimumBucketThreshold`, default: 2):** Any bucket with `TrackCount < MinimumBucketThreshold` MUST be filtered out and discarded.

---

## 5. Configurable Thresholds (`IOptions<AggregatorSettings>`)
All threshold values must be placed in `appsettings.json` (and `appsettings.Development.json`) under `NewAlbumsDiscovery:Aggregator` and strongly bound to `IOptions<AggregatorSettings>`:
```json
{
  "NewAlbumsDiscovery": {
    "Aggregator": {
      "CountryRegionThreshold": 10,
      "CountryRegionLanguageThreshold": 10,
      "MinimumBucketThreshold": 2
    }
  }
}
```
- **Class Definition (`NewAlbumsDiscovery.Application/MusicAggregator/AggregatorSettings.cs`):**
  - `CountryRegionThreshold`: int (default: 10)
  - `CountryRegionLanguageThreshold`: int (default: 10)
  - `MinimumBucketThreshold`: int (default: 2)
- **Binding Requirement:** Must be registered in `src/NewAlbumsDiscovery.Worker/Program.cs` or `AddApplicationServices()` via `.Configure<AggregatorSettings>(builder.Configuration.GetSection("NewAlbumsDiscovery:Aggregator"))`.

---

## 6. Internal Application Database Persistence (`new-albums-discovery.db`)
Once the in-memory calculation finishes successfully, the output is saved to the internal application database located in a `Database` subfolder under the application's running folder (`<AppBaseDirectory>/Database/new-albums-discovery.db`), which is owned by this application and maintained via EF Core Migrations (`AppDbContext`).

### Table Schema (`AggregatedBuckets`)
```sql
CREATE TABLE IF NOT EXISTS AggregatedBuckets (
    Id              TEXT    NOT NULL PRIMARY KEY,
    BucketName      TEXT    NOT NULL,
    BucketType      TEXT    NOT NULL, -- "Country", "CountryLanguage", "CountryLanguageGenre"
    Country         TEXT    NOT NULL,
    Language        TEXT    NULL,
    Genre           TEXT    NULL,
    TrackCount      INTEGER NOT NULL,
    CreatedAtUtc    TEXT    NOT NULL
);
```

### Atomic Swap Transaction
Persistence must execute inside a single EF Core / SQLite transaction against `AppDbContext` to ensure zero-downtime and zero risk of corrupted/partial data if interrupted:
```csharp
using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM AggregatedBuckets", cancellationToken);
dbContext.AggregatedBuckets.AddRange(newBuckets);
await dbContext.SaveChangesAsync(cancellationToken);
await transaction.CommitAsync(cancellationToken);
```

---

## 7. Architecture & Code Structure
Per `docs/constitution/DDD-architecture.md` and `coding-principles.md`:

1. **Domain Layer (`src/NewAlbumsDiscovery.Domain/MusicAggregator`)**:
   - `AggregatedBucket` (Entity with `BucketName`, `BucketType`, `Country`, `Language`, `Genre`, `TrackCount`).
   - `BucketAggregatorEngine` (Pure C# domain service executing the 3-level cascading logic and threshold filtering). Zero dependencies.
2. **Application Layer (`src/NewAlbumsDiscovery.Application/MusicAggregator`)**:
   - `AggregateMusicPreferencesCommand` & MediatR Command Handler.
   - Defines `ILovedTrackRepository` (queries read-only `loved-tracks.db`) and `IAggregatedBucketRepository` (writes to `new-albums-discovery.db`) interfaces.
3. **Infrastructure Layer (`src/NewAlbumsDiscovery.Infrastructure/Sqlite`)**:
   - `LovedTrackDbContext` / `LovedTrackRepository` (Queries external `loved-tracks.db`).
   - `AppDbContext` / `AggregatedBucketRepository` (Executes EF Core Migrations and atomic `DELETE + INSERT` transaction on `new-albums-discovery.db`).

---

## 8. Testing Mandate (100% Branch Coverage)
Unit tests in `tests/NewAlbumsDiscovery.Domain.Tests/MusicAggregator/` must achieve 100% branch coverage over `BucketAggregatorEngine`:
- **Boundary Checks:** Country count = 9 (forms Level 1 bucket) vs 10 (cascades to Level 2).
- **Boundary Checks:** Country/Language count = 9 (forms Level 2 bucket) vs 10 (cascades to Level 3).
- **Multi-Value Expansion:** Track with 2 languages is counted in both language groups. Track with 3 genres is counted in all 3 genre groups.
- **Minimum Threshold:** Buckets with count < 2 are discarded. Buckets with count >= 2 are retained.
- **Empty Datasets:** Handling empty `LovedTracks` list cleanly.
