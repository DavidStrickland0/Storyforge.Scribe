namespace StoryForge.Scribe.Services;

/// <summary>Saves the manuscript shortly after the author stops typing, and at least every 15s while they don't.</summary>
public sealed class AutosaveService : BackgroundService
{
    private readonly BookStore _store;
    private readonly StorageOptions _options;
    private readonly ILogger<AutosaveService> _logger;

    public AutosaveService(BookStore store, StorageOptions options, ILogger<AutosaveService> logger)
    {
        _store = store;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var debounce = TimeSpan.FromMilliseconds(Math.Clamp(_options.AutosaveDebounceMs, 200, 15_000));
        var maxGap = TimeSpan.FromSeconds(15);
        var lastAttempt = DateTime.UtcNow;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!_store.IsDirty || _store.State == SaveState.Saving)
                {
                    continue;
                }

                var now = DateTime.UtcNow;
                if (_store.State == SaveState.Error && now - lastAttempt < TimeSpan.FromSeconds(5))
                {
                    continue;
                }

                var quiet = now - _store.LastEditUtc >= debounce;
                var overdue = now - lastAttempt >= maxGap;
                if (!quiet && !overdue)
                {
                    continue;
                }

                lastAttempt = now;
                var saved = await _store.SaveAsync(stoppingToken);
                if (!saved && _store.State == SaveState.Error)
                {
                    _logger.LogWarning("Save attempt failed: {Error}", _store.LastError);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down; StopAsync performs the final flush.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_store.IsDirty)
            {
                await _store.SaveAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Final save on shutdown failed.");
        }

        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Writes a snapshot on a fixed schedule (15 minutes by default) whenever the manuscript has
/// changed since the previous snapshot.
/// </summary>
public sealed class BackupService : BackgroundService
{
    private readonly BookStore _store;
    private readonly StorageOptions _options;
    private readonly ILogger<BackupService> _logger;

    public BackupService(BookStore store, StorageOptions options, ILogger<BackupService> logger)
    {
        _store = store;
        _options = options;
        _logger = logger;
    }

    public TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Clamp(_options.BackupIntervalMinutes, 1, 24 * 60));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (_store.BackedUpRevision >= _store.Revision)
                {
                    continue;
                }

                if (Domain.Outline.BookWords(_store.Book) == 0)
                {
                    continue;
                }

                var backup = await _store.CreateBackupAsync("scheduled", stoppingToken);
                if (backup is not null)
                {
                    _logger.LogInformation("Scheduled backup written: {File}", backup.FileName);
                }
                else
                {
                    _logger.LogWarning("Scheduled backup failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
