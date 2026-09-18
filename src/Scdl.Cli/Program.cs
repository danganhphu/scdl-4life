using Scdl.Cli.Rendering;

namespace Scdl.Cli;

internal static class Program
{
    private const int ExitCancelled = 130;

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

            return CommandFactory.ExitFailure;
        }
        catch (OperationCanceledException)
        {
            renderer.Warning("Cancelled.");

            return ExitCancelled;
        }
        catch (HttpRequestException e)
        {
            renderer.Error($"Network error: {e.Message}");

            return CommandFactory.ExitFailure;
        }
    }
}
