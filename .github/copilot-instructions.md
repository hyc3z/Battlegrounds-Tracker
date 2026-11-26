# Battlegrounds-Tracker Repository Instructions

- **Default Release Behavior**: After completing code changes and verifying builds in this repository, automatically commit, push to the active target branch (`master` for v1 or `v2` for v2), bump and push the next matching GitHub Release tag (`v0.1.x` on `master`, `v2.0.0-alpha.x` on `v2`), and verify the GitHub Actions release completes — do not ask whether to package/release unless the user explicitly says to skip it. See `.github/skills/auto-release/SKILL.md` for details.
- **UI Language**: Keep user-facing settings, overlay labels, tooltips, and automation/AI logs in Simplified Chinese (`简体中文`).
