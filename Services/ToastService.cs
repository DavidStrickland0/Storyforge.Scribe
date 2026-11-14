namespace StoryForge.Scribe.Services;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record ToastMessage(Guid Id, string Text, ToastKind Kind);

/// <summary>Small, scoped notification queue for the circuit.</summary>
public sealed class ToastService
{
    private readonly object _gate = new();
    private readonly List<ToastMessage> _messages = [];

    public event Action? Changed;

    public IReadOnlyList<ToastMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToList();
            }
        }
    }

    public void Show(string text, ToastKind kind = ToastKind.Info)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var message = new ToastMessage(Guid.NewGuid(), text, kind);
        lock (_gate)
        {
            _messages.Add(message);
        }

        Changed?.Invoke();
        _ = RemoveAfterDelayAsync(message.Id);
    }

    public void Dismiss(Guid id)
    {
        var removed = false;
        lock (_gate)
        {
            removed = _messages.RemoveAll(m => m.Id == id) > 0;
        }

        if (removed)
        {
            Changed?.Invoke();
        }
    }

    private async Task RemoveAfterDelayAsync(Guid id)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        Dismiss(id);
    }
}
