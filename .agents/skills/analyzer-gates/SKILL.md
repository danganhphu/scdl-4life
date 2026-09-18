---
name: analyzer-gates
description: Why the build fails on warnings here, and the order to try when a rule fires. Use this when an analyzer blocks a build, before adding a NoWarn, a pragma or a GlobalSuppressions entry.
---

# Analyzers are the gate, not advice

`Directory.Build.props` sets `TreatWarningsAsErrors`, so every compiler and
analyzer finding fails the build. `WarningsNotAsErrors` exempts NU1901-NU1904
only, so a CVE advisory published against a transitive package does not break an
unrelated build the morning it lands.

Two properties are easy to confuse and one of them does nothing:

- **`TreatWarningsAsErrors`** is the boolean. This is the one.
- **`WarningsAsErrors`** takes a *list of warning IDs* plus the token `nullable`.
  `<WarningsAsErrors>true</WarningsAsErrors>` parses `true` as a warning ID that
  does not exist and escalates nothing. A repo that writes it believes it is
  strict and is not.

`GenerateDocumentationFile` is on for every project for a related reason:
without it the compiler never binds doc comments, so RCS1139 and friends never
fire on the build while Rider reports them anyway. The symptom is an IDE that
complains about something CI is happy with.

# When a rule fires

Work down this list, and only move down when the one above genuinely does not
apply.

1. **Fix the code.** Nearly every finding in this repo pointed at something worth
   changing, including two in code written minutes earlier.
2. **Turn the rule off in `.editorconfig`**, with a comment saying why, when it
   does not suit a whole folder. CA1707 is off for the test projects that way.
   Check which section the line lands in - appending to the end of the file once
   put rules inside `[tests/**/*.cs]`, where they proved nothing.
3. **Suppress in `GlobalSuppressions.cs`**, per member or per type, with a
   Justification that explains what the code does that the rule cannot see.
4. **`#pragma`**, only for a suppression that must be scoped tighter than a
   member.

"False positive" and "by design" are not justifications.

## The one suppression that exists

CA1708 on `TrackStreams`: two C# 14 `extension(...)` blocks in one static class
emit two compiler-generated grouping types with the same name. Nothing can call
those types from any language, so the case-insensitive collision the rule guards
against cannot happen. Re-check after an analyzer update.

## Findings worth knowing before they bite

- **CA1716** - `Error` is a reserved word in Visual Basic. Hence `ScdlError`.
- **CA1000** - no static members on generic types. Factories live on the
  non-generic `Result`, the way `Task`/`Task<T>` do it.
- **CA1859** - an internal helper taking `IReadOnlyDictionary` is told to take
  the concrete `Dictionary`. In test helpers, just take the concrete type.
- **CA2016** - forward the `CancellationToken` a lambda receives, including in
  Moq callbacks, or pass `CancellationToken.None` explicitly.
- **RCS1093** - "file contains no code" fires on an empty `GlobalSuppressions.cs`;
  it is scoped off for that path in `.editorconfig`.

## A caution about ReSharper and Rider suggestions

Read what the hint actually says before applying it. The suggested `.Where()`
rewrite over `FrozenDictionary.Values` warns in its own text that "another
GetEnumerator will be used" - it boxes the struct enumerator. Attribute grouping
is controlled by `resharper_force_attribute_style` in `.editorconfig`, which is
set to `separate`; if attributes keep getting merged, fix the setting rather than
the files.
