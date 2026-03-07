namespace TUITorrent.Domain.ValueObjects;

public sealed record DownloadConfiguration(
    TorrentSource Source,
    string OutputDirectory,
    int Port,
    int MaxDownloadSpeedKbps,
    int MaxUploadSpeedKbps,
    int MaxConnections,
    bool SeedAfterDownload);
