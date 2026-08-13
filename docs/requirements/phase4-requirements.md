# Phase 4 Requirements: Generative AI Engine Integration (Gemini)

## 1. Objective
Integrate the Google Gemini API to serve as the core intelligence engine for the New Albums Discovery application. This engine will execute the country/language/genre prompts to identify relevant albums according to the application's constraints. 

## 2. Environment Setup & Configuration
- **API Key Storage**: The Gemini API key will be read from a file located at `z-com-ai/gemini-api-key.txt`.
- **Environment Variable Mapping**: As defined in the updated Phase 2 setup scripts, because the file does NOT end in `_path.txt`, the scripts will treat its contents as a raw string and assign it to the machine-level environment variable `NewAlbumsDiscovery__GeminiApiKey`.
- **.NET Configuration**: The application will automatically bind this environment variable via the standard `IConfiguration` provider to an internal options class (e.g., `GeminiOptions`).

## 3. NuGet Dependencies
- **Package**: The application will use the `Mscc.GenerativeAI` NuGet package to access the Gemini API. 
- **Reasoning**: This SDK natively supports API key authentication directly, making it an ideal choice for this architecture without requiring a handcrafted REST implementation.

## 4. Discovery Engine Architecture
- **Prompt Execution**: Implement a service (e.g., `GeminiDiscoveryService`) responsible for formatting the markdown templates (e.g., `country-language-genre-prompt.md`) with the user's specific parameters.
- **Model Selection**: The service will configure the `Mscc.GenerativeAI` client to use an appropriate Gemini model (e.g., `gemini-1.5-flash` or `gemini-1.5-pro` depending on the complexity of the extraction).
- **JSON Parsing Mandate**: The prompt templates explicitly mandate a JSON response. The `GeminiDiscoveryService` must deserialize the AI's JSON output strictly into internal .NET entities (e.g., `DiscoveredAlbum`).
- **Error Handling**: 
  - Handle rate limits (HTTP 429) gracefully, potentially implementing exponential backoff.
  - Handle malformed JSON responses by retrying the prompt or logging a failure.

## 5. Integration with Phase 3
- The Gemini engine will be invoked by the Background Aggregator Engine developed in Phase 3.
- Discovered albums returned by Gemini will be cross-referenced with the internal `new-albums-discovery.db` to prevent processing duplicates.
- The workflow will operate in atomic EF transactions as specified in the Phase 3 constraints.
