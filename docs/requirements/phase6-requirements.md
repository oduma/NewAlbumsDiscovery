# Phase 6 Requirements: Bucket Aggregator Engine Refinements

## 1. Objective
Refine the Phase 3 Music Preference Aggregator by introducing a data-normalization pre-pass and a bucket-filtering post-pass. This ensures that the generated buckets are highly targeted to specific countries, eliminating redundant continent-level buckets and filtering out invalid/generic regions.

## 2. Asset Integration
- A new file has been created at `docs/specs/assets/country-synonyms.json`.
- **Rule**: Just like in Phase 5, this file MUST be **copied** (not moved) into the appropriate `src/` directory (e.g., `src/NewAlbumsDiscovery.Infrastructure/MusicAggregator/Assets` or similar) so the `BucketAggregatorEngine` can read it.
- The existing `countries-languages.json` file (which now includes continents) will also be utilized in this phase as a master validation list.

## 3. Pre-Processing: Country Normalization
Before the engine performs the 3-level cascading threshold aggregation, it must normalize the raw track data.
- **Action**: Intercept the incoming `LovedTracks` and map their `CountryOrRegion` value against the `country-synonyms.json` dictionary.
- **Example**: If a track has `"United States"`, it is normalized to `"USA"`. If a track has `"Great Britain"`, it is normalized to `"UK"`.
- **Why**: This guarantees that tracks labeled with synonyms contribute to the *same* bucket threshold, rather than splintering into weak, sub-threshold buckets.

## 4. Post-Processing: The Second Pass (Filtering)
After the `BucketAggregatorEngine` finishes creating the buckets (but **before** they are yielded and saved to the database), a second filtering pass must run over the generated buckets.

### 4.1 Rule A: Continent Fallback Elimination
- **Context**: The `countries-languages.json` file now contains a `Continent` mapping for every valid country.
- **Action**: If a bucket's `Country` name exactly matches a continent (e.g., "Europe", "Asia", "North America", "South America", "Africa", "Oceania"), the engine must check if there are *any* other buckets representing a specific country *within* that continent.
- **Condition**: If at least one specific country bucket exists for that continent (e.g., a "France" bucket exists, which is in "Europe"), the continent-level bucket (e.g., "Europe") MUST be **deleted/dropped** from the final results. The tracks inside it are intentionally discarded.
- **Edge Case Constraint**: "Australia" is both a country and a continent. The "Country" classification takes precedence. "Australia" must NEVER be deleted by this rule just because it shares a name with a continent.

### 4.2 Rule B: Invalid Country Deletion
- **Context**: Sometimes raw track data contains non-geographical regions (e.g., "Global/Various", "Unknown", or random strings).
- **Action**: If a bucket's `Country` name is NOT found in the `countries-languages.json` master list (either as a top-level Country key or as one of the associated Continent values), that bucket MUST be **deleted/dropped** from the final results.
