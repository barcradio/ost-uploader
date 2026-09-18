COPILOT PROJECT RULES — ost-uploader

This short file highlights concise, actionable rules derived from the reference project and tuned for this codebase.

1) Preserve data formats
- Do not change CSV headers, exported file layout, or local database schemas without: (a) a design note in the PR, and (b) a migration path or backward-compatibility plan.

2) Keep changes small and reviewable
- Break large AI-generated refactors into smaller PRs. Each PR should be reviewable and focused on a single concern.

3) Follow .editorconfig / existing style
- Match the repository's spacing, indentation, and naming conventions. If no .editorconfig exists, follow existing surrounding files.

4) Tests and verification
- Add unit tests for logic changes. For UI changes, include manual verification steps and screenshots where relevant.

5) Dependency hygiene
- Do not add new third-party dependencies for a single small feature. If necessary, document why and add tests and CI verification.

6) Commit and PR notes for AI-assisted changes
- In the PR body, add a line: "Parts of this change were generated or assisted by GitHub Copilot." List files that were primarily generated and any manual edits.

Suggested additional rules (not present in the reference)
- Nullability: enable nullable reference types for new files or follow existing project setting; prefer explicit null handling in public APIs.
- Logging: use ILogger<T> for non-UI components. Don't add console-only logging in production code.
- Secrets: add a pre-merge check to ensure no secrets are present in committed files (use existing company or OSS scanning tools).
