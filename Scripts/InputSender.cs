using System.Runtime.InteropServices;
using Godot;

namespace Lanmian;

public sealed class InputSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const ushort Control = 0x11;
    private const ushort A = 0x41;
    private const ushort V = 0x56;
    private const ushort Backspace = 0x08;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    public async Task SendAsync(string message, string chatKey, int keyDelayMs, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("烂梗喵目前只支持 Windows");
        if (!await _sendLock.WaitAsync(0, cancellationToken)) throw new InvalidOperationException("上一条消息仍在发送");

        try
        {
            if (!Cs2WindowChecker.IsCs2Foreground()) throw new InvalidOperationException("CS2 不是当前活动窗口");

            var normalized = Sanitize(message, 250);
            if (string.IsNullOrWhiteSpace(normalized)) throw new InvalidOperationException("消息为空");

            var oldClipboard = DisplayServer.ClipboardGet();
            DisplayServer.ClipboardSet(normalized);

            var chatVirtualKey = KeyMap.Parse(chatKey);
            if (chatVirtualKey == 0) throw new InvalidOperationException("聊天按键无效");

            await PressAsync(chatVirtualKey, keyDelayMs, cancellationToken);
            await HotkeyAsync(Control, A, keyDelayMs, cancellationToken);
            await PressAsync(Backspace, keyDelayMs, cancellationToken);
            await HotkeyAsync(Control, V, keyDelayMs, cancellationToken);
            await PressAsync(0x0D, keyDelayMs, cancellationToken);

            await Task.Delay(Math.Max(50, keyDelayMs), cancellationToken);
            if (DisplayServer.ClipboardGet() == normalized) DisplayServer.ClipboardSet(oldClipboard);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static string Sanitize(string value, int maxLength)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var character in value)
        {
            if (character is '\r' or '\n') builder.Append(' ');
            else if (!char.IsControl(character)) builder.Append(character);
        }

        var result = builder.ToString().Trim();
        return result.Length <= maxLength ? result : result[..maxLength];
    }

    private static async Task PressAsync(ushort virtualKey, int delayMs, CancellationToken cancellationToken)
    {
        SendKey(virtualKey, false);
        await Task.Delay(Math.Max(20, delayMs), cancellationToken);
        SendKey(virtualKey, true);
        await Task.Delay(Math.Max(20, delayMs), cancellationToken);
    }

    private static async Task HotkeyAsync(ushort modifier, ushort virtualKey, int delayMs, CancellationToken cancellationToken)
    {
        SendKey(modifier, false);
        await Task.Delay(20, cancellationToken);
        await PressAsync(virtualKey, Math.Max(20, delayMs / 2), cancellationToken);
        SendKey(modifier, true);
        await Task.Delay(Math.Max(20, delayMs), cancellationToken);
    }

    private static void SendKey(ushort virtualKey, bool release)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = release ? KeyUp : 0
            }
        };

        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
        {
            throw new InvalidOperationException("Windows 输入发送失败");
        }
    }
}

