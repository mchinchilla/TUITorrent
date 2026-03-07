using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TUITorrent.Application.Models;
using TUITorrent.Infrastructure.Daemon;
using TUITorrent.Presentation.Rendering;

namespace TUITorrent.Presentation.Commands;

public class StatusCommand : AsyncCommand<StatusCommand.Settings>
{
    private readonly DaemonClient _daemonClient;

    public StatusCommand(DaemonClient daemonClient)
    {
        _daemonClient = daemonClient;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Torrent ID")]
        public string Id { get; set; } = string.Empty;

        [CommandOption("-f|--follow")]
        [Description("Follow progress in real-time")]
        public bool Follow { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await _daemonClient.EnsureDaemonRunningAsync(cancellationToken: cancellationToken);

        var info = await _daemonClient.GetAsync(settings.Id, cancellationToken);
        if (info is null)
        {
            AnsiConsole.MarkupLine($"[red]Torrent '{settings.Id}' not found.[/]");
            return 1;
        }

        if (!settings.Follow)
        {
            AnsiConsole.Write(
                new Panel(TorrentProgressRenderer.RenderDetailTable(info))
                    .Header($"[bold yellow]TUITorrent - {Markup.Escape(info.Name)}[/]")
                    .Border(BoxBorder.Double));
            return 0;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await AnsiConsole.Live(TorrentProgressRenderer.RenderProgressTable(info))
            .StartAsync(async ctx =>
            {
                while (!cts.IsCancellationRequested)
                {
                    var current = await _daemonClient.GetAsync(settings.Id, cts.Token);
                    if (current is null) break;

                    ctx.UpdateTarget(TorrentProgressRenderer.RenderProgressTable(current));

                    if (current.State is TorrentStatus.Seeding or TorrentStatus.Stopped or TorrentStatus.Error)
                        break;

                    await Task.Delay(500, cts.Token)
                        .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }
            });

        return 0;
    }
}
