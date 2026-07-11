using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Lanmian;

public sealed record Cs2ManagedBinding(string AccountId, string Key, string OriginalCommand);

public sealed partial class Cs2UserBindingsManager
{
    private const string LanmianCommand = "exec lanmian_send";
    private const ulong SteamId64Base = 76561197960265728UL;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly string _steamRoot;
    private readonly bool _enforceCs2Stopped;

    public Cs2UserBindingsManager(string cs2Path, bool enforceCs2Stopped = true)
    {
        _steamRoot = Path.GetFullPath(Path.Combine(cs2Path, "..", "..", ".."));
        _enforceCs2Stopped = enforceCs2Stopped;
    }

    public static bool IsCs2Running() => Process.GetProcessesByName("cs2").Length > 0;

    public Cs2ManagedBinding Apply(
        string triggerKey,
        string previousAccountId,
        string previousKey,
        string previousOriginalCommand)
    {
        EnsureCs2Stopped();
        var accountId = DetectAccountId();
        var files = GetBindingFiles(accountId);
        var newKey = KeyMap.ToCs2UserBindingKey(triggerKey);
        var sameManagedAccount = accountId.Equals(previousAccountId, StringComparison.Ordinal);
        var oldKey = sameManagedAccount ? previousKey : string.Empty;
        var oldOriginal = sameManagedAccount ? previousOriginalCommand : string.Empty;

        var primaryLines = File.ReadAllLines(files[0]).ToList();
        var existingNewCommand = FindBinding(primaryLines, newKey);
        var originalCommand = oldKey.Equals(newKey, StringComparison.OrdinalIgnoreCase)
            ? oldOriginal
            : existingNewCommand.Equals(LanmianCommand, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : existingNewCommand;

        foreach (var file in files)
        {
            BackupOnce(file);
            var lines = File.ReadAllLines(file).ToList();
            PatchBindings(lines, oldKey, oldOriginal, newKey);
            WriteAtomically(file, lines);
        }

        return new Cs2ManagedBinding(accountId, newKey, originalCommand);
    }

    public bool Remove(string managedAccountId, string managedKey, string managedOriginalCommand)
    {
        EnsureCs2Stopped();
        var accountId = !string.IsNullOrWhiteSpace(managedAccountId) && AccountExists(managedAccountId)
            ? managedAccountId
            : DetectAccountId();
        var files = GetBindingFiles(accountId);
        var changed = false;

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file).ToList();
            var fileChanged = RemoveLanmianBindings(lines);
            if (!string.IsNullOrWhiteSpace(managedKey) && !string.IsNullOrEmpty(managedOriginalCommand))
            {
                fileChanged |= SetBinding(lines, managedKey, managedOriginalCommand);
            }

            if (!fileChanged) continue;
            BackupOnce(file);
            WriteAtomically(file, lines);
            changed = true;
        }

