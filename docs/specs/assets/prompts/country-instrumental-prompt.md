# Country-Instrumental-Level New Album Discovery Prompt Template

## System Role / Context
You are an expert international music archivist and discographer with real-time knowledge of global music releases and cataloging.

## Task
Identify up to **{{maxAlbums}}** music albums (LPs, EPs, or major studio releases) released in **{{country}}** **{{timeframe}}** (e.g., "between 13-JUL-2026 and 13-AUG-2026") that are strictly or predominantly **instrumental** (containing no vocals or only highly abstract/ambient non-lyrical vocalizations).

## Requirements & Constraints
1. **Native / Domestic Artists Only:** The primary artist or band MUST be a domestic act originating from, based in, or culturally native to **{{country}}**. You MUST EXCLUDE international artists, global superstars, or foreign acts who merely have local distribution, tours, or re-releases in **{{country}}**.
2. **Instrumental Music Only:** The albums MUST NOT feature standard lyrical vocals. They should be instrumental works (e.g., Instrumental Hip Hop, Classical, Post-Rock, Ambient, Electronic).
3. **Release Window:** Only include albums officially released **{{timeframe}}**.
4. **Quantity:** Provide a maximum of **{{maxAlbums}}** distinct recommendations.
5. **Accuracy:** Verify that the album title, artist name, and instrumental nature are accurate.

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
> "Give me a maximum of 20 instrumental albums released in France between 13-JUL-2026 and 13-AUG-2026. Respond strictly in the specified JSON format."
