namespace Lanmian;

public sealed class MemeQueue
{
    private readonly Sb6657Client _client;
    private readonly Queue<Meme> _items = new();
    private readonly HashSet<string> _ids = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _refillLock = new(1, 1);
    private readonly object _sync = new();

    public MemeQueue(Sb6657Client client)
    {
        _client = client;
    }

    public int Count
    {
        get
        {
            lock (_sync) return _items.Count;
        }
    }

    public Meme? TryTake()
    {
        lock (_sync)
        {
            if (_items.Count == 0) return null;
            var meme = _items.Dequeue();
            _ids.Remove(meme.Id);
            return meme;
        }
    }

    public async Task EnsureAsync(int minimum, CancellationToken cancellationToken = default)
    {
        if (Count >= minimum) return;

        await _refillLock.WaitAsync(cancellationToken);
        try
        {
            while (Count < minimum)
            {
                var meme = await _client.FetchRandomMemeAsync(cancellationToken);
                lock (_sync)
                {
                    if (_ids.Add(meme.Id)) _items.Enqueue(meme);
                }
            }
        }
        finally
        {
            _refillLock.Release();
        }
    }

    public async Task<Meme?> TakeAsync(CancellationToken cancellationToken = default)
    {
        var meme = TryTake();
        if (meme != null) return meme;

        await EnsureAsync(1, cancellationToken);
        return TryTake();
    }
}

