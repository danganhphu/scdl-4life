using Microsoft.Extensions.Options;

namespace Scdl.Core.SoundCloud;

/// <summary>
/// Validates <see cref="SoundCloudOptions"/> against the DataAnnotations on its
/// properties.
/// </summary>
/// <remarks>
/// The body is generated. <see cref="OptionsValidatorAttribute"/> makes the
/// options source generator emit the <see cref="IValidateOptions{TOptions}"/>
/// implementation at compile time, substituting non-reflecting versions of the
/// attributes - <c>__SourceGen__RangeAttribute</c> in place of
/// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/> and so on.
/// That is what makes attribute validation usable here at all: the runtime
/// <c>ValidateDataAnnotations()</c> path reflects, carries
/// <c>RequiresUnreferencedCode</c>, and produces IL2026 under
/// <c>PublishAot</c>.
/// <para>
/// Nothing else is needed; do not also call <c>ValidateDataAnnotations()</c>.
/// </para>
/// </remarks>
[OptionsValidator]
internal sealed partial class ValidateSoundCloudOptions : IValidateOptions<SoundCloudOptions>;
