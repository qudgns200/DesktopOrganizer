namespace DesktopOrganizer.Models;

public enum DesktopChangeType
{
    Created,
    Deleted,
    Renamed
}

public class DesktopChangeEventArgs : EventArgs
{
    public DesktopChangeType ChangeType { get; init; }
    public string FullPath { get; init; } = string.Empty;

    /// <summary>Populated only for <see cref="DesktopChangeType.Renamed"/> events.</summary>
    public string? OldFullPath { get; init; }
}
