using TUITorrent.Application.Models;
using TUITorrent.Domain.Enums;
using TUITorrent.Domain.Interfaces;

namespace TUITorrent.Application.Services;

public class SettingsAppService
{
    private readonly ISettingsRepository _repository;

    public SettingsAppService(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.LoadAsync(cancellationToken);
    }

    public async Task<AppSettings> UpdateSettingsAsync(
        string? outputDirectory,
        int? port,
        int? maxDownloadSpeed,
        int? maxUploadSpeed,
        int? maxConnections,
        EncryptionMode? encryption,
        bool? seedAfterDownload,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.LoadAsync(cancellationToken);

        if (outputDirectory is not null)
            settings.OutputDirectory = outputDirectory;

        if (port is not null)
            settings.ListenPort = port.Value;

        if (maxDownloadSpeed is not null)
            settings.MaxDownloadSpeedKbps = maxDownloadSpeed.Value;

        if (maxUploadSpeed is not null)
            settings.MaxUploadSpeedKbps = maxUploadSpeed.Value;

        if (maxConnections is not null)
            settings.MaxConnections = maxConnections.Value;

        if (encryption is not null)
            settings.Encryption = encryption.Value;

        if (seedAfterDownload is not null)
            settings.SeedAfterDownload = seedAfterDownload.Value;

        await _repository.SaveAsync(settings, cancellationToken);
        return settings;
    }
}
