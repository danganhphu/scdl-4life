# .agents

Instructions for coding agents, in the layout the wider ecosystem is settling
on: one folder per skill under `.agents/skills`, each holding a `SKILL.md` with
`name` and `description` frontmatter.

## How the two instruction sources divide

| Source | Loaded | Holds |
| --- | --- | --- |
| `AGENTS.md` | always | conventions, commands, project layout - the things that apply to every change |
| `.agents/skills/*/SKILL.md` | on demand, by `description` | procedures for one kind of task, and the traps specific to it |

A skill should tell an agent **how to do something here and what goes wrong**,
not restate the style rules. If a line belongs in every change, it belongs in
`AGENTS.md`.

## The skills

| Skill | Read it before |
| --- | --- |
| `soundcloud-ladder` | touching `Scdl.Core/Audio`, or answering anything about 320 kbps, WAV or "high quality" |
| `errors-and-exit-codes` | adding a way for something to go wrong |
| `tunit-testing` | adding or changing a test |
| `publish-native-aot` | publishing, or adding a dependency that reflects |
| `analyzer-gates` | an analyzer blocks the build |

`CLAUDE.md` forwards to `AGENTS.md`, and `.github/copilot-instructions.md` does
the same, so all three assistants read one set of rules.
