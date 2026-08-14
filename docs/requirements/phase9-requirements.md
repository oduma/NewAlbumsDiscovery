# Phase 9 Requirements: Gemini API Integration (Genre Expansion Only)

## 1. Objective
Begin integrating the Gemini API into the `AIDiscovery` pipeline by executing the **Genre Expansion** step for Level 3 buckets. The Discovery Query step (sending the second prompt) remains out of scope for API integration and will continue to just print to the console, but it will now receive dynamically expanded genres instead of the hardcoded "TBD".

## 2. Configuration & Secrets
- **API Key**: The application must read the Gemini API key from the environment variable established in Phase 2: `NewAlbumsDiscovery__GeminiApiKey`. (The application should fail gracefully if missing during setup, or handle it via standard Options validation).
- **Gemini Model**: The target model must be configurable in `appsettings.json`. The default value must be exactly `"gemini-3.5-flash"`.
- **Retry Strategy**: The retry backoff intervals must be configurable in `appsettings.json` as an array of integers representing seconds. The default value must be `[10, 30, 180]`.

## 3. Genre Expansion Execution
Update the `GenreExpansionPromptStep` to actually call the Gemini API using the rendered `genre-expansion-prompt.md`.

- **Console Output**: The system MUST STOP printing the `genre-expansion-prompt.md` content to the console. It is now exclusively sent to the Gemini API.
- **Retry Logic**: If the API call fails (network error, rate limit, etc.), the engine must wait for the configured interval before trying again (Attempt 1 fails -> wait 10s -> Attempt 2 fails -> wait 30s -> Attempt 3 fails -> wait 180s -> Attempt 4 fails).

### 3.1. Abandonment (Failure Path)
- If the API call fails after exhausting all configured retries, the bucket is marked as **abandoned**.
- **Action**: Print to the console the Bucket Name, Track Count, and a clear message stating the bucket was abandoned due to API failure.
- The pipeline MUST skip the remaining steps (e.g., `DiscoveryQueryPromptStep`) for this abandoned bucket and immediately move on to the next bucket.

### 3.2. Success Path & Parsing
If the API call succeeds, parse the returned JSON array of strings.
- **Fallback**: If the reply contains an empty array, or if the JSON fails to parse despite a 200 OK HTTP success, fallback by using the bucket's original single genre.
- **Success**: If the reply contains a valid list of genres, join them into a single comma-separated string (e.g., `"Indie Pop, Synth Pop, Dream Pop"`).
- **State Handoff**: This resolved genre string must be stored in the pipeline context or passed down so that the next step can access it.

## 4. Discovery Query Modification
Update the `DiscoveryQueryPromptStep` to consume the resolved genres.

- Replace the `{{genres}}` variable in the `country-language-genres-prompt.md` template with the resolved comma-separated genre string from the previous step.
- The hardcoded `"TBD"` placeholder is no longer used for non-instrumental Level 3 buckets.
- **Console Output**: The system MUST CONTINUE to print the rendered Discovery Query prompt to the console. Sending this second prompt to the Gemini API is completely **OUT OF SCOPE** for this phase.

## 5. Pipeline Reporting
The `AIDiscoveryPipelineContext` must be updated to track the count of abandoned buckets.

- **ReportPublicationStage**: Update Stage 3 to print both statistics at the end of the run.
- **Example Output**: `"Pipeline Complete. Total Buckets Processed: 120 | Total Buckets Abandoned: 2"`
