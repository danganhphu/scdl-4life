using System.CommandLine;
using Scdl.Cli.Rendering;
using Scdl.Core;
using Scdl.Core.Results;
using Spectre.Console.Testing;

namespace Scdl.Cli.Tests;

/// <summary>
/// What the process actually returns, through the real invocation path rather
/// than through <see cref="ScdlExitCode.For"/> alone. The exit code is the
/// tool's machine-readable output, so a script can tell "install ffmpeg" from
/// "that track is gone" without parsing English.
/// </summary>
public sealed class ProgramExitCodeTests
{
    /// <summary>Invokes a command whose action does nothing but fail the way a real one might.</summary>
    private static async Task<(int Exit, string Output)> RunFailing(Exception exception)
    {
        var command = new RootCommand("test");
        command.SetAction((ParseResult _, CancellationToken _) => Task.FromException<int>(exception));

        var console = new TestConsole();
        var exit = await Program.RunAsync(command.Parse([]), new(console));

        return (exit, console.Output);
    }

    [Test]
    [Arguments(ScdlErrorCode.MuxerUnavailable, ScdlExitCode.MissingDependency)]
    [Arguments(ScdlErrorCode.Unauthorized, ScdlExitCode.Unauthorized)]
    [Arguments(ScdlErrorCode.NotFound, ScdlExitCode.Unavailable)]
    [Arguments(ScdlErrorCode.NoPlayableStream, ScdlExitCode.Unavailable)]
    [Arguments(ScdlErrorCode.UnsupportedResource, ScdlExitCode.Usage)]
    [Arguments(ScdlErrorCode.MuxerFailed, ScdlExitCode.Failure)]
    [Arguments(ScdlErrorCode.Unspecified, ScdlExitCode.Failure)]
    public async Task A_failure_exits_with_the_code_its_reason_maps_to(ScdlErrorCode code, int expected)
    {
        var (exit, _) = await RunFailing(new ScdlException("something went wrong", code));

        await Assert.That(exit).IsEqualTo(expected);
    }

    /// <summary>
    /// The reason ScdlException exists: its message is already written for a
    /// person, so it prints as the sentence it is. Anything else escaping to the
    /// top is a bug and keeps its stack trace.
    /// </summary>
    [Test]
    public async Task A_failure_prints_its_sentence_and_not_a_stack_trace()
    {
        var (_, output) = await RunFailing(new ScdlException("ffmpeg is not on PATH.", ScdlErrorCode.MuxerUnavailable));

        await Assert.That(output.Contains("ffmpeg is not on PATH.", StringComparison.Ordinal)).IsTrue();
        await Assert.That(output.Contains("ScdlException", StringComparison.Ordinal)).IsFalse();
        await Assert.That(output.Contains("   at ", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>128 + SIGINT, which is what a shell expects from an interrupted process.</summary>
    [Test]
    public async Task Ctrl_c_exits_with_the_conventional_signal_code()
    {
        var (exit, output) = await RunFailing(new OperationCanceledException());

        await Assert.That(exit).IsEqualTo(ScdlExitCode.Cancelled);
        await Assert.That(output.Contains("Cancelled", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>
    /// A network failure is the one common case with no ScdlException wrapping
    /// it, and an unhandled one would print a stack trace at a user who only
    /// needs to know their connection dropped.
    /// </summary>
    [Test]
    public async Task A_network_failure_is_reported_as_one()
    {
        var (exit, output) = await RunFailing(new HttpRequestException("connection refused"));

        await Assert.That(exit).IsEqualTo(ScdlExitCode.Failure);
        await Assert.That(output.Contains("Network error", StringComparison.Ordinal)).IsTrue();
        await Assert.That(output.Contains("connection refused", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>A command that succeeds owns its own exit code; nothing here should rewrite it.</summary>
    [Test]
    public async Task A_command_that_returns_keeps_its_own_exit_code()
    {
        var command = new RootCommand("test");
        command.SetAction((ParseResult _, CancellationToken _) => Task.FromResult(ScdlExitCode.Unavailable));

        var console = new TestConsole();
        var exit = await Program.RunAsync(command.Parse([]), new(console));

        await Assert.That(exit).IsEqualTo(ScdlExitCode.Unavailable);
        await Assert.That(console.Output).IsEmpty();
    }

    /// <summary>
    /// Anything that is not one of the three handled shapes is a bug, and a bug
    /// deserves its stack trace rather than a tidy sentence.
    /// </summary>
    [Test]
    public async Task An_unexpected_exception_is_left_to_escape()
    {
        var command = new RootCommand("test");

        command.SetAction((ParseResult _, CancellationToken _)
            => Task.FromException<int>(new InvalidOperationException("a real bug")));

        await Assert.ThrowsAsync<InvalidOperationException>(async ()
            => await Program.RunAsync(command.Parse([]), new(new TestConsole())));
    }
}
