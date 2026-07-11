using System.Text.Json;
using Godot;

namespace Lanmian;

public sealed class AppConfig
{
    public const string ApiBaseUrl = "https://hguofichp.cn:10086";
    public string TriggerKey { get; set; } = "F8";
    public bool TeamChat { get; set; }
    public int QueueSize { get; set; } = 8;
    public bool Enabled { get; set; } = true;
    public string Cs2Path { get; set; } = string.Empty;
    public string ManagedBindingKey { get; set; } = string.Empty;
    public string ManagedBindingOriginalCommand { get; set; } = string.Empty;
    public string ManagedSteamAccountId { get; set; } = string.Empty;

    private static string ConfigPath => ProjectSettings.GlobalizePath("user://config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new AppConfig();
            }
        }
        catch
        {
            // 损坏的配置不应阻止程序启动，直接回退默认值。
        }

        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // 配置保存失败会由界面状态提示，不能让发送流程崩溃。
        }
    }
}
