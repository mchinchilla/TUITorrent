namespace TUITorrent.Application.Models;

public enum TorrentStatus
{
    Starting,
    Downloading,
    Seeding,
    Hashing,
    Stopped,
    Error
}
