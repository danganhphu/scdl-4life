<!--
The title must be a Conventional Commit, lower case, no trailing period:
  feat(downloading): resume a partial transfer
A workflow checks it.
-->

## What changed

<!-- One or two sentences. What the reader would otherwise have to reconstruct from the diff. -->

## Why

<!-- The problem, not the solution. If an analyzer or a real download prompted this, say which. -->

## Checklist

- [ ] `./build.ps1 Test` is green, with no new warnings
- [ ] No claim anywhere implies a bitrate SoundCloud does not serve
- [ ] New failure modes carry a `ScdlErrorCode` and map to an exit code
- [ ] ASCII punctuation only, in code, comments and the commit message
- [ ] A load-bearing test was shown to fail before it passed