        return changed;
    }

    private void EnsureCs2Stopped()
    {
        if (_enforceCs2Stopped && IsCs2Running())
        {
            throw new InvalidOperationException("请先关闭 CS2，再应用或删除绑定，避免游戏退出时覆盖配置");
        }
    }

    private string DetectAccountId()
    {
        var loginUsersPath = Path.Combine(_steamRoot, "config", "loginusers.vdf");
        if (File.Exists(loginUsersPath))
        {
            var content = File.ReadAllText(loginUsersPath);
            foreach (Match match in LoginUserBlockRegex().Matches(content))
            {
                if (!MostRecentRegex().IsMatch(match.Groups["body"].Value)) continue;
                if (!ulong.TryParse(match.Groups["steamId"].Value, out var steamId64) || steamId64 < SteamId64Base) continue;
                var accountId = (steamId64 - SteamId64Base).ToString();
                if (AccountExists(accountId)) return accountId;
            }
        }

        var userdataPath = Path.Combine(_steamRoot, "userdata");
        var newest = Directory.Exists(userdataPath)
            ? Directory.GetDirectories(userdataPath)
                .Select(path => new
                {
                    AccountId = Path.GetFileName(path),
                    KeysPath = Path.Combine(path, "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg")
                })
                .Where(item => File.Exists(item.KeysPath))
                .OrderByDescending(item => File.GetLastWriteTimeUtc(item.KeysPath))
                .FirstOrDefault()
            : null;

        return newest?.AccountId ?? throw new InvalidOperationException("未找到当前 Steam 用户的 CS2 按键配置");
    }

    private bool AccountExists(string accountId)
    {
        return File.Exists(Path.Combine(_steamRoot, "userdata", accountId, "730", "local", "cfg", "cs2_user_keys_0_slot0.vcfg"));
    }

    private List<string> GetBindingFiles(string accountId)
    {
        var accountRoot = Path.Combine(_steamRoot, "userdata", accountId, "730");
        var primary = Path.Combine(accountRoot, "local", "cfg", "cs2_user_keys_0_slot0.vcfg");
        if (!File.Exists(primary)) throw new InvalidOperationException("未找到 CS2 用户按键配置");

        var candidates = new[]
        {
            primary,
            primary + "_lastclouded",
            Path.Combine(accountRoot, "remote", "cs2_user_keys.vcfg")
        };
        return candidates.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string FindBinding(List<string> lines, string key)
    {
        var (start, end) = FindBindingsRange(lines);
        for (var index = start; index < end; index++)
        {
            var match = BindingLineRegex().Match(lines[index]);
            if (match.Success && match.Groups["key"].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["command"].Value;
            }
        }

        return string.Empty;
    }

    private static void PatchBindings(List<string> lines, string oldKey, string oldOriginal, string newKey)
    {
        RemoveLanmianBindings(lines);
        if (!string.IsNullOrWhiteSpace(oldKey) &&
            !oldKey.Equals(newKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(oldOriginal))
        {
            SetBinding(lines, oldKey, oldOriginal);
        }

        SetBinding(lines, newKey, LanmianCommand);
    }

    private static bool RemoveLanmianBindings(List<string> lines)
    {
        var (start, end) = FindBindingsRange(lines);
        var changed = false;
        for (var index = end - 1; index >= start; index--)
        {
            var match = BindingLineRegex().Match(lines[index]);
            if (!match.Success || !match.Groups["command"].Value.Equals(LanmianCommand, StringComparison.OrdinalIgnoreCase)) continue;
            lines.RemoveAt(index);
            changed = true;
        }

        return changed;
    }

    private static bool SetBinding(List<string> lines, string key, string command)
    {
        var (start, end) = FindBindingsRange(lines);
        var changed = false;
        for (var index = end - 1; index >= start; index--)
        {
            var match = BindingLineRegex().Match(lines[index]);
            if (!match.Success || !match.Groups["key"].Value.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            if (match.Groups["command"].Value.Equals(command, StringComparison.Ordinal)) return changed;
            lines.RemoveAt(index);
            changed = true;
            end--;
        }

        lines.Insert(end, $"\t\t\"{key}\"\t\t\"{command}\"");
        return true;
    }

    private static (int Start, int End) FindBindingsRange(List<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (!lines[index].Trim().Equals("\"bindings\"", StringComparison.OrdinalIgnoreCase)) continue;
            var open = index + 1;
            while (open < lines.Count && lines[open].Trim().Length == 0) open++;
            if (open >= lines.Count || lines[open].Trim() != "{") break;

            var depth = 1;
            for (var cursor = open + 1; cursor < lines.Count; cursor++)
            {
                var trimmed = lines[cursor].Trim();
                if (trimmed == "{") depth++;
                else if (trimmed == "}" && --depth == 0) return (open + 1, cursor);
            }
        }

        throw new InvalidOperationException("CS2 用户按键配置格式无法识别，已停止修改");
    }

    private static void BackupOnce(string path)
    {
        var backupPath = path + ".lanmian.bak";
        if (!File.Exists(backupPath)) File.Copy(path, backupPath);
    }

    private static void WriteAtomically(string path, List<string> lines)
    {
        var temporaryPath = path + ".lanmian.tmp";
        File.WriteAllLines(temporaryPath, lines, Utf8WithoutBom);
        File.Move(temporaryPath, path, true);
    }

    [GeneratedRegex("\\\"(?<steamId>\\d{17})\\\"\\s*\\{(?<body>.*?)\\}", RegexOptions.Singleline)]
    private static partial Regex LoginUserBlockRegex();

    [GeneratedRegex("\\\"MostRecent\\\"\\s*\\\"1\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex MostRecentRegex();

    [GeneratedRegex("^\\s*\\\"(?<key>[^\\\"]+)\\\"\\s+\\\"(?<command>[^\\\"]*)\\\"\\s*$")]
    private static partial Regex BindingLineRegex();
}
