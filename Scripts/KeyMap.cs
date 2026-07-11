namespace Lanmian;

public static class KeyMap
{
    private static readonly Dictionary<string, (string CfgName, ushort VirtualKey)> NamedKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ENTER"] = ("ENTER", 0x0D),
            ["RETURN"] = ("ENTER", 0x0D),
            ["SPACE"] = ("SPACE", 0x20),
            ["TAB"] = ("TAB", 0x09),
            ["ESC"] = ("ESCAPE", 0x1B),
            ["ESCAPE"] = ("ESCAPE", 0x1B),
            ["BACKSPACE"] = ("BACKSPACE", 0x08),
            ["INSERT"] = ("INS", 0x2D),
            ["INS"] = ("INS", 0x2D),
            ["DELETE"] = ("DEL", 0x2E),
            ["DEL"] = ("DEL", 0x2E),
            ["HOME"] = ("HOME", 0x24),
            ["END"] = ("END", 0x23),
            ["PAGEUP"] = ("PGUP", 0x21),
            ["PGUP"] = ("PGUP", 0x21),
            ["PAGEDOWN"] = ("PGDN", 0x22),
            ["PGDN"] = ("PGDN", 0x22),
            ["LEFT"] = ("LEFTARROW", 0x25),
            ["LEFTARROW"] = ("LEFTARROW", 0x25),
            ["UP"] = ("UPARROW", 0x26),
            ["UPARROW"] = ("UPARROW", 0x26),
            ["RIGHT"] = ("RIGHTARROW", 0x27),
            ["RIGHTARROW"] = ("RIGHTARROW", 0x27),
            ["DOWN"] = ("DOWNARROW", 0x28),
            ["DOWNARROW"] = ("DOWNARROW", 0x28),
            ["SHIFT"] = ("SHIFT", 0x10),
            ["CTRL"] = ("CTRL", 0x11),
            ["CONTROL"] = ("CTRL", 0x11),
            ["ALT"] = ("ALT", 0x12),
            ["CAPSLOCK"] = ("CAPSLOCK", 0x14),
            ["SEMICOLON"] = ("SEMICOLON", 0xBA),
            ["EQUAL"] = ("EQUALS", 0xBB),
            ["EQUALS"] = ("EQUALS", 0xBB),
            ["COMMA"] = ("COMMA", 0xBC),
            ["MINUS"] = ("MINUS", 0xBD),
            ["PERIOD"] = ("PERIOD", 0xBE),
            ["SLASH"] = ("SLASH", 0xBF),
            ["BACKQUOTE"] = ("BACKQUOTE", 0xC0),
            ["QUOTELEFT"] = ("BACKQUOTE", 0xC0),
            ["BRACKETLEFT"] = ("LBRACKET", 0xDB),
            ["LBRACKET"] = ("LBRACKET", 0xDB),
            ["BACKSLASH"] = ("BACKSLASH", 0xDC),
            ["BRACKETRIGHT"] = ("RBRACKET", 0xDD),
            ["RBRACKET"] = ("RBRACKET", 0xDD),
            ["APOSTROPHE"] = ("APOSTROPHE", 0xDE),
            ["MOUSE1"] = ("MOUSE1", 0x01),
            ["MOUSE2"] = ("MOUSE2", 0x02),
            ["MOUSE3"] = ("MOUSE3", 0x04),
            ["MOUSE4"] = ("MOUSE4", 0x05),
            ["MOUSE5"] = ("MOUSE5", 0x06)
        };

    private static readonly Dictionary<char, (string CfgName, ushort VirtualKey)> PunctuationKeys = new()
    {
        [';'] = ("SEMICOLON", 0xBA),
        ['='] = ("EQUALS", 0xBB),
        [','] = ("COMMA", 0xBC),
        ['-'] = ("MINUS", 0xBD),
        ['.'] = ("PERIOD", 0xBE),
        ['/'] = ("SLASH", 0xBF),
        ['`'] = ("BACKQUOTE", 0xC0),
        ['['] = ("LBRACKET", 0xDB),
        ['\\'] = ("BACKSLASH", 0xDC),
        [']'] = ("RBRACKET", 0xDD),
        ['\''] = ("APOSTROPHE", 0xDE)
    };

    public static ushort Parse(string? key)
    {
        return TryNormalize(key, out _, out var virtualKey) ? virtualKey : (ushort)0;
    }

    public static bool TryNormalize(string? key, out string cfgName)
    {
        return TryNormalize(key, out cfgName, out _);
    }

    public static bool TryNormalize(string? key, out string cfgName, out ushort virtualKey)
    {
        cfgName = string.Empty;
        virtualKey = 0;
        var value = (key ?? string.Empty).Trim();
        if (value.Length == 0) return false;

        if (value.Length == 1)
        {
            var character = char.ToUpperInvariant(value[0]);
            if (character is >= 'A' and <= 'Z')
            {
                cfgName = character.ToString();
                virtualKey = character;
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                cfgName = character.ToString();
                virtualKey = character;
                return true;
            }

            if (PunctuationKeys.TryGetValue(value[0], out var punctuation))
            {
                (cfgName, virtualKey) = punctuation;
                return true;
            }
        }

        var token = new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (token.StartsWith('F') && int.TryParse(token[1..], out var functionKey) && functionKey is >= 1 and <= 24)
        {
            cfgName = $"F{functionKey}";
            virtualKey = (ushort)(0x70 + functionKey - 1);
            return true;
        }

        if (!NamedKeys.TryGetValue(token, out var namedKey)) return false;
        (cfgName, virtualKey) = namedKey;
        return true;
    }

    public static string ToCs2UserBindingKey(string key)
    {
        if (!TryNormalize(key, out var cfgName)) throw new InvalidOperationException("发送键无效");
        return cfgName switch
        {
            "SEMICOLON" => ";",
            "EQUALS" => "=",
            "COMMA" => ",",
            "MINUS" => "-",
            "PERIOD" => ".",
            "SLASH" => "/",
            "BACKQUOTE" => "`",
            "LBRACKET" => "[",
            "BACKSLASH" => "\\",
            "RBRACKET" => "]",
            "APOSTROPHE" => "'",
            _ when cfgName.Length == 1 && char.IsLetter(cfgName[0]) => cfgName.ToLowerInvariant(),
            _ => cfgName
        };
    }
}
