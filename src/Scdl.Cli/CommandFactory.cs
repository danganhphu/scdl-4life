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
            Description = "Output directory.", DefaultValueFactory = _ => new(Environment.CurrentDirectory),
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

        var noTagsOption = new Option<bool>("--no-tags")
        {
            Description = "Write no metadata at all, not even the title.",
        };

        var fullTagsOption = new Option<bool>("--full-tags")
        {
            Description = "Also write artist, album, genre, year and cover art, not just the title.",
        };

        var command = new Command("get", "Download a track or set.")
        {
            urlArgument,
            outOption,
            presetOption,
            preferOption,
            overwriteOption,
            noTagsOption,
            fullTagsOption,
        };

        command.SetAction((parseResult, cancellationToken) => RunGetAsync(
            new(
                parseResult.GetValue(urlArgument)!,
                parseResult.GetValue(outOption)!,
                parseResult.GetValue(presetOption),
                string.Equals(parseResult.GetValue(preferOption), "stream", StringComparison.Ordinal)
                    ? SourcePreference.Stream
                    : SourcePreference.Original,
                parseResult.GetValue(overwriteOption),
                ResolveTagScope(parseResult.GetValue(noTagsOption), parseResult.GetValue(fullTagsOption)),
                parseResult.GetValue(OAuthOption),
                parseResult.GetValue(VerboseOption)),
            cancellationToken));

        return command;
    }

    /// <summary>
    /// Title-only unless told otherwise. <c>--no-tags</c> wins over
    /// <c>--full-tags</c> when both are given: a file that carries less than
    /// asked is a smaller problem than one that carries more.
    /// </summary>
    private static TagScope ResolveTagScope(bool noTags, bool fullTags)
    {
        if (noTags)
        {
            return TagScope.None;
        }

        return fullTags ? TagScope.Full : TagScope.TitleOnly;
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
                                       TagScope Tags,
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

        var resolved = await soundCloud.ResolveAsync(arguments.Url, cancellationToken);
        var tracks = resolved.Tracks;

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

                // Printed here rather than logged as it happens: the progress
                // display owns the screen during the transfer, and a console
                // log line lands on top of the bar.
                foreach (var skipped in result.SkippedRungs)
                {
                    renderer.Warning(skipped);
                }

                if (result.IsPreview)
                {
                    renderer.Warning("This is a 30 second preview, not the full track. The full stream needs --oauth.");
                }

                // Only a set carries an album and a running order. A single
                // track gets neither rather than a made up one.
                SetPosition? position = resolved.SetTitle is { Length: > 0 } setTitle
                                            ? new SetPosition(setTitle, i + 1, tracks.Count)
                                            : null;

                await tagger.TryTagAsync(result.FilePath, track, position, arguments.Tags, cancellationToken);

                renderer.Saved(result);
            }
            catch (ScdlException e) when (tracks.Count > 1)
            {
                // One bad track in a set should not abandon the rest.
                renderer.Error(e.Message);
                failures++;
            }
        }

        if (failures <= 0)
        {
            return ScdlExitCode.Success;
        }

        renderer.Warning($"{failures} of {tracks.Count} track(s) failed.");

        return ScdlExitCode.Failure;
    }

    private static async Task<DownloadResult> TransferAsync(IAnsiConsole console,
                                                            ITrackDownloader downloader,
                                                            DownloadRequest request,
                                                            Track track,
                                                            CancellationToken cancellationToken)
    {
        DownloadResult? result = null;
        var readout = new TransferReadout();

        await console.Progress()
                     .AutoClear(true)
                     .Columns(
                         new TaskDescriptionColumn(),
                         new ProgressBarColumn(),
                         new TransferPercentageColumn(),
                         new TransferAmountColumn(readout),
                         new TransferRateColumn(readout))
                     .StartAsync(async context =>
                     {
                         var task = context.AddTask(Markup.Escape(track.DisplayTitle), maxValue: 1d);

                         var progress = new Progress<TransferProgress>(tick =>
                         {
                             readout.Last = tick;

                             if (tick.IsStreamTime)
                             {
                                 // Seconds on both sides, so Spectre's own
                                 // percentage is right and the columns read the
                                 // unit off the tick rather than off the task.
                                 task.IsIndeterminate = false;
                                 task.MaxValue = tick.StreamDuration.TotalSeconds;
                                 task.Value = tick.StreamTime.TotalSeconds;

                                 return;
                             }

                             if (tick.TotalBytes is > 0)
                             {
                                 task.IsIndeterminate = false;
                                 task.MaxValue = tick.TotalBytes.Value;
                             }
                             else
                             {
                                 // Segment concatenation has no total until the
                                 // last one lands. The ceiling has to stay out of
                                 // reach as well: Spectre calls a task finished
                                 // the moment Value meets MaxValue, and a
                                 // finished task freezes as a full bar.
                                 task.IsIndeterminate = true;
                                 task.MaxValue = double.PositiveInfinity;
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

        var tracks = (await soundCloud.ResolveAsync(url, cancellationToken)).Tracks;

        if (tracks.Count is 0)
        {
            renderer.Error("Nothing playable at that URL.");

            return ScdlExitCode.Unavailable;
        }

        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            renderer.TrackHeading(track, i + 1, tracks.Count);

            var probed = await ProbeLadderAsync(soundCloud, track, cancellationToken);

            renderer.LadderTable(track, probed, hasToken: oauthToken is { Length: > 0 });
        }

        return ScdlExitCode.Success;
    }

    /// <summary>
    /// Asks SoundCloud for each advertised rung in turn and records which ones
    /// it will actually serve.
    /// </summary>
    /// <remarks>
    /// One request per rung, and that cost is the point: <c>abr_sq</c> is
    /// routinely advertised and then answers 404, so a ladder printed from
    /// <c>media.transcodings</c> alone promises a bitrate <c>get</c> will not
    /// deliver. The signed URL that comes back is thrown away; only whether one
    /// came back at all is being asked.
    /// </remarks>
    private static async Task<IReadOnlyList<ProbedRung>> ProbeLadderAsync(ISoundCloudClient soundCloud,
                                                                          Track track,
                                                                          CancellationToken cancellationToken)
    {
        var ranked = track.RankStreams();
        var probed = new List<ProbedRung>(ranked.Count);

        foreach (var option in ranked)
        {
            var stream = await soundCloud.GetStreamUriAsync(track, option.Transcoding, cancellationToken);

            probed.Add(new(option, stream.TryGetValue(out _) ? null : stream.Error.Message));
        }

        return probed;
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
