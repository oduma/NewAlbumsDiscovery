# Phase 8 Requirements: Instrumental Bucket Processing

## 1. Objective
Expand the Phase 7 `BucketProcessingStage` to correctly handle buckets that were previously deferred: Instrumental music. Instrumental buckets require different prompts than standard language-based buckets because they have no lyrics.

## 2. Asset Modifications
- **Deleted**: `docs/specs/assets/prompts/country-genres-prompt.md` is no longer needed in the architecture and has been deleted. **Claude Code Action Required**: You MUST delete the copied version of this file from `src/` (e.g., `src/NewAlbumsDiscovery.Infrastructure/Gemini/Prompts/country-genres-prompt.md`) if it exists.
- **Created**: `docs/specs/assets/prompts/country-instrumental-prompt.md` (asks strictly for instrumental albums).
- **Created**: `docs/specs/assets/prompts/country-instrumental-genres-prompt.md` (asks strictly for instrumental albums within a specific genre).
- **Rule**: These two new `.md` files must be copied to the appropriate `src/` directory alongside the other prompts.

## 3. Bucket Processing Logic (Instrumental Extension)
Update the routing logic from Phase 7 to process buckets where the `Language` equals `"Instrumental"`.

Just like in Phase 7, all processing in this phase results in the generated prompts being printed to the console (no actual API calls yet). The `{{maxAlbums}}` and `{{timeframe}}` variables continue to resolve dynamically from configuration/system clock.

### 3.1. Level 2 (Instrumental): Country + "Instrumental" Language
- **Condition**: The bucket has a Country and its Language is exactly `"Instrumental"`. It has NO Genre.
- **Templates**: `country-instrumental-prompt.md`
- **Substitutions**: Replace `{{country}}` with the bucket's Country. Replace `{{timeframe}}` and `{{maxAlbums}}`.
- **Action**: Print the rendered prompt to the console.

### 3.2. Level 3 (Instrumental): Country + "Instrumental" Language + Genre
- **Condition**: The bucket has a Country, its Language is exactly `"Instrumental"`, and it HAS a Genre.
- **Genre Expansion Override**: Do **NOT** perform a Genre Expansion pass for instrumental buckets. Instrumental genres (like Ambient, Lo-Fi, Classical) are universally standard enough that expanding them is counter-productive.
- **Templates**: `country-instrumental-genres-prompt.md` (Only 1 prompt required).
- **Substitutions**: 
  - Replace `{{country}}`. 
  - Replace `{{timeframe}}` and `{{maxAlbums}}`.
  - Replace `{{genres}}` **directly with the bucket's genre**. (Unlike the non-instrumental Level 3 which replaces it with `"TBD"`, we can fulfill this immediately because there is no expansion step).
- **Action**: Print the rendered prompt to the console.
