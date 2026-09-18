using Scdl.Core.Results;

namespace Scdl.Core.Tests;

public sealed class ScdlExitCodeTests
{
    [Test]
    [Arguments(ScdlErrorCode.UnsupportedResource, ScdlExitCode.Usage)]
    [Arguments(ScdlErrorCode.PresetNotOffered, ScdlExitCode.Usage)]
    [Arguments(ScdlErrorCode.NotFound, ScdlExitCode.Unavailable)]
    [Arguments(ScdlErrorCode.NoPlayableStream, ScdlExitCode.Unavailable)]
    [Arguments(ScdlErrorCode.RungNotServed, ScdlExitCode.Unavailable)]
    [Arguments(ScdlErrorCode.Unauthorized, ScdlExitCode.Unauthorized)]
    [Arguments(ScdlErrorCode.MuxerUnavailable, ScdlExitCode.MissingDependency)]
    [Arguments(ScdlErrorCode.MuxerFailed, ScdlExitCode.Failure)]
    [Arguments(ScdlErrorCode.ClientIdUnavailable, ScdlExitCode.Failure)]
    [Arguments(ScdlErrorCode.Unspecified, ScdlExitCode.Failure)]
    public async Task For_maps_a_failure_onto_its_exit_code(ScdlErrorCode code, int expected)
    {
        await Assert.That(ScdlExitCode.For(code)).IsEqualTo(expected);
    }

    /// <summary>
    /// The contract a script relies on: a non-zero exit means no file landed.
    /// A new mapping that returned <see cref="ScdlExitCode.Success"/> for a
    /// failure would make <c>scdl get ... &amp;&amp; play</c> play nothing.
    /// </summary>
    [Test]
    public async Task No_failure_maps_to_success()
    {
        foreach (var code in Enum.GetValues<ScdlErrorCode>())
        {
            if (code is ScdlErrorCode.None)
            {
                continue;
            }

            await Assert.That(ScdlExitCode.For(code)).IsNotEqualTo(ScdlExitCode.Success);
        }
    }

    /// <summary>A value outside the enum is still a failure, not a crash.</summary>
    [Test]
    public async Task For_answers_an_unknown_code_with_the_generic_failure()
    {
        await Assert.That(ScdlExitCode.For((ScdlErrorCode)9999)).IsEqualTo(ScdlExitCode.Failure);
    }
}
