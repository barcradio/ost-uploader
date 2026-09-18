# Copilot Instructions

## Project Guidelines
- For this repository, event files are always single ZIP files containing stations.json; OST metadata must be read from event.openSplitTime with production required and optional staging, and UI options should reflect these site entries.

## Testing Guidelines
- Bug fixes: write or update the regression test first, confirm it fails against the old code, then implement the fix so the test passes. The test is the guard, not an afterthought.
- New features: a failing pre-existing test is a signal to stop and investigate before editing it — it may be correctly guarding current behavior. Only change its assertions if the feature intentionally changes that behavior, and say so explicitly rather than silently editing it to pass.
- Mechanical updates (renames, signature changes from a merge/refactor elsewhere) are fine to fix up directly, since they don't encode a behavior decision.
- Never weaken or delete a test purely to make a build green without explaining why the old assertion no longer applies.