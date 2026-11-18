using MonitorOff.Kit;

namespace MonitorOff;

/// <summary>
/// Sources/MonitorOff/StatusBarController.swift 의 포트.
/// 단일 클릭 → 메뉴, 더블 클릭 → 디스플레이 끄기.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    public const double DoubleClickInterval = 0.28;
    public const string MuteStorageKey = "muteOnDisplaySleep";

    private static readonly (AppLanguage Language, string Label)[] LanguageOptions =
    {
        (AppLanguage.System, "System Default"),
        (AppLanguage.Korean, "한국어"),
        (AppLanguage.English, "English"),
    };

    private readonly SleepService _sleepService;
    private readonly LoginItemService _loginService;
    private readonly LanguageService _languageService;
    private readonly ISystemAwakeService _awakeService;
    private readonly ISettingsStore _settingsStore;
    private readonly DisplayMuteController _muteController;
    private readonly DisplayPowerNotifier _powerNotifier;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _sleepItem;
    private readonly ToolStripMenuItem _loginItem;
    private readonly ToolStripMenuItem _muteItem;
    private readonly ToolStripMenuItem _languageItem;
    private readonly ToolStripMenuItem _quitItem;
    private readonly System.Windows.Forms.Timer _pendingTimer;

    private ClickDiscriminator _discriminator = new(DoubleClickInterval);
    private DateTime _lastSleepAt = DateTime.MinValue;

    public TrayApplicationContext(
        SleepService? sleepService = null,
        LoginItemService? loginService = null,
        LanguageService? languageService = null,
        ISystemAwakeService? awakeService = null,
        ISettingsStore? settingsStore = null,
        IAudioMuter? audioMuter = null)
    {
        // 같은 파일을 가리키는 저장소가 둘이면 서로의 키를 덮어쓰므로 하나를 공유한다.
        _settingsStore = settingsStore ?? new JsonFileSettingsStore();
        _sleepService = sleepService ?? new SleepService();
        _loginService = loginService ?? new LoginItemService();
        _languageService = languageService ?? new LanguageService(_settingsStore);
        _awakeService = awakeService ?? new SystemAwakeService();

        _sleepItem = new ToolStripMenuItem
        {
            ShortcutKeys = Keys.Control | Keys.D,
            ShortcutKeyDisplayString = "Ctrl+D",
        };
        _sleepItem.Click += (_, _) => PerformSleep();

        _loginItem = new ToolStripMenuItem { CheckOnClick = false };
        _loginItem.Click += (_, _) => ToggleLoginItem();

        // 값이 없으면 기본 켜짐.
        var muteEnabled = !string.Equals(_settingsStore.Get(MuteStorageKey), "false", StringComparison.OrdinalIgnoreCase);
        _muteController = new DisplayMuteController(
            audioMuter ?? new SystemAudioMuter(),
            muteEnabled,
            message => System.Diagnostics.Trace.WriteLine(message));
        _muteItem = new ToolStripMenuItem { CheckOnClick = false };
        _muteItem.Click += (_, _) => ToggleMute();

        _languageItem = BuildLanguageMenu();

        _quitItem = new ToolStripMenuItem
        {
            ShortcutKeys = Keys.Control | Keys.Q,
            ShortcutKeyDisplayString = "Ctrl+Q",
        };
        _quitItem.Click += (_, _) => Quit();

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange(new ToolStripItem[]
        {
            _sleepItem,
            new ToolStripSeparator(),
            _loginItem,
            _muteItem,
            _languageItem,
            new ToolStripSeparator(),
            _quitItem,
        });

        _pendingTimer = new System.Windows.Forms.Timer
        {
            Interval = (int)(DoubleClickInterval * 1000),
        };
        _pendingTimer.Tick += (_, _) => OnPendingTimeout();

        _notifyIcon = new NotifyIcon
        {
            Icon = MoonIcon.Create(),
            Text = "MonitorOff",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += OnMouseClick;

        LocalizeMenu();
        PreventSystemSleep();

        _powerNotifier = new DisplayPowerNotifier(isOn =>
        {
            if (isOn)
            {
                _muteController.OnDisplayWake();
            }
            else
            {
                _muteController.OnDisplaySleep();
            }
        });
    }

    /// <summary>
    /// 앱이 켜져 있는 동안 시스템이 대기모드에 빠지지 않도록 전원 요청을 건다.
    /// 화면은 꺼지지만 백그라운드 작업은 계속 돌도록 하는 것이 목적이다.
    /// </summary>
    private void PreventSystemSleep()
    {
        try
        {
            _awakeService.Acquire();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, T(L10nKey.AwakeFailedTitle), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerNotifier.Dispose();
            _muteController.RestoreOnExit();
            _pendingTimer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _awakeService.Dispose();
        }

        base.Dispose(disposing);
    }

    private string T(L10nKey key) => L10n.String(key, _languageService.EffectiveLanguage());

    private ToolStripMenuItem BuildLanguageMenu()
    {
        var menu = new ToolStripMenuItem();
        foreach (var (language, label) in LanguageOptions)
        {
            var item = new ToolStripMenuItem(label) { Tag = language };
            item.Click += (_, _) =>
            {
                _languageService.SetSelection(language);
                LocalizeMenu();
            };
            menu.DropDownItems.Add(item);
        }

        return menu;
    }

    private void LocalizeMenu()
    {
        _sleepItem.Text = T(L10nKey.SleepNow);
        _loginItem.Text = T(L10nKey.LaunchAtLogin);
        _loginItem.Checked = _loginService.IsEnabled;
        _muteItem.Text = T(L10nKey.MuteOnDisplaySleep);
        _muteItem.Checked = _muteController.Enabled;
        _languageItem.Text = "Language";
        _quitItem.Text = T(L10nKey.Quit);

        foreach (ToolStripMenuItem item in _languageItem.DropDownItems)
        {
            item.Checked = Equals(item.Tag, _languageService.Selection);
        }
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // 메뉴가 열려 있으면 클릭은 닫기 용도로만 쓴다.
        if (_menu.Visible)
        {
            return;
        }

        if (_discriminator.Click(DateTime.UtcNow) == ClickOutcome.Double)
        {
            _pendingTimer.Stop();
            PerformSleep();
            return;
        }

        _pendingTimer.Stop();
        _pendingTimer.Start();
    }

    private void OnPendingTimeout()
    {
        _pendingTimer.Stop();
        if (_discriminator.Timeout() == ClickOutcome.Single)
        {
            ShowMenu();
        }
    }

    private void ShowMenu() => _menu.Show(Cursor.Position);

    private void PerformSleep()
    {
        if (DateTime.UtcNow - _lastSleepAt < TimeSpan.FromSeconds(1.0))
        {
            return;
        }

        try
        {
            _sleepService.Sleep();
            _lastSleepAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, T(L10nKey.SleepFailedTitle), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleLoginItem()
    {
        try
        {
            _loginService.SetEnabled(!_loginService.IsEnabled);
            _loginItem.Checked = _loginService.IsEnabled;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, T(L10nKey.LoginFailedTitle), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleMute()
    {
        var newValue = !_muteController.Enabled;
        _settingsStore.Set(MuteStorageKey, newValue ? "true" : "false");
        _muteController.SetEnabled(newValue); // 끄는 시점에 우리가 건 음소거면 즉시 복원
        _muteItem.Checked = newValue;
    }

    private void Quit()
    {
        _notifyIcon.Visible = false;
        ExitThread();
    }
}
