using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DesktopClock;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private AppSettings _settings = new();
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _hotkeyRegistered;
    private IntPtr _windowHandle;
    private bool _isShuttingDown;

    private const int HOTKEY_ID = 9000;
    private const int WM_HOTKEY = 0x0312;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    private Ellipse[,]? _binaryDots;
    private bool _binaryBuilt;
    private TextBlock? _binaryColon1, _binaryColon2;
    private int[] _flipOldDigits = { -1, -1, -1, -1, -1, -1 };


    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        ApplySettings();
        LoadPosition();

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        this.MouseLeftButtonDown += (s, e) =>
        {
            if (!_settings.LockPosition) this.DragMove();
        };

        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon();
        try
        {
            _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Windows.Application.ResourceAssembly.Location);
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        _trayIcon.Text = "桌面时钟";
        _trayIcon.Visible = true;

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示/隐藏", null, (_, _) => ToggleVisibility());
        menu.Items.Add("设置", null, (_, _) => OpenSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            _isShuttingDown = true;
            System.Windows.Application.Current.Shutdown();
        });

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ToggleVisibility();
    }

    private void ToggleVisibility()
    {
        if (this.Visibility == Visibility.Visible)
        {
            this.Visibility = Visibility.Hidden;
        }
        else
        {
            this.Visibility = Visibility.Visible;
            this.Show();
            this.Activate();

        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(_windowHandle);
        source?.AddHook(WndProc);
        RegisterGlobalHotkey();
        if (_settings.ClickThrough)
            SetClickThrough(true);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void RegisterGlobalHotkey()
    {
        if (_windowHandle == IntPtr.Zero) return;

        if (_hotkeyRegistered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _hotkeyRegistered = false;
        }

        try
        {
            var parts = _settings.HotkeyHide.Split('+');
            uint mod = 0;
            uint vk = 0;
            foreach (var part in parts)
            {
                switch (part.Trim().ToLower())
                {
                    case "ctrl": mod |= 0x0002; break;
                    case "alt": mod |= 0x0001; break;
                    case "shift": mod |= 0x0004; break;
                    case "win": mod |= 0x0008; break;
                    default:
                        var key = (Key)Enum.Parse(typeof(Key), part.Trim(), true);
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                        break;
                }
            }

            if (mod != 0 && vk != 0)
            {
                RegisterHotKey(_windowHandle, HOTKEY_ID, mod, vk);
                _hotkeyRegistered = true;
            }
        }
        catch
        {
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        switch (_settings.DisplayMode)
        {
            case "minimal":
                UpdateMinimal();
                break;
            case "word":
                UpdateWord();
                break;
            case "binary":
                UpdateBinary();
                break;
            case "progress":
                UpdateProgress();
                break;
            case "flip":
                UpdateFlip();
                break;
            default:
                UpdateDigital();
                break;
        }

        UpdateWorldClock();
        UpdateChime();
    }

    private string GetTimeFormat()
    {
        var format = _settings.Use24Hour ? "HH" : "hh";
        format += ":mm";
        if (_settings.ShowSeconds) format += ":ss";
        return format;
    }

    private void UpdateDigital()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString(GetTimeFormat());

        if (_settings.ShowDate)
        {
            var culture = _settings.Language == "en"
                ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
                : System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            var dayFormat = _settings.Language == "en" ? "dddd" : "dddd";
            DateText.Text = now.ToString("yyyy-MM-dd ", culture) + now.ToString(dayFormat, culture);
        }
    }

    private void UpdateMinimal()
    {
        MinimalTimeText.Text = DateTime.Now.ToString(GetTimeFormat());
    }

    private void UpdateWord()
    {
        var now = DateTime.Now;
        int h = _settings.Use24Hour ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        int m = now.Minute;
        int s = now.Second;

        WordTimeText.Text = TimeToChinese(h, m, s);

        if (_settings.ShowDate)
        {
            var culture = _settings.Language == "en"
                ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
                : System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            var dayFormat = _settings.Language == "en" ? "dddd" : "dddd";
            DateText.Text = now.ToString("yyyy-MM-dd ", culture) + now.ToString(dayFormat, culture);
        }
    }

    private void UpdateBinary()
    {
        BuildBinaryPanel();
        if (_binaryDots == null) return;

        var now = DateTime.Now;
        int h = _settings.Use24Hour ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        int m = now.Minute;
        int s = now.Second;

        int[] digits = { h / 10, h % 10, m / 10, m % 10, s / 10, s % 10 };

        Brush litBrush;
        try { litBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.FontColor)); }
        catch { litBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xd4, 0xff)); }
        var unlitBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        for (int d = 0; d < 6; d++)
        {
            int val = digits[d];
            for (int b = 0; b < 4; b++)
            {
                bool lit = (val & (1 << b)) != 0;
                _binaryDots[d, 3 - b].Fill = lit ? litBrush : unlitBrush;
            }
        }

        if (_binaryColon1 != null) _binaryColon1.Foreground = litBrush;
        if (_binaryColon2 != null) _binaryColon2.Foreground = litBrush;

        if (_settings.ShowDate)
        {
            var culture = _settings.Language == "en"
                ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
                : System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            var dayFormat = _settings.Language == "en" ? "dddd" : "dddd";
            DateText.Text = now.ToString("yyyy-MM-dd ", culture) + now.ToString(dayFormat, culture);
        }
    }

    private void UpdateProgress()
    {
        var now = DateTime.Now;
        double h = now.Hour;
        double m = now.Minute;
        double s = now.Second;

        double circ = Math.PI * 56;
        double hArc = (h / 24.0) * circ;
        double mArc = (m / 60.0) * circ;
        double sArc = (s / 60.0) * circ;

        HourArc.StrokeDashArray = new DoubleCollection { hArc, circ };
        MinuteArc.StrokeDashArray = new DoubleCollection { mArc, circ };
        SecondArc.StrokeDashArray = new DoubleCollection { sArc, circ };

        if (_settings.ShowDate)
        {
            var culture = _settings.Language == "en"
                ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
                : System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            var dayFormat = _settings.Language == "en" ? "dddd" : "dddd";
            DateText.Text = now.ToString("yyyy-MM-dd ", culture) + now.ToString(dayFormat, culture);
        }
    }

    private void UpdateFlip()
    {
        var now = DateTime.Now;
        int h = _settings.Use24Hour ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
        int m = now.Minute;
        int s = now.Second;

        int[] digits = { h / 10, h % 10, m / 10, m % 10, s / 10, s % 10 };
        TextBlock[] flipTexts = { FlipH1, FlipH2, FlipM1, FlipM2, FlipS1, FlipS2 };
        Border[] flipBorders = { FlipBorderH1, FlipBorderH2, FlipBorderM1, FlipBorderM2, FlipBorderS1, FlipBorderS2 };

        for (int i = 0; i < 6; i++)
        {
            if (digits[i] != _flipOldDigits[i])
            {
                flipTexts[i].Text = digits[i].ToString();
                AnimateFlip(flipBorders[i]);
                _flipOldDigits[i] = digits[i];
            }
        }

        FlipColon1.Text = ":";
        FlipColon2.Text = ":";

        if (_settings.ShowDate)
        {
            var culture = _settings.Language == "en"
                ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
                : System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            var dayFormat = _settings.Language == "en" ? "dddd" : "dddd";
            DateText.Text = now.ToString("yyyy-MM-dd ", culture) + now.ToString(dayFormat, culture);
        }
    }

    private void AnimateFlip(Border border)
    {
        var scale = new ScaleTransform(1, 1);
        border.RenderTransform = scale;
        border.RenderTransformOrigin = new Point(0.5, 0.5);

        var shrink = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(80));
        shrink.Completed += (s, e) =>
        {
            var grow = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        };
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
    }

    private string NumberToChinese(int n)
    {
        string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        if (n < 10) return digits[n];
        if (n < 20)
        {
            if (n == 10) return "十";
            return "十" + digits[n % 10];
        }
        int tens = n / 10;
        int ones = n % 10;
        if (ones == 0) return digits[tens] + "十";
        return digits[tens] + "十" + digits[ones];
    }

    private string TimeToChinese(int h, int m, int s)
    {
        string hStr = h == 0 ? "零时" : NumberToChinese(h) + "点";

        if (m == 0 && s == 0)
            return hStr + "整";

        string result = hStr + NumberToChinese(m) + "分";

        if (_settings.ShowSeconds)
            result += NumberToChinese(s) + "秒";

        return result;
    }

    private void BuildBinaryPanel()
    {
        if (_binaryBuilt) return;
        _binaryBuilt = true;

        var sv = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _binaryDots = new Ellipse[6, 4];
        Brush unlit = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        for (int d = 0; d < 6; d++)
        {
            if (d == 2 || d == 4)
            {
                var colon = new TextBlock
                {
                    Text = ":",
                    FontSize = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 0, 3, 0),
                    Foreground = unlit
                };
                if (d == 2) _binaryColon1 = colon;
                else _binaryColon2 = colon;
                sv.Children.Add(colon);
            }

            var col = new Grid { Width = 22, Margin = new Thickness(2) };
            for (int r = 0; r < 4; r++)
                col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int r = 0; r < 4; r++)
            {
                var dot = new Ellipse
                {
                    Width = 14,
                    Height = 14,
                    Fill = unlit,
                    Margin = new Thickness(2),
                    StrokeThickness = 0
                };
                Grid.SetRow(dot, r);
                col.Children.Add(dot);
                _binaryDots[d, r] = dot;
            }
            sv.Children.Add(col);
        }

        BinaryPanel.Children.Add(sv);
    }

    private void UpdateWorldClock()
    {
        var now = DateTime.Now;
        if (_settings.WorldClockEnabled)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(_settings.WorldClockTimeZone);
                var worldTime = TimeZoneInfo.ConvertTime(now, tz);
                var wf = _settings.Use24Hour ? "HH:mm" : "hh:mm";
                if (_settings.ShowSeconds) wf += ":ss";
                WorldClockText.Text = worldTime.ToString(wf);
                WorldClockText.Visibility = Visibility.Visible;
            }
            catch
            {
                WorldClockText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            WorldClockText.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateChime()
    {
        var now = DateTime.Now;
        if (_settings.ChimeEnabled && now.Minute == 0 && now.Second == 0)
        {
            SystemSounds.Beep.Play();
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (!_settings.SnapToEdge) return;

        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        if (this.Left < 20) this.Left = 0;
        if (this.Top < 20) this.Top = 0;
        if (this.Left + this.Width > screenWidth - 20) this.Left = screenWidth - this.Width;
        if (this.Top + this.Height > screenHeight - 20) this.Top = screenHeight - this.Height;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        var colorItem = new MenuItem { Header = "选择颜色" };
        colorItem.Click += (_, _) => PickColor();
        menu.Items.Add(colorItem);

        var settingsItem = new MenuItem { Header = "设置" };
        settingsItem.Click += (_, _) =>
        {
            this.ContextMenu = null;
            Dispatcher.BeginInvoke(new Action(OpenSettings));
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) =>
        {
            _isShuttingDown = true;
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        this.ContextMenu = menu;
        base.OnMouseRightButtonDown(e);
    }

    private void OpenSettings()
    {
        var win = new SettingsWindow(_settings)
        {
            Owner = this
        };
        if (win.ShowDialog() == true)
        {
            _settings = win.Settings;
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        Brush fg;
        try { fg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.FontColor)); }
        catch { fg = new SolidColorBrush(Color.FromRgb(0x00, 0xd4, 0xff)); }

        try { ClockText.FontFamily = new FontFamily(_settings.FontFamily); } catch { }
        ClockText.FontSize = _settings.FontSize;
        ClockText.Foreground = fg;

        try { DateText.FontFamily = new FontFamily(_settings.DateFontFamily); } catch { }
        DateText.FontSize = _settings.DateFontSize;
        try { DateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.DateColor)); } catch { }

        if (_settings.DatePosition == "bottom")
        {
            Grid.SetRow(WorldClockText, 0);
            Grid.SetRow(DateText, 2);
        }
        else
        {
            Grid.SetRow(DateText, 0);
            Grid.SetRow(WorldClockText, 2);
        }

        SwitchMode(_settings.DisplayMode);

        ApplyBackground();

        try { MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.BorderColor)); } catch { }
        MainBorder.BorderThickness = new Thickness(_settings.BorderThickness);

        ApplyThemePreset();

        switch (_settings.DisplayMode)
        {
            case "minimal":
                this.Width = Math.Max(200, _settings.FontSize * 5 + 40);
                this.Height = Math.Max(60, _settings.FontSize * 1.2 + 40);
                MinimalTimeText.FontSize = Math.Min(_settings.FontSize, 72);
                MinimalTimeText.Foreground = fg;
                break;
            case "word":
                this.Width = 420;
                this.Height = 160;
                WordTimeText.Foreground = fg;
                break;
            case "binary":
                this.Width = 340;
                this.Height = 140;
                break;
            case "progress":
                this.Width = 280;
                this.Height = 150;
                HourArc.Stroke = fg;
                MinuteArc.Stroke = fg;
                SecondArc.Stroke = fg;
                ProgressHLabel.Foreground = fg;
                ProgressMLabel.Foreground = fg;
                ProgressSLabel.Foreground = fg;
                break;
            case "flip":
                this.Width = 380;
                this.Height = 140;
                foreach (var tb in new TextBlock[] { FlipH1, FlipH2, FlipM1, FlipM2, FlipS1, FlipS2, FlipColon1, FlipColon2 })
                    tb.Foreground = fg;
                break;
            default:
                var h = _settings.FontSize * 1.3 + 40;
                if (_settings.ShowDate) h += _settings.DateFontSize + 10;
                if (_settings.WorldClockEnabled) h += 30;
                this.Width = _settings.FontSize * 7 + 40;
                this.Height = h;
                break;
        }

        if (_windowHandle != IntPtr.Zero)
            SetClickThrough(_settings.ClickThrough);

        SetAutoStart(_settings.AutoStart);

        try
        {
            var cultureName = _settings.Language == "en" ? "en-US" : "zh-CN";
            var culture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        }
        catch { }

        _settings.Save();
    }

    private void SwitchMode(string mode)
    {
        DigitalPanel.Visibility = Visibility.Collapsed;
        MinimalPanel.Visibility = Visibility.Collapsed;
        WordPanel.Visibility = Visibility.Collapsed;
        BinaryPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;
        FlipPanel.Visibility = Visibility.Collapsed;

        switch (mode)
        {
            case "minimal":
                MinimalPanel.Visibility = Visibility.Visible;
                DateText.Visibility = Visibility.Collapsed;
                break;
            case "word":
                WordPanel.Visibility = Visibility.Visible;
                DateText.Visibility = _settings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "binary":
                BinaryPanel.Visibility = Visibility.Visible;
                BuildBinaryPanel();
                DateText.Visibility = _settings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "progress":
                ProgressPanel.Visibility = Visibility.Visible;
                DateText.Visibility = _settings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "flip":
                FlipPanel.Visibility = Visibility.Visible;
                DateText.Visibility = _settings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
                break;
            default:
                DigitalPanel.Visibility = Visibility.Visible;
                DateText.Visibility = _settings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
                break;
        }
    }

    private void ApplyBackground()
    {
        if (_settings.BackgroundType == "gradient")
        {
            try
            {
                var startColor = (Color)ColorConverter.ConvertFromString(_settings.GradientStartColor);
                var endColor = (Color)ColorConverter.ConvertFromString(_settings.GradientEndColor);
                var gradient = new LinearGradientBrush(startColor, endColor, _settings.GradientAngle);
                gradient.Opacity = _settings.BackgroundOpacity;
                MainBorder.Background = gradient;
            }
            catch
            {
                MainBorder.Background = new SolidColorBrush(Color.FromArgb(
                    (byte)(_settings.BackgroundOpacity * 255), 0, 0, 0));
            }
        }
        else
        {
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(
                (byte)(_settings.BackgroundOpacity * 255), 0, 0, 0));
        }
    }

    private void ApplyThemePreset()
    {
        var currentTheme = _settings.ThemePreset;
        string fontColor;
        string borderColor;

        switch (currentTheme)
        {
            case "dark":
                fontColor = "#00d4ff";
                borderColor = "#00d4ff";
                break;
            case "light":
                fontColor = "#333333";
                borderColor = "#007aff";
                break;
            case "green":
                fontColor = "#00ff00";
                borderColor = "#00ff00";
                break;
            case "blue":
                fontColor = "#4488ff";
                borderColor = "#4488ff";
                break;
            default:
                return;
        }

        if (currentTheme != "default")
        {
            _settings.FontColor = fontColor;
            _settings.BorderColor = borderColor;
            try { ClockText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fontColor)); } catch { }
            try { MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor)); } catch { }
        }
    }

    private void SetClickThrough(bool enable)
    {
        if (_windowHandle == IntPtr.Zero) return;
        var exStyle = GetWindowLong(_windowHandle, GWL_EXSTYLE);
        if (enable)
            exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
        else
            exStyle &= ~(WS_EX_TRANSPARENT | WS_EX_LAYERED);
        SetWindowLong(_windowHandle, GWL_EXSTYLE, exStyle);
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (key == null) return;

            if (enable)
            {
                var fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (fileName != null)
                    key.SetValue("DesktopClock", fileName);
            }
            else
            {
                key.DeleteValue("DesktopClock", false);
            }
        }
        catch
        {
        }
    }

    private void PickColor()
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)ClockText.Foreground).Color.R,
                ((SolidColorBrush)ClockText.Foreground).Color.G,
                ((SolidColorBrush)ClockText.Foreground).Color.B)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
            ClockText.Foreground = new SolidColorBrush(c);
            DateText.Foreground = new SolidColorBrush(c);
            _settings.FontColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

            var fg = new SolidColorBrush(c);
            MinimalTimeText.Foreground = fg;
            WordTimeText.Foreground = fg;
            HourArc.Stroke = fg;
            MinuteArc.Stroke = fg;
            SecondArc.Stroke = fg;
            ProgressHLabel.Foreground = fg;
            ProgressMLabel.Foreground = fg;
            ProgressSLabel.Foreground = fg;
            foreach (var tb in new TextBlock[] { FlipH1, FlipH2, FlipM1, FlipM2, FlipS1, FlipS2, FlipColon1, FlipColon2 })
                tb.Foreground = fg;

            _settings.Save();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isShuttingDown)
        {
            e.Cancel = true;
            this.Visibility = Visibility.Hidden;
            return;
        }

        if (_hotkeyRegistered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _hotkeyRegistered = false;
        }
        _trayIcon?.Dispose();
        _trayIcon = null;
        SavePosition();
        _settings.Save();
        base.OnClosing(e);
    }

    private void SavePosition()
    {
        var path = GetConfigPath();
        var dir = System.IO.Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, $"{this.Left},{this.Top}");
    }

    private void LoadPosition()
    {
        var path = GetConfigPath();
        if (File.Exists(path))
        {
            var parts = File.ReadAllText(path).Split(',');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out var left) &&
                double.TryParse(parts[1], out var top))
            {
                this.Left = left;
                this.Top = top;
                return;
            }
        }

        this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
        this.Top = 20;
    }

    private static string GetConfigPath()
    {
        return System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopClock", "pos.txt");
    }
}
