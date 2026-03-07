using TUITorrent.Application.Models;
using TUITorrent.Domain.ValueObjects;

namespace TUITorrent.Domain.Interfaces;

public interface ITorrentManager
{
    Task<string> AddAsync(DownloadConfiguration config, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TorrentInfo>> ListAsync(CancellationToken cancellationToken = default);
    Task<TorrentInfo?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task StopAsync(string id, CancellationToken cancellationToken = default);
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);
}
