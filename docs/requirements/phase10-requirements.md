# Phase 10 Requirements: Gemini API Integration (Discovery Query & Persistence)

## 1. Objective
Complete the Gemini API integration by executing the **Discovery Query** step (`DiscoveryQueryPromptStep`) against the Gemini API. Process the responses, deduplicate the discovered albums, and persist them to the internal database (`new-albums-discovery.db`) with a `Pending` status. Provide a comprehensive summary report of the discovery run.

## 2. Gemini API Call & Retry Logic
Update the `DiscoveryQueryPromptStep` to call the Gemini API using the dynamically rendered discovery prompt.

- **Console Output**: The system MUST STOP printing the Discovery Query prompt to the console. It will now only be sent to the Gemini API.
- **Retry Strategy**: Apply the exact same retry strategy as Phase 9 for transient failures. Use the configured intervals (default: `[10, 30, 180]`).
- **Abandonment**: If all 3 retries fail (4 attempts total), mark the bucket as abandoned. Print to the console: `"AIDiscovery: bucket '{BucketName}' ABANDONED — {Reason}"` (reusing the Phase 9 abandonment format).

## 3. Result Processing & Persistence
When a successful response is received from the Gemini API, it will contain a JSON list of albums (Album Name, Artist). 

- **Deduplication**: Deduplicate the albums. An album is considered a duplicate if the exact `(Artist, Album)` pair (case-insensitive, trimmed) has already been discovered in a previous bucket during this run, or if it already exists in the `DiscoveredAlbums` table from a past run.
- **Persistence**: Save the newly discovered (unique) albums to the `DiscoveredAlbums` table. 
  - Each record must include a reference to the originating bucket's name.
  - Each record must be assigned an initial `Status` of `Pending`.
- **Success Notification**: For each bucket that completes successfully, print to the console: `"Processed Successfully - {Count} albums found"` (where `{Count}` is the number of valid albums parsed and returned for that specific bucket, prior to global deduplication filtering, or after? *Recommendation: Use the count of newly persisted unique albums from this bucket*).

## 4. Pipeline Reporting
The `ReportPublicationStage` (Stage 3) must be expanded to output a comprehensive statistical report of the discovery run. The report must contain the following metrics:

1. **Total Buckets**: Total number of buckets processed in this run.
2. **Buckets Skipped**: Number of buckets abandoned due to API failures.
3. **Empty Buckets**: Number of buckets that were successfully processed but resulted in 0 albums discovered.
4. **Total Albums Discovered**: Total number of newly discovered, unique albums saved to the database in this run.
5. **Average Albums per Bucket**: The average number of albums discovered per bucket (Exclude skipped buckets and buckets with 0 albums from this calculation).
6. **Average Albums per Level 1 Bucket**: Average albums discovered for Level 1 (Country) buckets.
7. **Average Albums per Level 2 Bucket**: Average albums discovered for Level 2 (Language) buckets.
8. **Average Albums per Level 3 Bucket**: Average albums discovered for Level 3 (Genre) buckets.
9. **Highest Yield Bucket**: The name of the bucket that produced the highest number of albums discovered.
10. **Lowest Yield Bucket**: The name of the bucket that produced the lowest number of albums discovered (ignoring empty/skipped buckets). 

*Note for implementer: To support these metrics, the `BucketProcessingState` or `AIDiscoveryPipelineContext` will need to track the number of albums yielded per bucket and the bucket types.*
