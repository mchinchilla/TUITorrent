using System.Text.Json;
using System.Text.Json.Serialization;
using TUITorrent.Application.Models;

namespace TUITorrent.Infrastructure.Daemon;

public static class DaemonPaths
{
    public static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "tuitorrent");

    public static readonly string SocketPath = Path.Combine(ConfigDir, "daemon.sock");
    public static readonly string PidFile = Path.Combine(ConfigDir, "daemon.pid");
}

// --- Requests ---

[JsonDerivedType(typeof(AddTorrentRequest), "add")]
[JsonDerivedType(typeof(ListTorrentsRequest), "list")]
[JsonDerivedType(typeof(GetTorrentRequest), "get")]
[JsonDerivedType(typeof(StopTorrentRequest), "stop")]
[JsonDerivedType(typeof(RemoveTorrentRequest), "remove")]
[JsonDerivedType(typeof(PurgeTorrentRequest), "purge")]
[JsonDerivedType(typeof(ShutdownRequest), "shutdown")]
public abstract record DaemonRequest;

public sealed record AddTorrentRequest(
    string Source,
    string OutputDirectory,
    int Port,
    int MaxDownloadSpeedKbps,
    int MaxUploadSpeedKbps,
    int MaxConnections,
    bool SeedAfterDownload) : DaemonRequest;

public sealed record ListTorrentsRequest : DaemonRequest;
public sealed record GetTorrentRequest(string Id) : DaemonRequest;
public sealed record StopTorrentRequest(string Id) : DaemonRequest;
public sealed record RemoveTorrentRequest(string Id) : DaemonRequest;
public sealed record PurgeTorrentRequest(string Id) : DaemonRequest;
public sealed record ShutdownRequest : DaemonRequest;

// --- Responses ---

public sealed record DaemonResponse(
    bool Success,
    string? Error = null,
    string? Id = null,
    List<TorrentInfoDto>? Torrents = null,
    TorrentInfoDto? Torrent = null);

public sealed record TorrentInfoDto(
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
    DateTime AddedAt)
{
    public TorrentInfo ToDomain() => new(
        Id, Name, Source, OutputDirectory, State, ProgressPercent,
        DownloadRateKbps, UploadRateKbps, Peers, Seeds, TotalSizeBytes, AddedAt);

    public static TorrentInfoDto FromDomain(TorrentInfo info) => new(
        info.Id, info.Name, info.Source, info.OutputDirectory, info.State,
        info.ProgressPercent, info.DownloadRateKbps, info.UploadRateKbps,
        info.Peers, info.Seeds, info.TotalSizeBytes, info.AddedAt);
}

public static class DaemonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string SerializeRequest(DaemonRequest request) =>
        JsonSerializer.Serialize(request, Options);

    public static DaemonRequest? DeserializeRequest(string json) =>
        JsonSerializer.Deserialize<DaemonRequest>(json, Options);

    public static string SerializeResponse(DaemonResponse response) =>
        JsonSerializer.Serialize(response, Options);

    public static DaemonResponse? DeserializeResponse(string json) =>
        JsonSerializer.Deserialize<DaemonResponse>(json, Options);
}
