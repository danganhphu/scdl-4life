// Assembly-level analyzer suppressions for Scdl.Core.
//
// Empty on purpose. It exists so that the first suppression this project ever
// needs lands somewhere a reviewer will see, instead of as an inline
// #pragma warning disable that nobody notices again.
//
// Reach for these in order, and only move down when the one above genuinely
// does not apply:
//
//   1. Fix the code. Nearly every finding in this repo so far pointed at
//      something worth changing, including two in code written minutes earlier.
//   2. Turn the rule off in .editorconfig, with a comment saying why, when the
//      rule does not suit the whole repo or a whole folder. CA1707 is off for
//      the test project that way.
//   3. Suppress here, for a single member, with a Justification that explains
//      the reasoning rather than restating the rule.
//   4. #pragma, only for a suppression that must be scoped tighter than a
//      member - a single statement inside one.
//
// Shape of an entry:
//
//   [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
//       "Design",
//       "CA1031:Do not catch general exception types",
//       Justification = "Best effort cleanup; the original failure is the one worth surfacing.",
//       Scope = "member",
//       Target = "~M:Scdl.Core.Downloading.TrackDownloader.TryDelete(System.String)")]
//
// A Justification of "false positive" or "by design" is not a justification.
// Say what the code does that the rule cannot see.


