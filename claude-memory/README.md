# claude-memory

Notes an AI agent - or a human returning after a month - should read before
changing this repo. Everything here was learned the expensive way: by a build
failing, an analyzer objecting, or the tool misbehaving against the live API.

`AGENTS.md` at the repo root is the short version: the rules you must follow.
This folder is the long version: **why** those rules exist, and what happens if
you ignore them.

| Folder | What is in it |
| --- | --- |
| `domain/` | Facts about SoundCloud's api-v2 that are not documented anywhere else. |
| `decisions/` | Choices that were argued out, with the reasoning and the alternatives rejected. |
| `traps/` | Things that fail silently on .NET 10, and how each one was diagnosed. |
| `workflow/` | Conventions, and an honest list of what is not finished. |

## How to use it

Read `traps/` before touching the build configuration. Read `domain/` before
touching anything that talks to SoundCloud. Read the relevant `decisions/` entry
before reversing a design choice, because most of them already have a rejected
alternative written down and you may be about to re-propose it.

## How to add to it

When something costs more than ten minutes to work out, write it down here in
the same shape: what was observed, what the cause turned out to be, and what to
do instead. A note that only says "X is broken" is worth very little; a note
that says how X was proven broken is worth the next hour someone would have
spent.

Do not record anything that belongs in a commit message, and do not record
secrets, tokens or key fingerprints. **This folder is committed**, so it follows
the repo to every machine and to anyone with access - write it for a reader who
is not you.
