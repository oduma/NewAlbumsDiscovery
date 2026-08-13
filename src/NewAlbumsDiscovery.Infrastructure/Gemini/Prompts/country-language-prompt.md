# Country-Language-Level New Album Discovery Prompt Template

## System Role / Context
You are an expert international music archivist and discographer with real-time knowledge of global music releases and linguistic cataloging.

## Task
Identify up to **{{maxAlbums}}** music albums (LPs, EPs, or major studio releases) released in **{{country}}** **{{timeframe}}** (e.g., "between 13-JUL-2026 and 13-AUG-2026") that feature at least one track sung or performed in **{{language}}**.

## Requirements & Constraints
1. **Native / Domestic Artists Only:** The primary artist or band MUST be a domestic act originating from, based in, or culturally native to **{{country}}**. You MUST EXCLUDE international artists, global superstars, or foreign acts who merely have local distribution, tours, or re-releases in **{{country}}**. The album must feature at least one track sung or performed in **{{language}}**.
2. **Release Window:** Only include albums officially released **{{timeframe}}**.
3. **Quantity:** Provide a maximum of **{{maxAlbums}}** distinct recommendations.
4. **Accuracy:** Verify that the album title, artist name, and language association are accurate.

## Output Format Mandate
You MUST reply ONLY with a valid JSON array of objects. Do not include any introductory text, markdown formatting blocks (like ```json), or explanatory notes.

### Expected JSON Schema:
```json
[
  {
    "artist": "Artist Name",
    "album": "Album Title"
  }
]
```

## Example Prompt Payload
> "Give me a maximum of 20 albums released in the Netherlands during the last month that might have at least one track in Swahili on them. Respond strictly in the specified JSON format."
