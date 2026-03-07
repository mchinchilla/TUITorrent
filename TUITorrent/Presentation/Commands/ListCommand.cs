using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using TUITorrent.Infrastructure.Daemon;
using TUITorrent.Presentation.Rendering;

namespace TUITorrent.Presentation.Commands;

public class ListCommand : AsyncCommand<ListCommand.Settings>
{
    private readonly DaemonClient _daemonClient;

    public ListCommand(DaemonClient daemonClient)
    {
        _daemonClient = daemonClient;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-w|--watch")]
        [Description("Continuously refresh the torrent list")]
        public bool Watch { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await _daemonClient.EnsureDaemonRunningAsync(cancellationToken: cancellationToken);

        if (!settings.Watch)
        {
            var torrents = await _daemonClient.ListAsync(cancellationToken);

            if (torrents.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No active torrents.[/]");
                return 0;
            }

            AnsiConsole.Write(
                new Panel(TorrentProgressRenderer.RenderTorrentListTable(torrents))
                    .Header("[bold yellow]TUITorrent - Active Downloads[/]")
                    .Border(BoxBorder.Double));
            return 0;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var initial = await _daemonClient.ListAsync(cts.Token);
        var initialRenderable = initial.Count > 0
            ? (IRenderable)TorrentProgressRenderer.RenderTorrentListTable(initial)
            : new Markup("[dim]No active torrents.[/]");

        await AnsiConsole.Live(initialRenderable)
            .StartAsync(async ctx =>
            {
                while (!cts.IsCancellationRequested)
                {
                    var list = await _daemonClient.ListAsync(cts.Token);
                    ctx.UpdateTarget(list.Count > 0
                        ? TorrentProgressRenderer.RenderTorrentListTable(list)
                        : new Markup("[dim]No active torrents.[/]"));

                    await Task.Delay(1000, cts.Token)
                        .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }
            });

        return 0;
    }
}
