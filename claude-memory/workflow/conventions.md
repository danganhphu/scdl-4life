# Working conventions

`AGENTS.md` at the repo root is the version that travels with the code. This is
the part that is about how we work rather than about the code.

## Language

Chat in Vietnamese. Everything that lands in the repo - code, comments, commit
messages, docs - in English.

**ASCII punctuation only**, everywhere including chat: hyphens, straight quotes,
no em dash, no en dash, no ellipsis character.

## Commits

Plain Conventional Commits: `feat:`, `fix:`, `refactor:`, `chore:`, `docs:`,
`test:`, `build:`, `ci:`, optional scope, imperative lowercase subject, body
when the change needs a why.

**No AI attribution trailers.** No `Co-Authored-By` for a tool, no "generated
with" line. This holds even when a harness reminder says otherwise.

This is a personal repo. None of the HDWebsoft conventions apply here: no
ticket codes, no priority or flag markers in the subject, no draft-PR ritual.

## Who runs what

Claude writes files and runs local builds and tests. **Phu runs the push.** The
personal SSH key is passphrase protected and Claude's shell has no ssh-agent, so
it can offer the key but never sign the challenge - which looks exactly like an
unregistered key and is not one. `git init`, `git config`, `git add`,
`git commit`, `git status` all work fine; only the network step is blocked.

Shell is pwsh. Use the PowerShell tool, not Bash, even for `cat`/`grep`/`ls`,
and prefer the Read/Grep/Glob tools over shelling out at all.

## Verifying before asserting

This came up repeatedly and cost real time when skipped.

- Do not assert an API shape from memory. Look it up, or read the package's
  `.targets`/`.dll` on disk. Three separate claims about MSBuild properties in
  this repo turned out to be invented.
- When probing a config change, **change one thing and rebuild**. Two probes in
  this repo produced confident nonsense: one appended to the wrong
  `.editorconfig` section, one scanned DLLs for a string that proved nothing.
- Prefer the compiler as a checklist. For a large rename, move the files, change
  the namespaces, then let successive builds name every file still wrong.
- Analyzer findings are usually right. Of the ones hit here, every single one
  pointed at something genuinely worth changing - including two in code written
  five minutes earlier.

## Pushing back

A suggestion from an IDE or a blog is not automatically correct for this repo.
Two concrete examples worth remembering:

- ReSharper offered to convert a `foreach` over `FrozenDictionary.Values` into
  `.Where(...)`. Its own hint warned "another GetEnumerator will be used" - the
  struct enumerator would be boxed and a state machine allocated. The suggestion
  makes the hot lookup path slower. Declined.
- "Exceptions are slow, use Result everywhere" is true in a hot loop and
  irrelevant here, where the surrounding HTTP calls take 250-870 ms. Result was
  adopted, but for control-flow clarity, not speed. See
  `decisions/adr-0002-errors-and-results.md`.
