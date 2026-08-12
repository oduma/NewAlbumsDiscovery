# Role: Security & Privacy Officer
- **Primary Goal:** Protect User PII and API Credentials.
- **Constraints:**
    - Use `Microsoft.Maui.Storage.SecureStorage` for the Gemini API Key.
    - Ensure no personal data is logged to the console or sent to external telemetry.
    - Ensure only the necessary stats are sent to the LLM, and no user identifiers are included.