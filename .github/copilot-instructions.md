# Copilot instructions

All development guidelines for this repo live in [AGENTS.md](../AGENTS.md):
conventions, commands, project layout and the analyzer policy. Read it first.

Task-specific procedures live in [`.agents/skills`](../.agents/skills), one
folder per skill. The ones worth knowing before writing any code here:

- **`soundcloud-ladder`** - SoundCloud stores no 320 kbps rung. Never suggest,
  label or default to a bitrate the CDN does not serve.
- **`errors-and-exit-codes`** - `Result<T>` for expected outcomes, exceptions for
  faults, and every failure carries a `ScdlErrorCode`.
- **`analyzer-gates`** - the build fails on warnings. Fix the code before
  reaching for a suppression.

Three rules that cause most of the churn if missed:

1. ASCII punctuation only in code, comments and commit messages - hyphens and
   straight quotes, no em dash, no ellipsis character.
2. Conventional Commits, and nothing else in the trailer.
3. One public type per file, `internal sealed` by default.
