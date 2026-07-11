namespace Lanmian;

public static class KeyMap
{
    public static ushort Parse(string? key)
    {
        var value = (key ?? string.Empty).Trim().ToUpperInvariant();
        if (value is "ENTER" or "RETURN") return 0x0D;
        if (value == "SPACE") return 0x20;
        if (value == "TAB") return 0x09;
        if (value is "ESC" or "ESCAPE") return 0x1B;
        if (value.Length == 1 && value[0] is >= 'A' and <= 'Z') return value[0];
        if (value.Length == 1 && value[0] is >= '0' and <= '9') return value[0];
        if (value.StartsWith('F') && int.TryParse(value[1..], out var functionKey) && functionKey is >= 1 and <= 24)
        {
            return (ushort)(0x70 + functionKey - 1);
        }

        return 0;
    }
}

