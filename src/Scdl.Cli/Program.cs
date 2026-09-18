using Scdl.Cli.Rendering;
using Scdl.Core.Results;

namespace Scdl.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var parseResult = CommandFactory.CreateRoot().Parse(args);

        var configuration = new InvocationConfiguration
        {
            // Own the exception path, so a ScdlException prints as the sentence
            // it already is rather than as a stack trace.
            EnableDefaultExceptionHandler = false,

            // Gives in flight transfers a moment to unwind on Ctrl+C and drop
            // their .part files instead of being killed mid write.
            ProcessTerminationTimeout = TimeSpan.FromSeconds(5),
        };

        var renderer = new ConsoleRenderer(AnsiConsole.Console);

        try
        {
            return await parseResult.InvokeAsync(configuration);
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
