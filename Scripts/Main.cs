using Godot;

namespace Lanmian;

public partial class Main : Control
{
    private static readonly object LogSync = new();
    private AppConfig _config = null!;
    private Sb6657Client _client = null!;
    private MemeQueue _queue = null!;
    private readonly InputSender _inputSender = new();
    private readonly GlobalKeyWatcher _keyWatcher = new();

    private Meme? _currentMeme;
    private bool _sending;
    private double _lastSendTime;

    private Label _statusLabel = null!;
    private Label _queueLabel = null!;
    private TextEdit _memeText = null!;
    private Label _memeMeta = null!;
    private LineEdit _triggerKeyInput = null!;
    private LineEdit _chatKeyInput = null!;
    private SpinBox _delayInput = null!;
    private CheckButton _enabledToggle = null!;
    private ItemList _historyList = null!;

    public override void _Ready()
    {
        _config = AppConfig.Load();
        _client = new Sb6657Client(AppConfig.ApiBaseUrl);
        _queue = new MemeQueue(_client);

        BuildUi();
        ApplyConfigToUi();
        StartKeyWatcher();
        _ = PrimeAsync();
    }

    public override void _ExitTree()
    {
        _keyWatcher.Dispose();
        _client.Dispose();
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

        var subtitle = new Label { Text = "sb6657 → CS2 一键发梗", VerticalAlignment = VerticalAlignment.Center };
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
            Text = "提示：触发键仅在 CS2 位于前台时生效；本程序不注入或读取 CS2 进程。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        footer.AddThemeColorOverride("font_color", new Color("#8b95a5"));
        root.AddChild(footer);
    }

