using Godot;

namespace Lanmian;

public partial class Main : Control
{
    private static readonly object LogSync = new();

    private AppConfig _config = null!;
    private Sb6657Client _client = null!;
    private MemeQueue _queue = null!;
    private Cs2CfgManager _cfgManager = null!;
    private readonly GlobalKeyWatcher _keyWatcher = new();

    private Meme? _stagedMeme;
    private bool _refilling;
    private bool _installCfgOnReady;
    private bool _capturingTriggerKey;
    private string _selectedTriggerKey = "F8";

    private Label _statusLabel = null!;
    private Label _queueLabel = null!;
    private Label _cfgStatusLabel = null!;
    private Label _pathLabel = null!;
    private TextEdit _memeText = null!;
    private Label _memeMeta = null!;
    private Button _triggerKeyButton = null!;
    private OptionButton _chatScopeInput = null!;
    private SpinBox _queueSizeInput = null!;
    private CheckButton _enabledToggle = null!;
    private ItemList _historyList = null!;
    private FileDialog _cs2PathDialog = null!;

    public override void _Ready()
    {
        _config = AppConfig.Load();
        _client = new Sb6657Client(AppConfig.ApiBaseUrl);
        _queue = new MemeQueue(_client);
        _cfgManager = new Cs2CfgManager(_config.Cs2Path);
        _installCfgOnReady = OS.GetCmdlineUserArgs().Any(argument =>
            argument.Equals("--install-cfg", StringComparison.OrdinalIgnoreCase));

        BuildUi();
        ApplyConfigToUi();
        DetectCs2Path();
        StartKeyWatcher();
        _ = PrimeAsync();
    }

    public override void _ExitTree()
    {
        _keyWatcher.Dispose();
        _client.Dispose();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_capturingTriggerKey) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            var keycode = keyEvent.PhysicalKeycode == Key.None
                ? keyEvent.Keycode
                : DisplayServer.KeyboardGetKeycodeFromPhysical(keyEvent.PhysicalKeycode);
            CaptureTriggerKey(OS.GetKeycodeString(keycode));
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            var mouseKey = (int)mouseEvent.ButtonIndex switch
            {
                1 => "MOUSE1",
                2 => "MOUSE2",
                3 => "MOUSE3",
                8 => "MOUSE4",
                9 => "MOUSE5",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(mouseKey))
            {
                CaptureTriggerKey(mouseKey);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void BuildUi()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        AddChild(margin);

        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 14);
        root.AddChild(header);

        var title = new Label { Text = "烂梗喵" };
        title.AddThemeFontSizeOverride("font_size", 30);
        header.AddChild(title);

        var subtitle = new Label { Text = "sb6657 → CS2 CFG 直发", VerticalAlignment = VerticalAlignment.Center };
        subtitle.AddThemeColorOverride("font_color", new Color("#8b95a5"));
        header.AddChild(subtitle);

        _statusLabel = new Label { Text = "正在初始化…", VerticalAlignment = VerticalAlignment.Center };
        _statusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
        header.AddChild(_statusLabel);

