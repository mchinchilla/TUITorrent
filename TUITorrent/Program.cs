using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using TUITorrent.Application.Services;
using TUITorrent.Domain.Interfaces;
using TUITorrent.Infrastructure.Daemon;
using TUITorrent.Infrastructure.Persistence;
using TUITorrent.Presentation.Commands;
using TUITorrent.Presentation.Infrastructure;

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "0.0.0";

var services = new ServiceCollection();

// Infrastructure
services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
services.AddSingleton<DaemonClient>();
services.AddSingleton<ITorrentManager>(sp => sp.GetRequiredService<DaemonClient>());

// Application services
services.AddTransient<DownloadService>();
services.AddTransient<SettingsAppService>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("tuitorrent");
    config.SetApplicationVersion(version);

    config.AddCommand<DownloadCommand>("download")
        .WithAlias("dl")
        .WithDescription("Download a torrent from a magnet link or .torrent file")
        .WithExample("download", "magnet:?xt=urn:btih:...", "-f")
        .WithExample("download", "file.torrent", "-o", "/path/to/output")
        .WithExample("dl", "magnet:?xt=urn:btih:...", "--dl-limit", "1024", "--no-seed");

    config.AddCommand<ListCommand>("list")
        .WithAlias("ls")
        .WithDescription("List all active torrents")
        .WithExample("list")
        .WithExample("ls", "--watch");

    config.AddCommand<StatusCommand>("status")
        .WithDescription("Show detailed status of a torrent")
        .WithExample("status", "a3f2b1c8")
        .WithExample("status", "a3f2b1c8", "-f");

    config.AddCommand<PriorityCommand>("priority")
        .WithAlias("prio")
        .WithDescription("Set the download priority of a torrent")
        .WithExample("priority", "a3f2b1c8", "high")
        .WithExample("prio", "a3f2b1c8", "low");

    config.AddCommand<StopTorrentCommand>("stop")
        .WithAlias("pause")
        .WithDescription("Pause a torrent download")
        .WithExample("stop", "a3f2b1c8");

    config.AddCommand<ResumeTorrentCommand>("resume")
        .WithDescription("Resume a paused torrent download")
        .WithExample("resume", "a3f2b1c8");

    config.AddCommand<RemoveTorrentCommand>("remove")
        .WithAlias("rm")
        .WithDescription("Remove a torrent from the download list (use -d to delete files)")
        .WithExample("remove", "a3f2b1c8")
        .WithExample("rm", "a3f2b1c8", "--delete-data");

    config.AddCommand<SettingsCommand>("settings")
        .WithAlias("config")
        .WithDescription("View or modify application settings")
        .WithExample("settings")
        .WithExample("settings", "--output", "/path/to/downloads");

    config.AddBranch("daemon", daemon =>
    {
        daemon.SetDescription("Manage the background daemon");

        daemon.AddCommand<DaemonStartCommand>("start")
            .WithDescription("Start the daemon in the foreground");

        daemon.AddCommand<DaemonStopCommand>("stop")
            .WithDescription("Stop the running daemon");

        daemon.AddCommand<DaemonStatusCommand>("status")
            .WithDescription("Check if the daemon is running");
    });
});

return await app.RunAsync(args);
