using MonitorOff.Kit;

namespace MonitorOff;

/// <summary>
/// Sources/MonitorOff/StatusBarController.swift 의 포트.
/// 단일 클릭 → 메뉴, 더블 클릭 → 디스플레이 끄기.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    public const double DoubleClickInterval = 0.28;

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

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _sleepItem;
    private readonly ToolStripMenuItem _loginItem;
    private readonly ToolStripMenuItem _languageItem;
    private readonly ToolStripMenuItem _quitItem;
    private readonly System.Windows.Forms.Timer _pendingTimer;

    private ClickDiscriminator _discriminator = new(DoubleClickInterval);
    private DateTime _lastSleepAt = DateTime.MinValue;

    public TrayApplicationContext(
        SleepService? sleepService = null,
        LoginItemService? loginService = null,
        LanguageService? languageService = null,
        ISystemAwakeService? awakeService = null)
    {
        _sleepService = sleepService ?? new SleepService();
        _loginService = loginService ?? new LoginItemService();
        _languageService = languageService ?? new LanguageService();
        _awakeService = awakeService ?? new SystemAwakeService();

        _sleepItem = new ToolStripMenuItem
        {
            ShortcutKeys = Keys.Control | Keys.D,
            ShortcutKeyDisplayString = "Ctrl+D",
        };
        _sleepItem.Click += (_, _) => PerformSleep();

        _loginItem = new ToolStripMenuItem { CheckOnClick = false };
        _loginItem.Click += (_, _) => ToggleLoginItem();

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

    private void Quit()
    {
        _notifyIcon.Visible = false;
        ExitThread();
    }
}