        var split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffsets = [610] };
        root.AddChild(split);
        split.AddChild(BuildMemePanel());
        split.AddChild(BuildSettingsPanel());

        var footer = new Label
        {
            Text = "无需进入游戏控制台。请先关闭 CS2，再点击“应用 CFG 绑定”；下次启动游戏时直接生效。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        footer.AddThemeColorOverride("font_color", new Color("#8b95a5"));
        root.AddChild(footer);

        _cs2PathDialog = new FileDialog
        {
            Title = "选择 CS2 安装目录",
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true
        };
        _cs2PathDialog.DirSelected += ApplyManualCs2Path;
        AddChild(_cs2PathDialog);
    }

    private Control BuildMemePanel()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 10);

        var heading = new HBoxContainer();
        panel.AddChild(heading);
        var headingLabel = new Label { Text = "下一条待发送烂梗" };
        headingLabel.AddThemeFontSizeOverride("font_size", 20);
        heading.AddChild(headingLabel);
        _queueLabel = new Label { Text = "缓存：0" };
        _queueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _queueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        heading.AddChild(_queueLabel);

        _memeText = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 170),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Editable = false,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        panel.AddChild(_memeText);

        _memeMeta = new Label { Text = "尚未获取烂梗", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _memeMeta.AddThemeColorOverride("font_color", new Color("#8b95a5"));
        panel.AddChild(_memeMeta);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        panel.AddChild(actions);

        var fetchButton = new Button { Text = "换一条" };
        fetchButton.Pressed += () => _ = LoadNextPreviewAsync();
        actions.AddChild(fetchButton);

        var installButton = new Button { Text = "应用 CFG 绑定" };
        installButton.Pressed += () => _ = InstallCfgAsync();
        actions.AddChild(installButton);

        var removeCfgButton = new Button { Text = "一键删除烂梗喵 CFG" };
        removeCfgButton.Pressed += RemoveCfg;
        actions.AddChild(removeCfgButton);

        var historyTitle = new Label { Text = "本次运行检测到的发送" };
        historyTitle.AddThemeFontSizeOverride("font_size", 16);
        panel.AddChild(historyTitle);
        _historyList = new ItemList { CustomMinimumSize = new Vector2(0, 130) };
        panel.AddChild(_historyList);

        return panel;
    }

    private Control BuildSettingsPanel()
    {
        var panel = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        panel.AddThemeConstantOverride("separation", 8);

        var title = new Label { Text = "应用设置" };
        title.AddThemeFontSizeOverride("font_size", 20);
        panel.AddChild(title);

        panel.AddChild(new Label { Text = "CS2 路径" });
        _pathLabel = new Label { Text = "正在检测…", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _pathLabel.AddThemeColorOverride("font_color", new Color("#8b95a5"));
        panel.AddChild(_pathLabel);

        _cfgStatusLabel = new Label { Text = "CFG 状态：未安装" };
        panel.AddChild(_cfgStatusLabel);

        var pathActions = new HBoxContainer();
        pathActions.AddThemeConstantOverride("separation", 8);
        panel.AddChild(pathActions);

        var detectButton = new Button { Text = "重新检测", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detectButton.Pressed += DetectCs2Path;
        pathActions.AddChild(detectButton);

        var selectPathButton = new Button { Text = "手动选择目录", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selectPathButton.Pressed += OpenCs2PathDialog;
        pathActions.AddChild(selectPathButton);

        panel.AddChild(new Label { Text = "游戏内发送键" });
        _triggerKeyButton = new Button { Text = "当前：F8（点击后按键）" };
        _triggerKeyButton.Pressed += BeginTriggerKeyCapture;
        panel.AddChild(_triggerKeyButton);

        var triggerHint = new Label
        {
            Text = "点击上方按钮，再按下键盘按键或鼠标侧键。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        triggerHint.AddThemeColorOverride("font_color", new Color("#8b95a5"));
        panel.AddChild(triggerHint);

        panel.AddChild(new Label { Text = "发送范围" });
        _chatScopeInput = new OptionButton();
        _chatScopeInput.AddItem("全体聊天", 0);
        _chatScopeInput.AddItem("队内聊天", 1);
        panel.AddChild(_chatScopeInput);

        panel.AddChild(new Label { Text = "后台缓存数量" });
        _queueSizeInput = new SpinBox { MinValue = 2, MaxValue = 32, Step = 1, Value = 8 };
        panel.AddChild(_queueSizeInput);

        _enabledToggle = new CheckButton { Text = "启用发送后自动补充下一条" };
        _enabledToggle.Toggled += pressed =>
        {
            _config.Enabled = pressed;
            _config.Save();
            SetStatus(pressed ? "后台补充已启用" : "后台补充已暂停");
        };
        panel.AddChild(_enabledToggle);

        var saveButton = new Button { Text = "保存设置" };
        saveButton.Pressed += SaveSettings;
        panel.AddChild(saveButton);

        var testButton = new Button { Text = "测试接口连接" };
        testButton.Pressed += () => _ = TestConnectionAsync();
        panel.AddChild(testButton);

        var warning = new Label
        {
            Text = "程序只在后台更新 lanmian_send.cfg；真正的发送由 CS2 自己执行 say/say_team。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        warning.AddThemeColorOverride("font_color", new Color("#c98b37"));
        panel.AddChild(warning);

        return panel;
    }

    private void ApplyConfigToUi()
    {
        _selectedTriggerKey = KeyMap.TryNormalize(_config.TriggerKey, out var normalizedKey) ? normalizedKey : "F8";
        UpdateTriggerKeyButton();
        _chatScopeInput.Select(_config.TeamChat ? 1 : 0);
        _queueSizeInput.Value = Math.Clamp(_config.QueueSize, 2, 32);
        _enabledToggle.SetPressedNoSignal(_config.Enabled);
    }

    private void BeginTriggerKeyCapture()
    {
        _capturingTriggerKey = true;
        _triggerKeyButton.Text = "请按下发送键…";
        SetStatus("正在捕获发送键；请按下键盘按键或鼠标侧键");
    }

    private void CaptureTriggerKey(string keyName)
    {
        if (!KeyMap.TryNormalize(keyName, out var normalizedKey))
        {
            SetStatus($"暂不支持按键：{keyName}，请换一个键");
            return;
        }

        _capturingTriggerKey = false;
        _selectedTriggerKey = normalizedKey;
        UpdateTriggerKeyButton();
        SaveSettings();
    }

    private void UpdateTriggerKeyButton()
    {
        _triggerKeyButton.Text = $"当前：{_selectedTriggerKey}（点击后按键）";
    }

    private void DetectCs2Path()
    {
        var detected = _cfgManager.DetectCs2Path();
        if (string.IsNullOrWhiteSpace(detected))
        {
            _pathLabel.Text = "未找到 CS2，请先启动游戏后重新检测";
            _cfgStatusLabel.Text = "CFG 状态：不可用";
            SetStatus("未检测到 CS2 安装目录");
            return;
        }

        _config.Cs2Path = detected;
        _config.Save();
        _pathLabel.Text = detected;
        UpdateCfgStatus();
    }

    private void OpenCs2PathDialog()
    {
        if (!string.IsNullOrWhiteSpace(_cfgManager.Cs2Path) && Directory.Exists(_cfgManager.Cs2Path))
        {
            _cs2PathDialog.CurrentDir = _cfgManager.Cs2Path;
        }

        _cs2PathDialog.PopupCenteredRatio(0.8f);
    }

    private void ApplyManualCs2Path(string selectedDirectory)
    {
        try
        {
            var cs2Path = _cfgManager.SetCs2Path(selectedDirectory);
            _config.Cs2Path = cs2Path;
            _config.Save();
            _pathLabel.Text = cs2Path;
            UpdateCfgStatus();
            SetStatus("CS2 路径已手动设置并保存");
        }
        catch (Exception exception)
        {
            SetStatus($"CS2 路径设置失败：{exception.Message}");
        }
    }

    private void StartKeyWatcher()
    {
        _keyWatcher.Triggered += () => CallDeferred(nameof(HandleHotkey));
        _keyWatcher.Start(_config.TriggerKey);
    }

    private void SaveSettings()
    {
        try
        {
            var trigger = Cs2CfgManager.NormalizeCfgKey(_selectedTriggerKey).ToUpperInvariant();
            _config.TriggerKey = trigger;
            _config.TeamChat = _chatScopeInput.Selected == 1;
            _config.QueueSize = (int)_queueSizeInput.Value;
            _config.Save();
            _keyWatcher.Start(_config.TriggerKey);

            if (_cfgManager.IsInstalled() && _stagedMeme != null)
            {
                ApplyCfgAndUserBinding();
                SetStatus("发送键和 CS2 用户绑定已更新；启动游戏后直接生效");
            }
            else
            {
                SetStatus("设置已保存");
            }

            UpdateCfgStatus();
        }
        catch (Exception exception)
        {
            SetStatus($"保存设置失败：{exception.Message}");
        }
    }

    private async Task PrimeAsync()
    {
        try
        {
            SetStatus("正在从 sb6657 获取烂梗…");
            await _queue.EnsureAsync(Math.Max(2, _config.QueueSize));
            await LoadNextPreviewAsync(false);
            if (_installCfgOnReady)
            {
                _installCfgOnReady = false;
                await InstallCfgAsync();
            }
            else if (_cfgManager.IsInstalled() && _stagedMeme != null)
            {
                _cfgManager.StageMeme(_stagedMeme, _config.TeamChat);
                SetStatus("CFG 已就绪，游戏内按发送键即可直发");
            }
        }
        catch (Exception exception)
        {
            SetStatus($"接口初始化失败：{exception.Message}");
        }
    }

    private async Task LoadNextPreviewAsync(bool stageToCfg = true)
    {
        try
        {
            _stagedMeme = await _queue.TakeAsync();
            if (_stagedMeme == null)
            {
                SetStatus("暂时没有获取到烂梗");
                return;
            }

            if (stageToCfg && _cfgManager.IsInstalled()) _cfgManager.StageMeme(_stagedMeme, _config.TeamChat);
            UpdatePreview();
            _ = _queue.EnsureAsync(Math.Max(2, _config.QueueSize));
            SetStatus(_cfgManager.IsInstalled() ? "下一条烂梗已写入 CFG" : "烂梗已准备，请先安装 CFG");
        }
        catch (Exception exception)
        {
            SetStatus($"获取烂梗失败：{exception.Message}");
        }
    }

    private async Task InstallCfgAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_cfgManager.DetectCs2Path())) throw new InvalidOperationException("未找到 CS2 安装目录");
            if (_stagedMeme == null) await LoadNextPreviewAsync(false);
            if (_stagedMeme == null) throw new InvalidOperationException("没有可写入的烂梗");

            ApplyCfgAndUserBinding();
            _config.Cs2Path = _cfgManager.Cs2Path;
            _config.Save();
            UpdateCfgStatus();
            SetStatus("CFG 和 CS2 用户按键绑定已完成；启动游戏后直接生效");
        }
        catch (Exception exception)
        {
            SetStatus($"CFG 安装失败：{exception.Message}");
        }
    }

    private void RemoveCfg()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_cfgManager.DetectCs2Path())) throw new InvalidOperationException("未找到 CS2 安装目录");
            var bindingManager = new Cs2UserBindingsManager(_cfgManager.Cs2Path);
            var bindingsChanged = bindingManager.Remove(
                _config.ManagedSteamAccountId,
                _config.ManagedBindingKey,
                _config.ManagedBindingOriginalCommand);
            var cfgChanged = _cfgManager.Restore();
            _config.ManagedSteamAccountId = string.Empty;
            _config.ManagedBindingKey = string.Empty;
            _config.ManagedBindingOriginalCommand = string.Empty;
            _config.Save();
            UpdateCfgStatus();
            SetStatus(bindingsChanged || cfgChanged
                ? "已清理烂梗喵 CFG 和旧按键绑定，并恢复被覆盖的原命令；其他配置未改动"
                : "未发现可删除的烂梗喵 CFG；其他 CFG 未改动");
        }
        catch (Exception exception)
        {
            SetStatus($"删除 CFG 失败：{exception.Message}");
        }
    }

    private void ApplyCfgAndUserBinding()
    {
        if (_stagedMeme == null) throw new InvalidOperationException("没有可写入的烂梗");
        if (Cs2UserBindingsManager.IsCs2Running())
        {
            throw new InvalidOperationException("请先关闭 CS2，再应用绑定，避免游戏退出时覆盖新按键");
        }

        _cfgManager.Install(_config.TriggerKey, _config.TeamChat, _stagedMeme);
        var bindingManager = new Cs2UserBindingsManager(_cfgManager.Cs2Path);
        var managed = bindingManager.Apply(
            _config.TriggerKey,
            _config.ManagedSteamAccountId,
            _config.ManagedBindingKey,
            _config.ManagedBindingOriginalCommand);
        _config.ManagedSteamAccountId = managed.AccountId;
        _config.ManagedBindingKey = managed.Key;
        _config.ManagedBindingOriginalCommand = managed.OriginalCommand;
        _config.Save();
    }

    private void HandleHotkey()
    {
        if (!_config.Enabled || _refilling || !_cfgManager.IsInstalled()) return;
        if (_stagedMeme != null) _historyList.AddItem($"#{_stagedMeme.Id}  {_stagedMeme.Text}");
        _ = RefillAfterTriggerAsync();
    }

    private async Task RefillAfterTriggerAsync()
    {
        _refilling = true;
        try
        {
            SetStatus("检测到发送键，正在补充下一条…");
            await Task.Delay(250);
            _stagedMeme = await _queue.TakeAsync();
            if (_stagedMeme == null) throw new InvalidOperationException("缓存中没有可用烂梗");
            _cfgManager.StageMeme(_stagedMeme, _config.TeamChat);
            UpdatePreview();
            _ = _queue.EnsureAsync(Math.Max(2, _config.QueueSize));
            SetStatus("下一条烂梗已写入 CFG");
        }
        catch (Exception exception)
        {
            SetStatus($"补充下一条失败：{exception.Message}");
        }
        finally
        {
            _refilling = false;
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            SetStatus("正在测试接口…");
            var meme = await _client.FetchRandomMemeAsync();
            SetStatus($"接口正常，已收到 #{meme.Id}");
        }
        catch (Exception exception)
        {
            SetStatus($"接口测试失败：{exception.Message}");
        }
    }

    private void UpdatePreview()
    {
        if (_stagedMeme == null)
        {
            _memeText.Text = "暂无烂梗";
            _memeMeta.Text = "点击“换一条”重试";
        }
        else
        {
            _memeText.Text = _stagedMeme.Text;
            _memeMeta.Text = $"ID: {_stagedMeme.Id}    标签: {_stagedMeme.Tags}    投稿: {_stagedMeme.SubmitTime}";
        }

        _queueLabel.Text = $"缓存：{_queue.Count}";
    }

    private void UpdateCfgStatus()
    {
        _cfgStatusLabel.Text = _cfgManager.IsInstalled() ? "CFG 状态：已安装" : "CFG 状态：未安装";
    }

    private void SetStatus(string message)
    {
        if (IsInstanceValid(_statusLabel)) _statusLabel.Text = message;
        if (IsInstanceValid(_queueLabel)) _queueLabel.Text = $"缓存：{_queue.Count}";
        AppendLog(message);
    }

    private static void AppendLog(string message)
    {
        try
        {
            lock (LogSync)
            {
                var path = ProjectSettings.GlobalizePath("user://lanmian.log");
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{System.Environment.NewLine}");
            }
        }
        catch
        {
            // 日志失败不应影响程序运行。
        }
    }
}
