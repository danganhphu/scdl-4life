using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scdl.Cli.Rendering;
using Scdl.Core.Downloading;
using Scdl.Core.Results;
using Scdl.Core.SoundCloud;
using Scdl.Core.SoundCloud.Models;
using Scdl.Core.Tagging;

namespace Scdl.Cli;

internal static class CommandFactory
{
    private static readonly Option<string?> OAuthOption =
        new("--oauth")
        {
            Description = "SoundCloud Go+ OAuth token. Unlocks the 256 kbps AAC rung.", Recursive = true,
        };

    private static readonly Option<bool> VerboseOption =
        new("--verbose", "-v") { Description = "Log what the client is doing to stderr.", Recursive = true, };

    /// <summary>
    /// Parses the URL argument by hand.
    /// </summary>
    /// <remarks>
    /// <c>Argument&lt;Uri&gt;</c> resolves its converter through TypeDescriptor,
    /// which the trimmer removes. The binary then fails at run time with
    /// "Cannot parse argument ... as expected type 'System.Uri'" for every
    /// input, valid or not. An explicit parser keeps the conversion in code the
    /// trimmer can see, and gives a far better message for a genuine typo.
    /// </remarks>
    private static Argument<Uri> CreateUrlArgument()
        => new("url")
        {
            Description = "A SoundCloud track or set URL.",
            CustomParser = result =>
            {
                var raw = result.Tokens.Count > 0 ? result.Tokens[0].Value : string.Empty;

                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                {
                    result.AddError($"'{raw}' is not an absolute URL.");

                    return null;
                }

                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    result.AddError($"'{raw}' must be an http or https URL.");

                    return null;
                }

                return uri;
            },
        };

    public static RootCommand CreateRoot()
        => new(
            "Download SoundCloud tracks at the highest bitrate SoundCloud actually serves. " +
            "There is no 320 kbps rung; 256 kbps AAC (Go+) or the uploader's original master is the ceiling.")
        {
            OAuthOption, VerboseOption, CreateGetCommand(), CreateFormatsCommand(),
        };

    private static Command CreateGetCommand()
    {
        var urlArgument = CreateUrlArgument();

        var outOption = new Option<DirectoryInfo>("--out", "-o")
        {
            Description = "Output directory.",
            DefaultValueFactory = _ => new DirectoryInfo(Environment.CurrentDirectory),
        };

        var presetOption = new Option<string?>("--format", "-f")
        {
            Description = "Force a preset, for example aac_256k or mp3_1_0.",
        };

        var preferOption = new Option<string>("--prefer")
        {
            Description = "'original' takes the uploader's master when offered; 'stream' always transcodes.",
            DefaultValueFactory = _ => "original",
        };

        preferOption.AcceptOnlyFromAmong("original", "stream");

        var overwriteOption = new Option<bool>("--overwrite")
        {
            Description = "Replace an existing file instead of appending a counter.",
        };

        var noTagsOption = new Option<bool>("--no-tags") { Description = "Skip metadata and cover art.", };

        var command = new Command("get", "Download a track or set.")
        {
            urlArgument,
            outOption,
            presetOption,
            preferOption,
            overwriteOption,
            noTagsOption,
        };

        command.SetAction((parseResult, cancellationToken) => RunGetAsync(
            new GetArguments(
                parseResult.GetValue(urlArgument)!,
                parseResult.GetValue(outOption)!,
                parseResult.GetValue(presetOption),
                string.Equals(parseResult.GetValue(preferOption), "stream", StringComparison.Ordinal)
                    ? SourcePreference.Stream
                    : SourcePreference.Original,
                parseResult.GetValue(overwriteOption),
                parseResult.GetValue(noTagsOption),
                parseResult.GetValue(OAuthOption),
                parseResult.GetValue(VerboseOption)),
            cancellationToken));

        return command;
    }

    private static Command CreateFormatsCommand()
    {
        var urlArgument = CreateUrlArgument();

        var command = new Command("formats", "Show the rungs SoundCloud offers for a URL, then exit.") { urlArgument, };

        command.SetAction((parseResult, cancellationToken) => RunFormatsAsync(
            parseResult.GetValue(urlArgument)!,
            parseResult.GetValue(OAuthOption),
            parseResult.GetValue(VerboseOption),
            cancellationToken));

        return command;
    }

    private sealed record GetArguments(Uri Url,
                                       DirectoryInfo OutputDirectory,
                                       string? Preset,
                                       SourcePreference Prefer,
                                       bool Overwrite,
                                       bool SkipTags,
                                       string? OAuthToken,
                                       bool Verbose);

    private static async Task<int> RunGetAsync(GetArguments arguments, CancellationToken cancellationToken)
    {
        await using var provider = BuildProvider(arguments.OAuthToken, arguments.Verbose);

        var console = AnsiConsole.Console;
        var renderer = new ConsoleRenderer(console);
        var soundCloud = provider.GetRequiredService<ISoundCloudClient>();
        var downloader = provider.GetRequiredService<ITrackDownloader>();
        var tagger = provider.GetRequiredService<IMediaTagger>();

        var tracks = await soundCloud.ResolveAsync(arguments.Url, cancellationToken);

        if (tracks.Count is 0)
        {
            renderer.Error("Nothing playable at that URL.");

            return ScdlExitCode.Unavailable;
        }

        var failures = 0;

        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            renderer.TrackHeading(track, i + 1, tracks.Count);

            var request = new DownloadRequest
            {
                Track = track,
                OutputDirectory = arguments.OutputDirectory.FullName,
                Prefer = arguments.Prefer,
                Preset = arguments.Preset,
                Overwrite = arguments.Overwrite,
            };

            try
            {
                var result = await TransferAsync(console, downloader, request, track, cancellationToken);

                if (result.FellBackFromOriginal)
                {
                    renderer.Note("  No original master offered; took the best transcoding instead.");
                }

                if (result.IsPreview)
                {
                    renderer.Warning("This is a 30 second preview, not the full track. The full stream needs --oauth.");
                }

                if (!arguments.SkipTags)
                {
                    await tagger.TryTagAsync(result.FilePath, track, cancellationToken);
                }

                renderer.Saved(result);
            }
            catch (ScdlException e) when (tracks.Count > 1)
            {
                // One bad track in a set should not abandon the rest.
                renderer.Error(e.Message);
                failures++;
            }
        }

        if (failures > 0)
        {
            renderer.Warning($"{failures} of {tracks.Count} track(s) failed.");

            return ScdlExitCode.Failure;
        }

        return ScdlExitCode.Success;
    }

    private static async Task<DownloadResult> TransferAsync(IAnsiConsole console,
                                                            ITrackDownloader downloader,
                                                            DownloadRequest request,
                                                            Track track,
                                                            CancellationToken cancellationToken)
    {
        DownloadResult? result = null;

        await console.Progress()
                     .AutoClear(true)
                     .Columns(
                         new TaskDescriptionColumn(),
                         new ProgressBarColumn(),
                         new PercentageColumn(),
                         new DownloadedColumn(),
                         new TransferSpeedColumn())
                     .StartAsync(async context =>
                     {
                         var task = context.AddTask(Markup.Escape(track.DisplayTitle), maxValue: 1d);

                         var progress = new Progress<TransferProgress>(tick =>
                         {
                             var total = tick.TotalBytes;

                             if (total is > 0)
                             {
                                 task.MaxValue = total.Value;
                                 task.IsIndeterminate = false;
                             }
                             else
                             {
                                 // HLS has no total until the final segment lands.
                                 task.IsIndeterminate = true;
                             }

                             task.Value = tick.BytesTransferred;
                         });

                         result = await downloader.DownloadAsync(request, progress, cancellationToken);

                         task.StopTask();
                     });

        return result ?? throw new ScdlException("The transfer produced no result.");
    }

    private static async Task<int> RunFormatsAsync(Uri url,
                                                   string? oauthToken,
                                                   bool verbose,
                                                   CancellationToken cancellationToken)
    {
        await using var provider = BuildProvider(oauthToken, verbose);

        var renderer = new ConsoleRenderer(AnsiConsole.Console);
        var soundCloud = provider.GetRequiredService<ISoundCloudClient>();

        var tracks = await soundCloud.ResolveAsync(url, cancellationToken);

        if (tracks.Count is 0)
        {
            renderer.Error("Nothing playable at that URL.");

            return ScdlExitCode.Unavailable;
        }

        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            renderer.TrackHeading(track, i + 1, tracks.Count);
            renderer.LadderTable(track, track.RankStreams(), oauthToken is { Length: > 0 });
        }

        return ScdlExitCode.Success;
    }

    private static ServiceProvider BuildProvider(string? oauthToken, bool verbose)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder
                                       .AddSimpleConsole(options => options.SingleLine = true)

                                       // The threshold lives on the provider's
                                       // ConsoleLoggerOptions, not on the
                                       // formatter's options, so it needs its own
                                       // call. Everything the logger emits goes to
                                       // stderr, leaving stdout to Spectre alone:
                                       // sharing the stream corrupts the live
                                       // progress display, and separating them
                                       // lets `scdl get ... 2>$null` print just
                                       // the result.
                                       .AddConsole(options =>
                                           options.LogToStandardErrorThreshold = LogLevel.Trace)
                                       .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning));

        services.AddScdl(options => options.OAuthToken = oauthToken);

        return services.BuildServiceProvider();
    }
}
