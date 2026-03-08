using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TUITorrent.Application.Models;
using TUITorrent.Application.Services;
using TUITorrent.Infrastructure.Daemon;
using TUITorrent.Presentation.Rendering;

namespace TUITorrent.Presentation.Commands;

public class DownloadCommand : AsyncCommand<DownloadCommand.Settings>
{
    private readonly DownloadService _downloadService;
    private readonly DaemonClient _daemonClient;

    public DownloadCommand(DownloadService downloadService, DaemonClient daemonClient)
    {
        _downloadService = downloadService;
        _daemonClient = daemonClient;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<source>")]
        [Description("Magnet URI, URL to a .torrent file, or local path to a .torrent file")]
        public string Source { get; set; } = string.Empty;

        [CommandOption("-o|--output")]
        [Description("Destination directory for downloaded files")]
        public string? OutputDirectory { get; set; }

        [CommandOption("-p|--port")]
        [Description("Listening port for incoming connections")]
        public int? Port { get; set; }

        [CommandOption("--dl-limit")]
        [Description("Max download speed in KB/s (0 = unlimited)")]
        public int? MaxDownloadSpeed { get; set; }

        [CommandOption("--ul-limit")]
        [Description("Max upload speed in KB/s (0 = unlimited)")]
        public int? MaxUploadSpeed { get; set; }

        [CommandOption("--max-connections")]
        [Description("Maximum number of connections")]
        public int? MaxConnections { get; set; }

        [CommandOption("--no-seed")]
        [Description("Do not seed after download completes")]
        public bool NoSeed { get; set; }

        [CommandOption("-f|--follow")]
        [Description("Follow download progress in real-time")]
        public bool Follow { get; set; }

        [CommandOption("--exit-when-done")]
        [Description("Shut down daemon automatically when all downloads complete")]
        public bool ExitWhenDone { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await AnsiConsole.Status()
            .StartAsync("Connecting to daemon...", async _ =>
            {
                await _daemonClient.EnsureDaemonRunningAsync(
                    exitWhenDone: settings.ExitWhenDone,
                    cancellationToken: cancellationToken);
            });

        var config = await _downloadService.BuildConfigurationAsync(
            source: settings.Source,
            outputDir: settings.OutputDirectory,
            port: settings.Port,
            dlLimit: settings.MaxDownloadSpeed,
            ulLimit: settings.MaxUploadSpeed,
            maxConnections: settings.MaxConnections,
            noSeed: settings.NoSeed ? true : null,
            cancellationToken: cancellationToken);

        AnsiConsole.Write(
            new Panel(TorrentProgressRenderer.RenderConfigTable(config))
                .Header("[bold yellow]TUITorrent - Download[/]")
                .Border(BoxBorder.Double));

        var id = await _downloadService.AddTorrentAsync(config, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Torrent added with ID:[/] [bold cyan]{id}[/]");

        if (!settings.Follow)
        {
            AnsiConsole.MarkupLine("[dim]Use [bold]tuitorrent status " + id + "[/] to check progress, or add -f to follow.[/]");
            return 0;
        }

        return await FollowProgressAsync(id, cancellationToken);
    }

    private async Task<int> FollowProgressAsync(string id, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var initial = await _daemonClient.GetAsync(id, cts.Token);
        if (initial is null)
        {
            AnsiConsole.MarkupLine("[red]Torrent not found.[/]");
            return 1;
        }

        await AnsiConsole.Live(TorrentProgressRenderer.RenderProgressTable(initial))
            .StartAsync(async ctx =>
            {
                while (!cts.IsCancellationRequested)
                {
                    var info = await _daemonClient.GetAsync(id, cts.Token);
                    if (info is null) break;

                    ctx.UpdateTarget(TorrentProgressRenderer.RenderProgressTable(info));

                    if (info.State is TorrentStatus.Seeding or TorrentStatus.Stopped or TorrentStatus.Error)
                        break;

                    await Task.Delay(500, cts.Token)
                        .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }
            });

        var final = await _daemonClient.GetAsync(id, CancellationToken.None);
        if (final?.State == TorrentStatus.Error)
        {
            AnsiConsole.MarkupLine("[red]Download failed with an error.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Download complete.[/]");
        return 0;
    }
}