    private Control BuildMemePanel()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 10);

        var heading = new HBoxContainer();
        panel.AddChild(heading);
        var headingLabel = new Label { Text = "下一条烂梗" };
        headingLabel.AddThemeFontSizeOverride("font_size", 20);
        heading.AddChild(headingLabel);
        _queueLabel = new Label { Text = "队列：0" };
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

        var fetchButton = new Button { Text = "获取下一条" };
        fetchButton.Pressed += () => _ = LoadNextPreviewAsync();
        actions.AddChild(fetchButton);

        var sendButton = new Button { Text = "立即发送" };
        sendButton.Pressed += () => _ = SendCurrentAsync("界面按钮");
        actions.AddChild(sendButton);

        var copyButton = new Button { Text = "复制" };
        copyButton.Pressed += CopyCurrent;
        actions.AddChild(copyButton);

        var historyTitle = new Label { Text = "本次运行已发送" };
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

        panel.AddChild(new Label { Text = "一键触发键（建议 F8）" });
        _triggerKeyInput = new LineEdit { PlaceholderText = "F8 / F9 / ...", MaxLength = 8 };
        panel.AddChild(_triggerKeyInput);

        panel.AddChild(new Label { Text = "聊天键（Y=全体，U=队内）" });
        _chatKeyInput = new LineEdit { PlaceholderText = "Y", MaxLength = 8 };
        panel.AddChild(_chatKeyInput);

        panel.AddChild(new Label { Text = "按键间隔（毫秒）" });
        _delayInput = new SpinBox { MinValue = 30, MaxValue = 500, Step = 10, Value = 100 };
        panel.AddChild(_delayInput);

        _enabledToggle = new CheckButton { Text = "启用一键发送" };
        _enabledToggle.Toggled += pressed =>
        {
            _config.Enabled = pressed;
            _config.Save();
            SetStatus(pressed ? "一键发送已启用" : "一键发送已暂停");
        };
        panel.AddChild(_enabledToggle);

        var saveButton = new Button { Text = "保存设置并重载触发键" };
        saveButton.Pressed += SaveSettings;
        panel.AddChild(saveButton);

        var testButton = new Button { Text = "测试接口连接" };
        testButton.Pressed += () => _ = TestConnectionAsync();
        panel.AddChild(testButton);

        var warning = new Label
        {
            Text = "自动发送会模拟键盘输入，仅在你明确启用时工作。请先在离线或私人房间测试。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        warning.AddThemeColorOverride("font_color", new Color("#c98b37"));
        panel.AddChild(warning);

        return panel;
    }

    private void ApplyConfigToUi()
    {
        _triggerKeyInput.Text = _config.TriggerKey;
        _chatKeyInput.Text = _config.ChatKey;
        _delayInput.Value = _config.KeyDelayMs;
        _enabledToggle.SetPressedNoSignal(_config.Enabled);
    }

    private void StartKeyWatcher()
    {
        _keyWatcher.Triggered += () => CallDeferred(nameof(HandleHotkey));
        _keyWatcher.Start(_config.TriggerKey);
    }

    private void SaveSettings()
    {
        var trigger = _triggerKeyInput.Text.Trim().ToUpperInvariant();
        var chat = _chatKeyInput.Text.Trim().ToUpperInvariant();
        if (KeyMap.Parse(trigger) == 0 || KeyMap.Parse(chat) == 0)
        {
            SetStatus("按键无效，请使用 F8、F9、Y、U 等按键");
            return;
        }

        _config.TriggerKey = trigger;
        _config.ChatKey = chat;
        _config.KeyDelayMs = (int)_delayInput.Value;
        _config.Save();
        _keyWatcher.Start(_config.TriggerKey);
        SetStatus("设置已保存，触发键已重载");
    }

    private async Task PrimeAsync()
    {
        try
        {
            SetStatus("正在从 sb6657 获取烂梗…");
            await _queue.EnsureAsync(Math.Max(1, _config.QueueSize));
            await LoadNextPreviewAsync();
        }
        catch (Exception exception)
        {
            SetStatus($"接口初始化失败：{exception.Message}");
        }
    }

    private async Task LoadNextPreviewAsync()
    {
        try
        {
            _currentMeme = await _queue.TakeAsync();
            if (_currentMeme == null)
            {
                SetStatus("暂时没有获取到烂梗");
                return;
            }

            UpdatePreview();
            _ = _queue.EnsureAsync(Math.Max(1, _config.QueueSize));
            SetStatus("烂梗已准备，按触发键即可发送");
        }
        catch (Exception exception)
        {
            SetStatus($"获取烂梗失败：{exception.Message}");
        }
    }

    private void HandleHotkey()
    {
        if (!_config.Enabled || _sending) return;
        _ = SendCurrentAsync("触发键");
    }

    private async Task SendCurrentAsync(string source)
    {
        if (_sending) return;
        if (!Cs2WindowChecker.IsCs2Foreground())
        {
            SetStatus("未发送：CS2 不是当前活动窗口");
            return;
        }

        var now = Time.GetTicksMsec();
        if (now - _lastSendTime < _config.CooldownMs)
        {
            SetStatus("发送过快，请稍后再试");
            return;
        }

        _sending = true;
        try
        {
            if (_currentMeme == null) _currentMeme = await _queue.TakeAsync();
            if (_currentMeme == null) throw new InvalidOperationException("没有可发送的烂梗");

            var meme = _currentMeme;
            SetStatus($"正在发送（{source}）…");
            await _inputSender.SendAsync(meme.Text, _config.ChatKey, _config.KeyDelayMs);
            _lastSendTime = Time.GetTicksMsec();
            _historyList.AddItem($"#{meme.Id}  {meme.Text}");
            _currentMeme = null;
            UpdatePreview();
            SetStatus("发送成功，正在准备下一条");
            _ = LoadNextPreviewAsync();
        }
        catch (Exception exception)
        {
            SetStatus($"发送失败：{exception.Message}");
        }
        finally
        {
            _sending = false;
        }
    }

    private void CopyCurrent()
    {
        if (_currentMeme == null)
        {
            SetStatus("当前没有烂梗");
            return;
        }

        DisplayServer.ClipboardSet(_currentMeme.Text);
        SetStatus("已复制到剪贴板");
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
        if (_currentMeme == null)
        {
            _memeText.Text = "暂无烂梗";
            _memeMeta.Text = "点击“获取下一条”重试";
        }
        else
        {
            _memeText.Text = _currentMeme.Text;
            _memeMeta.Text = $"ID: {_currentMeme.Id}    标签: {_currentMeme.Tags}    投稿: {_currentMeme.SubmitTime}";
        }

        _queueLabel.Text = $"队列：{_queue.Count}";
    }

    private void SetStatus(string message)
    {
        if (IsInstanceValid(_statusLabel)) _statusLabel.Text = message;
        if (IsInstanceValid(_queueLabel)) _queueLabel.Text = $"队列：{_queue.Count}";
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
            // 日志失败不应影响发送。
        }
    }
}
