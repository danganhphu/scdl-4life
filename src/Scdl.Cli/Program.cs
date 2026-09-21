using Scdl.Cli.Rendering;
using Scdl.Core.Results;

namespace Scdl.Cli;

internal static class Program
{
    private static readonly InvocationConfiguration Configuration = new()
    {
        // Own the exception path, so a ScdlException prints as the sentence
        // it already is rather than as a stack trace.
        EnableDefaultExceptionHandler = false,

        // Gives in flight transfers a moment to unwind on Ctrl+C and drop
        // their .part files instead of being killed mid write.
        ProcessTerminationTimeout = TimeSpan.FromSeconds(5),
    };

    private static Task<int> Main(string[] args)
        => RunAsync(CommandFactory.CreateRoot().Parse(args), new(AnsiConsole.Console));

    /// <summary>
    /// Invokes a parsed command and turns whatever escapes it into an exit code.
    /// </summary>
    /// <remarks>
    /// Separate from <c>Main</c> so the mapping can be exercised against a
    /// command that throws on purpose. An entry point is not testable, and this
    /// is the part worth testing: the exit code is what a script reads.
    /// </remarks>
    internal static async Task<int> RunAsync(ParseResult parseResult, ConsoleRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(renderer);

        try
        {
            return await parseResult.InvokeAsync(Configuration);
        }
        catch (ScdlException e)
        {
            renderer.Error(e.Message);

            // The exit code is this tool's machine-readable output: a script can
            // tell "install ffmpeg" from "that track is gone" without reading
            // the English.
            return ScdlExitCode.For(e.Code);
        }
        catch (OperationCanceledException)
        {
            renderer.Warning("Cancelled.");

            return ScdlExitCode.Cancelled;
        }
        catch (HttpRequestException e)
        {
            renderer.Error($"Network error: {e.Message}");

            return ScdlExitCode.Failure;
        }
    }
}
