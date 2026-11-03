using System.Diagnostics;
using System.Text;
using MpcSubtitle.Core;

namespace MpcSubtitle.Cli;

internal sealed record Options
{
    public string Input = "";
    public string Model = "small";
    public string Language = "auto";
    public string? Out = null;
    public string? Ffmpeg = null;
    public string? Mpc = null;
    public bool NoPlay = false;
    public bool Help = false;
}

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitError = 1;
    private const int ExitMissingFfmpeg = 2;

    private static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch
        {
            // Non-interactive consoles may reject the encoding change; proceed with the default.
        }

        Options opts;
        try
        {
            opts = ParseArgs(args);
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            PrintUsage();
            return ExitError;
        }

        if (opts.Help)
        {
            PrintUsage();
            return ExitOk;
        }

        if (opts.Input.Length == 0)
        {
            PrintUsage();
            return ExitError;
        }

        if (!File.Exists(opts.Input))
        {
            Console.Error.WriteLine($"Input not found: {opts.Input}");
            return ExitError;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            var input = Path.GetFullPath(opts.Input);
            var isWav = input.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
            var outSrt = opts.Out ?? Path.ChangeExtension(input, ".srt");

            Console.WriteLine($"Model: {opts.Model}");
            var modelPath = await ModelResolver.ResolveAsync(opts.Model, cacheDir: null, onProgress: Console.WriteLine, cancellationToken: cts.Token);

            Console.WriteLine("Loading model ...");
            using var session = new TranscriptionSession(modelPath);

            if (isWav)
            {
                Console.WriteLine($"Input is an audio WAV; skipping ffmpeg extraction: {input}");

                var samples = WavReader.Read16kPcmMono(input);
                if (samples.Length == 0)
                {
                    Console.Error.WriteLine("No audio samples found.");
                    return ExitError;
                }

                Console.WriteLine($"Audio: {samples.Length / (double)WavReader.WhisperSampleRate:F1} s of 16 kHz mono PCM");
                Console.WriteLine($"Transcribing (language: {(opts.Language.Length == 0 ? "auto" : opts.Language)}) ...");

                var sw = Stopwatch.StartNew();
                var segments = await session.TranscribeAsync(samples, opts.Language, LogSegment, cts.Token);
                sw.Stop();

                SubRipWriter.Write(outSrt, segments);
                Console.WriteLine();
                Console.WriteLine($"Wrote {segments.Count} cue(s) to {outSrt} in {sw.Elapsed.TotalSeconds:F1}s");
            }
            else
            {
                var ffmpeg = FfmpegLocator.Locate(opts.Ffmpeg);
                if (ffmpeg is null)
                {
                    Console.Error.WriteLine("ffmpeg was not found on PATH. Install it and retry, or pass --ffmpeg <path>.");
                    Console.Error.WriteLine("Download: https://www.gyan.dev/ffmpeg/builds/ (ffmpeg-release-full zip; add ffmpeg.exe to PATH).");
                    return ExitMissingFfmpeg;
                }

                Console.WriteLine("Extracting audio + transcribing ...");
                var sw = Stopwatch.StartNew();
                var cueCount = await SubtitleGenerator.GenerateForFileAsync(input, outSrt, ffmpeg, session, opts.Language, LogSegment, cts.Token);
                sw.Stop();

                Console.WriteLine();
                Console.WriteLine($"Wrote {cueCount} cue(s) to {outSrt} in {sw.Elapsed.TotalSeconds:F1}s");

                if (!opts.NoPlay)
                {
                    var mpc = MpcLauncher.LocateMpcHc(opts.Mpc);
                    if (mpc is null)
                    {
                        Console.WriteLine("MPC-HC executable not found. Load the subtitle manually in MPC-HC (File > Load Subtitle, Ctrl+L), or pass --mpc <path>.");
                    }
                    else if (MpcLauncher.Launch(mpc, input, outSrt))
                    {
                        Console.WriteLine($"Launched MPC-HC: {mpc}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to launch MPC-HC ({mpc}). Subtitle saved at {outSrt}.");
                    }
                }
            }

            return ExitOk;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            return ExitError;
        }
    }

    private static void LogSegment(SubtitleSegment segment)
    {
        Console.WriteLine($"{SubRipWriter.FormatTimestamp(segment.Start)} -> {SubRipWriter.FormatTimestamp(segment.End)}: {segment.Text}");
    }

    private static Options ParseArgs(string[] args)
    {
        var opts = new Options();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            string RequireValue()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}.");
                }

                return args[++i];
            }

            switch (arg)
            {
                case "-h" or "--help":
                    opts.Help = true;
                    break;
                case "--model":
                    opts.Model = RequireValue();
                    break;
                case "-l" or "--language":
                    opts.Language = RequireValue();
                    break;
                case "-o" or "--out":
                    opts.Out = RequireValue();
                    break;
                case "--ffmpeg":
                    opts.Ffmpeg = RequireValue();
                    break;
                case "--mpc":
                    opts.Mpc = RequireValue();
                    break;
                case "--no-play":
                    opts.NoPlay = true;
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        throw new ArgumentException($"Unknown option: {arg}");
                    }

                    if (opts.Input.Length != 0)
                    {
                        throw new ArgumentException($"Unexpected extra argument: {arg}");
                    }

                    opts.Input = arg;
                    break;
            }
        }

        return opts;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            MpcSubtitle — generate subtitles on the fly for Media Player Classic (MPC-HC)

            Usage:
              mpc-subtitle <input> [options]

              <input>            Video or audio file to caption. A .wav input is transcribed directly;
                                 any other file has its audio extracted via ffmpeg.

            Options:
              --model <name|path>   Whisper model (default: small). Names: tiny, tiny.en, base, base.en,
                                    small, small.en, medium, medium.en, large (large-v3), large-v1, large-v2,
                                    large-v3-turbo; or a path to a local .bin model. Downloaded and cached on
                                    first use.
              --language <code>     Language as an ISO code, or 'auto' for detection (default: auto).
              --out <file.srt>      Output subtitle path (default: <input>.srt).
              --ffmpeg <path>       Path to ffmpeg.exe (default: look up on PATH).
              --mpc <path>          Path to the MPC-HC executable (default: auto-detect common install dirs).
              --no-play             Write the subtitle file only; do not launch MPC-HC.
              --help                Show this text.

            Example:
              mpc-subtitle "C:\video\episode.mkv" --language id --model small

            Requirements:
              - ffmpeg on PATH (for non-.wav inputs)
              - Internet on first run per model (downloads a Whisper GGML model)
            """);
    }
}
