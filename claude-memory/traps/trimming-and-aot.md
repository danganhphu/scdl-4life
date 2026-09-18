# Trimming and Native AOT

`Scdl.Cli` publishes Native AOT, so the whole dependency graph has to survive
trimming. The failure mode is nasty: everything builds, the published binary
starts, and then a reflection path throws at run time on input that worked in
`dotnet run`.

## `Argument<Uri>` breaks every URL in the published binary

System.CommandLine resolves the converter for non-primitive argument types
through `TypeDescriptor`, which the trimmer removes. The published binary then
rejects **every** URL, valid or not:

```
Cannot parse argument '<anything>' for command 'formats' as expected type 'System.Uri'
```

It works perfectly under `dotnet run`, which is what makes it expensive to find.
Give any non-primitive command-line argument an explicit `CustomParser`. The
handwritten parser also produces a far better message for a genuine typo.

## `Spectre.Console.Cli` is not AOT appropriate

Documented as neither trimmable nor AOT appropriate by its maintainers, and
unlikely to change. Parsing stays on System.CommandLine; Spectre is used for
rendering only, and only its simple renderables - prompts and the type
conversion helpers behind them are the parts that do not survive.

## JSON goes through a source-generated context

`SoundCloudJsonContext` is source-generated, and
`JsonSerializerIsReflectionEnabledByDefault` is set to false so a forgotten
`[JsonSerializable]` fails loudly at run time instead of silently falling back
to reflection and then breaking only once trimmed.

The context is `internal`, which is fine for source generation and keeps it off
the public surface.

## Tagging is ATL, not TagLibSharp

ATL (`z440.atl.core`) parses containers by hand in managed code. A reflection
heavy tagger would not survive trimming.

## DataAnnotations on options are a trim hazard - unless source-generated

`RangeAttribute` and friends validate through reflection and carry
`RequiresUnreferencedCode`, so `ValidateDataAnnotations()` produces IL2026. That
is why the options are currently validated with plain `.Validate(...)` lambdas.

**There is a better answer that is not yet applied**: `[OptionsValidator]` on an
empty partial class source-generates the `IValidateOptions<T>` implementation,
substituting `__SourceGen__RangeAttribute` for the reflecting original. It is
AOT-compatible and automatically enabled by Microsoft.Extensions.Options v8+.
See `workflow/known-gaps.md`.

## Generic helpers that resolve services need annotating

A private generic helper calling `AddHttpClient<TClient, TImplementation>` needs
`[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]`
on its own type parameter, or IL2091 fires. The annotation has to be repeated at
every layer that forwards the type parameter.

## How to check

`IsAotCompatible` is on for every project in `Directory.Build.props`, not just
the one that publishes AOT, so a library reaching for reflection is caught at
build time rather than at publish time. A trimmed publish
(`-p:PublishAot=false -p:PublishTrimmed=true`) exercises the IL linker and
reports IL warnings without needing the C++ toolchain - useful when Native AOT
itself cannot link on the machine.
