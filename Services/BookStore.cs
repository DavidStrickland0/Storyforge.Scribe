using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using StoryForge.Scribe.Domain;

namespace StoryForge.Scribe.Services;

public enum SaveState
{
    Saved,
    Pending,
    Saving,
    Error
}

public sealed record BackupInfo(string FileName, string FullPath, DateTime CreatedUtc, long SizeBytes, string Reason);

public sealed class BackupDocument
{
    public int Version { get; set; } = 1;
    public string Reason { get; set; } = "manual";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Book Book { get; set; } = new();
}

/// <summary>
/// Owns the in-memory manuscript and its file. Edits are marked dirty; the autosave loop
/// persists them. A snapshot history is kept in App_Data/backups.
/// </summary>
public sealed class BookStore
{
    private static readonly Regex BackupNamePattern = new(
        @"^book-(?<stamp>\d{8}-\d{6}-\d{3})-(?<reason>[a-z0-9\-]+)\.json$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppPaths _paths;
    private readonly StorageOptions _options;
    private readonly ILogger<BookStore> _logger;
    private readonly object _gate = new();
    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    private Book _book;
    private long _revision;
    private long _savedRevision;
    private long _backedUpRevision;
    private DateTime _lastEditUtc = DateTime.UtcNow;
    private DateTime? _lastSavedUtc;
    private DateTime? _lastBackupUtc;
    private SaveState _state = SaveState.Saved;
    private string? _lastError;

    public BookStore(AppPaths paths, StorageOptions options, ILogger<BookStore> logger)
    {
        _paths = paths;
        _options = options;
        _logger = logger;

        var load = LoadBook();
        _book = load.Book;
        LoadWarning = load.Warning;
        _lastSavedUtc = load.SavedUtc;
        _lastBackupUtc = TryGetNewestBackupTime();

        if (load.NeedsWrite)
        {
            try
            {
                WriteAtomic(paths.BookFile, AppJson.Serialize(_book));
                _lastSavedUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not write the initial manuscript file.");
                SetState(SaveState.Error, "The manuscript folder is not writable.");
            }
        }
    }

    public AppPaths Paths => _paths;
    public StorageOptions Options => _options;
    public string? LoadWarning { get; }

    public event Action? SaveStateChanged;
    public event Action? BookReplaced;
    public event Action? BackupCreated;

    /// <summary>In-memory manuscript. Structural edits should go through <see cref="Edit"/>.</summary>
    public Book Book
    {
        get
        {
            lock (_gate)
            {
                return _book;
            }
        }
    }

    public long Revision => Interlocked.Read(ref _revision);
    public long SavedRevision => Interlocked.Read(ref _savedRevision);
    public long BackedUpRevision => Interlocked.Read(ref _backedUpRevision);
    public bool IsDirty => Revision > SavedRevision;
    public DateTime LastEditUtc => _lastEditUtc;

    public SaveState State
    {
        get { lock (_stateGate) { return _state; } }
    }

    public string? LastError
    {
        get { lock (_stateGate) { return _lastError; } }
    }

    public DateTime? LastSavedUtc
    {
        get { lock (_stateGate) { return _lastSavedUtc; } }
    }

    public DateTime? LastBackupUtc
    {
        get { lock (_stateGate) { return _lastBackupUtc; } }
    }

    /// <summary>Call after mutating prose or metadata. Cheap: it only bumps a counter.</summary>
    public void MarkDirty()
    {
        Interlocked.Increment(ref _revision);
        _lastEditUtc = DateTime.UtcNow;

        var stateChanged = false;
        lock (_stateGate)
        {
            if (_state is SaveState.Saved or SaveState.Error)
            {
                _state = SaveState.Pending;
                _lastError = null;
                stateChanged = true;
            }
        }

        if (stateChanged)
        {
            SaveStateChanged?.Invoke();
        }
    }

    /// <summary>Runs a structural change under the same lock used while serializing.</summary>
    public void Edit(Action<Book> edit)
    {
        lock (_gate)
        {
            edit(_book);
        }

        MarkDirty();
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            var revision = Revision;
            if (revision <= SavedRevision)
            {
                if (State != SaveState.Saved && State != SaveState.Error)
                {
                    SetState(SaveState.Saved);
                }

                return false;
            }

            SetState(SaveState.Saving);

            var json = SerializeBook();
            await AtomicWriteAsync(_paths.BookFile, json, cancellationToken);

            Interlocked.Exchange(ref _savedRevision, revision);
            lock (_stateGate)
            {
                _lastSavedUtc = DateTime.UtcNow;
            }

            SetState(IsDirty ? SaveState.Pending : SaveState.Saved);
            return true;
        }
        catch (OperationCanceledException)
        {
            SetState(IsDirty ? SaveState.Pending : SaveState.Saved);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Autosave failed.");
            SetState(SaveState.Error, "Could not save. Check that the App_Data folder is writable.");
            return false;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    /// <summary>Saves immediately. Safe to await before navigating or shutting down.</summary>
    public async Task<bool> FlushAsync(CancellationToken cancellationToken = default) =>
        await SaveAsync(cancellationToken);

    /// <summary>Writes a point-in-time snapshot to the backup folder.</summary>
    public async Task<BackupInfo?> CreateBackupAsync(string reason = "manual", CancellationToken cancellationToken = default)
    {
        reason = SanitizeReason(reason);
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            var created = DateTime.UtcNow;
            long revisionAtSnapshot;
            string json;

            lock (_gate)
            {
                _book.UpdatedUtc = created;
                revisionAtSnapshot = Revision;
                var document = new BackupDocument
                {
                    Reason = reason,
                    CreatedUtc = created,
                    Book = _book
                };
                json = AppJson.Serialize(document);
            }

            var fileName = $"book-{created:yyyyMMdd-HHmmss-fff}-{reason}.json";
            var fullPath = Path.Combine(_paths.BackupsDirectory, fileName);
            await AtomicWriteAsync(fullPath, json, cancellationToken);

            var info = new BackupInfo(fileName, fullPath, created, new FileInfo(fullPath).Length, reason);

            lock (_stateGate)
            {
                _lastBackupUtc = created;
            }

            Interlocked.Exchange(ref _backedUpRevision, revisionAtSnapshot);
            PruneBackups();
            BackupCreated?.Invoke();
            return info;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed.");
            return null;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public IReadOnlyList<BackupInfo> ListBackups()
    {
        try
        {
            var list = new List<BackupInfo>();
            foreach (var file in new DirectoryInfo(_paths.BackupsDirectory).GetFiles("book-*.json"))
            {
                var match = BackupNamePattern.Match(file.Name);
                var created = match.Success
                    ? ParseStamp(match.Groups["stamp"].Value)
                    : file.LastWriteTimeUtc;
                var reason = match.Success ? match.Groups["reason"].Value : "backup";

                list.Add(new BackupInfo(file.Name, file.FullName, created, file.Length, reason));
            }

            return list.OrderByDescending(b => b.CreatedUtc).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not list backups.");
            return [];
        }
    }

    /// <summary>Restores a snapshot. The current state is backed up first.</summary>
    public async Task<bool> RestoreBackupAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidOperationException("That backup name is not valid.");
        }

        var fullPath = Path.Combine(_paths.BackupsDirectory, fileName);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException("That backup file no longer exists.");
        }

        BackupDocument? document;
        try
        {
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
            document = AppJson.Deserialize<BackupDocument>(json);
            if (document?.Book is null)
            {
                // Tolerate a raw book.json copied into the backup folder by hand.
                document = new BackupDocument { Book = AppJson.Deserialize<Book>(json) ?? throw new InvalidDataException() };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Backup {File} could not be read.", fileName);
            throw new InvalidOperationException("That backup file is not readable.", ex);
        }

        await CreateBackupAsync("before-restore", cancellationToken);

        Normalize(document.Book);
        lock (_gate)
        {
            _book = document.Book;
        }

        MarkDirty();
        await SaveAsync(cancellationToken);
        BookReplaced?.Invoke();
        return true;
    }

    /// <summary>Replaces the manuscript wholesale (new book / reset). A safety backup is taken first.</summary>
    public async Task ReplaceBookAsync(Book book, string reason = "replaced", CancellationToken cancellationToken = default)
    {
        await CreateBackupAsync($"before-{reason}", cancellationToken);

        Normalize(book);
        lock (_gate)
        {
            _book = book;
        }

        MarkDirty();
        await SaveAsync(cancellationToken);
        BookReplaced?.Invoke();
    }

    public string ExportMarkdown() => ManuscriptExporter.ToMarkdown(Book);

    public string ExportJson() => AppJson.Serialize(Book);

    public static void Normalize(Book book)
    {
        book.Title ??= "";
        book.Author ??= "";
        book.Subtitle ??= "";
        book.Beats ??= [];
        book.Scenes ??= [];
        book.Chapters ??= [];

        NormalizeBeats(book.Beats);
        foreach (var scene in book.Scenes)
        {
            NormalizeScene(scene);
        }

        foreach (var chapter in book.Chapters)
        {
            if (chapter.Id == Guid.Empty)
            {
                chapter.Id = Guid.NewGuid();
            }

            chapter.Title ??= "";
            chapter.Summary ??= "";
            chapter.Scenes ??= [];

            foreach (var scene in chapter.Scenes)
            {
                NormalizeScene(scene);
            }
        }
    }

    private static void NormalizeScene(Scene scene)
    {
        if (scene.Id == Guid.Empty)
        {
            scene.Id = Guid.NewGuid();
        }

        scene.Title ??= "";
        scene.Summary ??= "";
        scene.Pov ??= "";
        scene.Setting ??= "";
        scene.Beats ??= [];

        NormalizeBeats(scene.Beats);
    }

    private static void NormalizeBeats(List<Beat> beats)
    {
        foreach (var beat in beats)
        {
            if (beat.Id == Guid.Empty)
            {
                beat.Id = Guid.NewGuid();
            }

            beat.Title ??= "";
            beat.Brief ??= "";
            beat.Content ??= "";
            beat.Pov ??= "";
        }
    }

    private BookLoadResult LoadBook()
    {
        if (!File.Exists(_paths.BookFile))
        {
            var seeded = SeedBook.CreateSample();
            Normalize(seeded);
            return new BookLoadResult(seeded, null, true, null);
        }

        try
        {
            var json = File.ReadAllText(_paths.BookFile);
            var book = AppJson.Deserialize<Book>(json);
            if (book is null)
            {
                throw new InvalidDataException("The manuscript file was empty.");
            }

            Normalize(book);
            return new BookLoadResult(book, null, false, File.GetLastWriteTimeUtc(_paths.BookFile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The manuscript file could not be read. A fresh sample was loaded.");

            try
            {
                var corrupt = Path.Combine(_paths.DataDirectory, $"book.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
                File.Move(_paths.BookFile, corrupt, true);
            }
            catch (Exception moveEx)
            {
                _logger.LogError(moveEx, "Could not preserve the unreadable manuscript file.");
            }

            var seeded = SeedBook.CreateSample();
            Normalize(seeded);
            return new BookLoadResult(
                seeded,
                "Your manuscript file could not be read. A sample book was loaded and the old file was preserved.",
                true,
                null);
        }
    }

    private string SerializeBook()
    {
        lock (_gate)
        {
            _book.UpdatedUtc = DateTime.UtcNow;
            return AppJson.Serialize(_book);
        }
    }

    private void SetState(SaveState state, string? error = null)
    {
        var changed = false;
        lock (_stateGate)
        {
            if (_state != state || _lastError != error)
            {
                _state = state;
                _lastError = error;
                changed = true;
            }
        }

        if (changed)
        {
            SaveStateChanged?.Invoke();
        }
    }

    private void PruneBackups()
    {
        try
        {
            var keep = Math.Max(1, _options.MaxBackups);
            var files = new DirectoryInfo(_paths.BackupsDirectory)
                .GetFiles("book-*.json")
                .OrderByDescending(f => f.Name)
                .Skip(keep)
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not prune old backup {File}.", file.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not prune backups.");
        }
    }

    private DateTime? TryGetNewestBackupTime() => ListBackups().FirstOrDefault()?.CreatedUtc;

    private static async Task AtomicWriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content, new UTF8Encoding(false), cancellationToken);
        File.Move(temp, path, true);
    }

    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    private static DateTime ParseStamp(string stamp) =>
        DateTime.TryParseExact(
            stamp,
            "yyyyMMdd-HHmmss-fff",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : DateTime.UtcNow;

    private static string SanitizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "manual";
        }

        var sb = new StringBuilder();
        foreach (var c in reason.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) && c < 128)
            {
                sb.Append(c);
            }
            else if ((c is '-' or '_' or ' ') && sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }

        var clean = sb.ToString().Trim('-');
        return clean.Length == 0 ? "manual" : clean;
    }

    private sealed record BookLoadResult(Book Book, string? Warning, bool NeedsWrite, DateTime? SavedUtc);
}
