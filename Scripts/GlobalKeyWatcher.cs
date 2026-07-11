using System.Runtime.InteropServices;

namespace Lanmian;

public sealed class GlobalKeyWatcher : IDisposable
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private Thread? _thread;
    private volatile bool _running;
    private ushort _virtualKey;

    public event Action? Triggered;

    public void Start(string key)
    {
        Stop();
        _virtualKey = KeyMap.Parse(key);
        if (_virtualKey == 0 || !OperatingSystem.IsWindows()) return;

        _running = true;
        _thread = new Thread(Poll)
        {
            IsBackground = true,
            Name = "Lanmian.GlobalKeyWatcher"
        };
        _thread.Start();
    }

    private void Poll()
    {
        var wasDown = false;
        while (_running)
        {
            var isDown = (GetAsyncKeyState(_virtualKey) & 0x8000) != 0;
            if (isDown && !wasDown && Cs2WindowChecker.IsCs2Foreground()) Triggered?.Invoke();
            wasDown = isDown;
            Thread.Sleep(15);
        }
    }

    public void Stop()
    {
        _running = false;
        if (_thread is { IsAlive: true }) _thread.Join(200);
        _thread = null;
    }

    public void Dispose() => Stop();
}

