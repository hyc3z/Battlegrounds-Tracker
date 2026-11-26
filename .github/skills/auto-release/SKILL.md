---
name: auto-release
description: Default commit, push, and GitHub Release tagging workflow for Battlegrounds-Tracker. Use automatically after completing code changes in this repository without asking for confirmation.
---

# Auto-Release Workflow for Battlegrounds-Tracker

In this repository, packaging and publishing a GitHub Release via git tag is the **default behavior** after completing code changes and local build verification. Do not ask the user whether to create a release unless they explicitly ask to skip releasing.

## Version & Tag Conventions

- **`master` (v1 stable)**:
  - Find the latest `v0.1.x` tag (`git tag --sort=-v:refname | grep '^v0\.1\.'`).
  - Bump the patch number (`v0.1.N` -> `v0.1.(N+1)`).
  - Push to `origin/master` and push the tag (`git push origin v0.1.(N+1)`).
  - Published as the **Latest** stable GitHub Release by `.github/workflows/build.yml`.
- **`v2` (experimental automation / LLM / replay branch)**:
  - Find the latest `v2.0.0-alpha.x` tag (`git tag --sort=-v:refname | grep '^v2\.'`).
  - Bump the prerelease number (`v2.0.0-alpha.N` -> `v2.0.0-alpha.(N+1)`).
  - Push to `origin/v2` and push the tag (`git push origin v2.0.0-alpha.(N+1)`).
  - Hyphenated tags are automatically marked as **Pre-release** by `.github/workflows/build.yml` so they coexist cleanly with v1's `Latest` release.

## Steps

1. Run local build verification (`.\build.ps1 -Configuration Release -SkipRestore`).
2. Restore any line-ending-only drift on `BattlegroundsTracker/Properties/Strings.Designer.cs` if untouched.
3. Commit changes and push to the target branch (`origin/master` or `origin/v2`).
4. Create and push the next version tag pointing to `HEAD`.
5. Wait for the GitHub Actions `Build` workflow (`gh run watch <run-id> --exit-status`) and verify the published release asset with `gh release view <tag>`.
