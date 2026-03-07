using TUITorrent.Domain.Interfaces;
using TUITorrent.Domain.ValueObjects;

namespace TUITorrent.Application.Services;

public class DownloadService
{
    private readonly ITorrentManager _manager;
    private readonly ISettingsRepository _settingsRepository;

    public DownloadService(ITorrentManager manager, ISettingsRepository settingsRepository)
    {
        _manager = manager;
        _settingsRepository = settingsRepository;
    }

    public async Task<DownloadConfiguration> BuildConfigurationAsync(
        string source,
        string? outputDir,
        int? port,
        int? dlLimit,
        int? ulLimit,
        int? maxConnections,
        bool? noSeed,
        CancellationToken cancellationToken = default)
    {
        var appSettings = await _settingsRepository.LoadAsync(cancellationToken);
        var torrentSource = TorrentSource.Parse(source);

        return new DownloadConfiguration(
            Source: torrentSource,
            OutputDirectory: outputDir ?? appSettings.OutputDirectory,
            Port: port ?? appSettings.ListenPort,
            MaxDownloadSpeedKbps: dlLimit ?? appSettings.MaxDownloadSpeedKbps,
            MaxUploadSpeedKbps: ulLimit ?? appSettings.MaxUploadSpeedKbps,
            MaxConnections: maxConnections ?? appSettings.MaxConnections,
            SeedAfterDownload: noSeed == true ? false : appSettings.SeedAfterDownload);
    }

    public async Task<string> AddTorrentAsync(
        DownloadConfiguration config,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(config.OutputDirectory))
            Directory.CreateDirectory(config.OutputDirectory);

        return await _manager.AddAsync(config, cancellationToken);
    }
}
