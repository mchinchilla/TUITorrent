namespace TUITorrent.Application.Models;

public sealed record TorrentInfo(
    string Id,
    string Name,
    string Source,
    string OutputDirectory,
    TorrentStatus State,
    double ProgressPercent,
    double DownloadRateKbps,
    double UploadRateKbps,
    int Peers,
    int Seeds,
    long TotalSizeBytes,
    DateTime AddedAt);
