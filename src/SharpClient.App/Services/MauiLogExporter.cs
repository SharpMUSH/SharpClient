using Microsoft.Maui.ApplicationModel.DataTransfer;
using SharpClient.Core.Diagnostics;

namespace SharpClient.App.Services;

/// <summary>
/// MAUI implementation of <see cref="ILogExporter"/>: hands the on-device log file to the OS share
/// sheet so the user can email it / send it over chat from their phone.
/// </summary>
public sealed class MauiLogExporter : ILogExporter
{
    private readonly FileLogStore _store;

    public MauiLogExporter(FileLogStore store) => _store = store;

    public bool IsAvailable => true;

    public string? LogPath => _store.FilePath;

    public async Task ShareAsync()
    {
        // Guarantee the file exists so the share sheet has something to attach even on a fresh install.
        if (!File.Exists(_store.FilePath))
        {
            _store.Append("Information", "LogExporter", "No log entries recorded yet.");
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "SharpClient diagnostics log",
            File = new ShareFile(_store.FilePath),
        });
    }
}
