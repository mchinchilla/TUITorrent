using System.Diagnostics;
using System.Net.Sockets;
using TUITorrent.Application.Models;
using TUITorrent.Domain.Interfaces;
using TUITorrent.Domain.ValueObjects;

namespace TUITorrent.Infrastructure.Daemon;

public class DaemonClient : ITorrentManager
{
    public async Task<string> AddAsync(DownloadConfiguration config, CancellationToken cancellationToken = default)
    {
        var request = new AddTorrentRequest(
            config.Source.Value,
            config.OutputDirectory,
            config.Port,
            config.MaxDownloadSpeedKbps,
            config.MaxUploadSpeedKbps,
            config.MaxConnections,
            config.SeedAfterDownload);

        var response = await SendRequestAsync(request, cancellationToken);
        return response.Id ?? throw new InvalidOperationException("Daemon did not return a torrent ID.");
    }

    public async Task<IReadOnlyList<TorrentInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync(new ListTorrentsRequest(), cancellationToken);
        return response.Torrents?.Select(t => t.ToDomain()).ToList()
               ?? (IReadOnlyList<TorrentInfo>)[];
    }

    public async Task<TorrentInfo?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await SendRequestAsync(new GetTorrentRequest(id), cancellationToken);
        return response.Torrent?.ToDomain();
    }

    public async Task StopAsync(string id, CancellationToken cancellationToken = default)
    {
        await SendRequestAsync(new StopTorrentRequest(id), cancellationToken);
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        await SendRequestAsync(new RemoveTorrentRequest(id), cancellationToken);
    }

    public async Task PurgeAsync(string id, CancellationToken cancellationToken = default)
    {
        await SendRequestAsync(new PurgeTorrentRequest(id), cancellationToken);
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await SendRequestAsync(new ShutdownRequest(), cancellationToken);
    }

    public async Task EnsureDaemonRunningAsync(
        bool exitWhenDone = false, CancellationToken cancellationToken = default)
    {
        if (await IsDaemonRunningAsync())
            return;

        var exePath = Environment.ProcessPath
                      ?? throw new InvalidOperationException("Cannot determine executable path.");

        var arguments = exitWhenDone ? "daemon start --exit-when-done" : "daemon start";

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process.Start(startInfo);

        // Wait for the socket to appear
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(200, cancellationToken);
            if (File.Exists(DaemonPaths.SocketPath))
                return;
        }

        throw new TimeoutException("Daemon did not start in time.");
    }

    private static async Task<bool> IsDaemonRunningAsync()
    {
        if (!File.Exists(DaemonPaths.SocketPath))
            return false;

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(DaemonPaths.SocketPath));
            socket.Close();
            return true;
        }
        catch
        {
            // Socket file exists but daemon is dead - clean up
            if (File.Exists(DaemonPaths.SocketPath))
                File.Delete(DaemonPaths.SocketPath);
            return false;
        }
    }

    private static async Task<DaemonResponse> SendRequestAsync(
        DaemonRequest request, CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(DaemonPaths.SocketPath), cancellationToken);

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        await using var writer = new StreamWriter(stream) { AutoFlush = true };
        using var reader = new StreamReader(stream);

        await writer.WriteLineAsync(DaemonSerializer.SerializeRequest(request));

        var line = await reader.ReadLineAsync(cancellationToken)
                   ?? throw new InvalidOperationException("Daemon closed connection without response.");

        var response = DaemonSerializer.DeserializeResponse(line)
                       ?? throw new InvalidOperationException("Invalid response from daemon.");

        if (!response.Success)
            throw new InvalidOperationException(response.Error ?? "Daemon returned an error.");

        return response;
    }
}
