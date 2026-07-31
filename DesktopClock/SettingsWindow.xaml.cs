using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopClock.Contracts;
using DesktopClock.Services;

namespace DesktopClock;

public class PluginItem : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public bool InitiallyEnabled { get; set; }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }

    private readonly PluginManager? _pluginManager;
    private readonly ObservableCollection<PluginItem> _plugins = new();
    private bool _loaded;

    public SettingsWindow(AppSettings settings, PluginManager? pluginManager = null)
    {
        InitializeComponent();

        _pluginManager = pluginManager;

        // 深拷贝:保留 Layout/Components/Plugins/Global 等结构化数据,
        // 避免编辑期间丢失自由布局位置;取消时丢弃副本即可。
        // 必须使用 JsonOpts:NumberHandling=AllowNamedFloatingPointLiterals,
        // 否则当配置中存在 Infinity/NaN(如默认 GradientAngle 未初始化)时会抛 ArgumentException。
        Settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
            System.Text.Json.JsonSerializer.Serialize(settings, AppSettings.JsonOpts), AppSettings.JsonOpts) ?? new AppSettings();

        PopulateTimeZones();
        PopulateDualAnalogTimeZones();

        foreach (var item in HourFormatCombo.Items)
            if (item is ComboBoxItem ci && string.Equals(ci.Tag?.ToString(), Settings.Use24Hour.ToString(), StringComparison.OrdinalIgnoreCase))
                HourFormatCombo.SelectedItem = item;
        ShowSecondsCheck.IsChecked = Settings.ShowSeconds;
        foreach (var item in DisplayModeCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.DisplayMode)
                DisplayModeCombo.SelectedItem = item;
        WorldClockCheck.IsChecked = Settings.WorldClockEnabled;
        SelectTimeZone(Settings.WorldClockTimeZone);
        ChimeCheck.IsChecked = Settings.ChimeEnabled;

        // 时钟字体下拉框:枚举系统字体,默认选中 DS-Digital
        FontFamilyCombo.Items.Clear();
        foreach (var ff in System.Windows.Media.Fonts.SystemFontFamilies)
        {
            var item = new ComboBoxItem { Content = ff.Source, Tag = ff.Source };
            FontFamilyCombo.Items.Add(item);
            if (string.Equals(ff.Source, Settings.FontFamily, StringComparison.OrdinalIgnoreCase))
                FontFamilyCombo.SelectedItem = item;
        }
        if (FontFamilyCombo.SelectedItem == null)
        {
            // 未命中:优先找 DS-Digital,再退回第一项
            var dsdigital = FontFamilyCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Tag?.ToString(), "DS-Digital", StringComparison.OrdinalIgnoreCase));
            if (dsdigital != null) FontFamilyCombo.SelectedItem = dsdigital;
            else if (FontFamilyCombo.Items.Count > 0) FontFamilyCombo.SelectedIndex = 0;
        }
        FontSizeSlider.Value = Settings.FontSize;
        FontSizeLabel.Text = Settings.FontSize.ToString("F0");
        OpacitySlider.Value = Settings.BackgroundOpacity * 100;
        OpacityLabel.Text = $"{(int)(Settings.BackgroundOpacity * 100)}%";
        ColorBox.Text = Settings.FontColor;
        UpdateColorPreview();

        foreach (var item in BackgroundTypeCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.BackgroundType)
                BackgroundTypeCombo.SelectedItem = item;
        GradientStartBox.Text = Settings.GradientStartColor;
        GradientEndBox.Text = Settings.GradientEndColor;
        GradientAngleSlider.Value = Settings.GradientAngle;
        GradientAngleLabel.Text = Settings.GradientAngle.ToString("F0");
        UpdateGradientPreviews();

        BorderColorBox.Text = Settings.BorderColor;
        UpdateBorderColorPreview();
        BorderThicknessSlider.Value = Settings.BorderThickness;
        BorderThicknessLabel.Text = Settings.BorderThickness.ToString("F0");

        foreach (var item in ThemePresetCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.ThemePreset)
                ThemePresetCombo.SelectedItem = item;

        // 相册背景
        SkinBackgroundEnableCheck.IsChecked = Settings.SkinBackgroundEnabled;
        SkinBackgroundPathBox.Text = Settings.SkinBackgroundPath;
        SkinBackgroundOpacitySlider.Value = Settings.SkinBackgroundOpacity * 100;
        SkinBackgroundOpacityLabel.Text = $"{(int)(Settings.SkinBackgroundOpacity * 100)}%";
        SkinBackgroundBlurSlider.Value = Settings.SkinBackgroundBlur;
        SkinBackgroundBlurLabel.Text = Settings.SkinBackgroundBlur.ToString("F0");
        foreach (var item in SkinBackgroundStretchCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.SkinBackgroundStretch)
                SkinBackgroundStretchCombo.SelectedItem = item;
        SkinBackgroundPanel.Visibility = Settings.SkinBackgroundEnabled ? Visibility.Visible : Visibility.Collapsed;

        ShowDateCheck.IsChecked = Settings.ShowDate;

        // 日期字体下拉框:枚举系统字体,默认选中 DS-Digital
        DateFontFamilyCombo.Items.Clear();
        foreach (var ff in System.Windows.Media.Fonts.SystemFontFamilies)
        {
            var item = new ComboBoxItem { Content = ff.Source, Tag = ff.Source };
            DateFontFamilyCombo.Items.Add(item);
            if (string.Equals(ff.Source, Settings.DateFontFamily, StringComparison.OrdinalIgnoreCase))
                DateFontFamilyCombo.SelectedItem = item;
        }
        if (DateFontFamilyCombo.SelectedItem == null)
        {
            var dsdigital = DateFontFamilyCombo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Tag?.ToString(), "DS-Digital", StringComparison.OrdinalIgnoreCase));
            if (dsdigital != null) DateFontFamilyCombo.SelectedItem = dsdigital;
            else if (DateFontFamilyCombo.Items.Count > 0) DateFontFamilyCombo.SelectedIndex = 0;
        }
        DateFontSizeSlider.Value = Settings.DateFontSize;
        DateFontSizeLabel.Text = Settings.DateFontSize.ToString("F0");
        DateColorBox.Text = Settings.DateColor;
        UpdateDateColorPreview();
        foreach (var item in DatePositionCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.DatePosition)
                DatePositionCombo.SelectedItem = item;

        ClickThroughCheck.IsChecked = Settings.ClickThrough;
        SnapToEdgeCheck.IsChecked = Settings.SnapToEdge;
        SnapDistanceSlider.Value = Math.Clamp(Settings.SnapDistance, 5, 100);
        SnapDistanceLabel.Text = $"{(int)SnapDistanceSlider.Value}px";
        LockPositionCheck.IsChecked = Settings.LockPosition;
        HotkeyBox.Text = Settings.HotkeyHide;
        HotkeyCountdownBox.Text = Settings.HotkeyCountdown;

        AutoStartCheck.IsChecked = Settings.AutoStart;
        foreach (var item in LanguageCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.Language)
                LanguageCombo.SelectedItem = item;

        TimeZoneRow.Visibility = Settings.WorldClockEnabled ? Visibility.Visible : Visibility.Collapsed;

        // Dual analog
        foreach (var item in DualAnalogTimeZoneCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.DualAnalogTimeZone)
                DualAnalogTimeZoneCombo.SelectedItem = item;
        DualAnalogLabelBox.Text = Settings.DualAnalogLabel;
        UpdateDualAnalogVisibility();

        // Analog skin colors
        LoadAnalogSkinConfig();

        // Lunar calendar
        LunarCheck.IsChecked = Settings.LunarEnabled;
        SolarTermCheck.IsChecked = Settings.ShowSolarTerm;
        ZodiacCheck.IsChecked = Settings.ShowZodiac;
        LunarFontSizeSlider.Value = Settings.LunarFontSize;
        LunarFontSizeLabel.Text = Settings.LunarFontSize.ToString("F0");
        LunarColorBox.Text = Settings.LunarColor;
        UpdateLunarColorPreview();
        LunarSettingsPanel.Visibility = Settings.LunarEnabled ? Visibility.Visible : Visibility.Collapsed;

        // Reminders
        ReminderCheck.IsChecked = Settings.ReminderEnabled;
        ReminderSettingsPanel.Visibility = Settings.ReminderEnabled ? Visibility.Visible : Visibility.Collapsed;
        LoadReminderList();

        // Layout mode
        foreach (var item in LayoutModeCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.Layout.Mode)
                LayoutModeCombo.SelectedItem = item;

        // Plugins
        PluginListBox.ItemsSource = _plugins;
        RefreshPluginsList();
        PluginListBox.Visibility = _plugins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PluginStatusText.Text = _plugins.Count > 0 ? $"已加载 {_plugins.Count} 个插件" : "未检测到任何插件";

        // Backdrop
        foreach (var item in BackdropTypeCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.BackdropType)
                BackdropTypeCombo.SelectedItem = item;

        // Global filter
        GlobalFilterEnableCheck.IsChecked = Settings.GlobalFilterEnabled;
        VignetteSlider.Value = Settings.GlobalFilterVignette * 100;
        VignetteLabel.Text = $"{(int)(Settings.GlobalFilterVignette * 100)}%";
        GrayscaleSlider.Value = Settings.GlobalFilterGrayscale * 100;
        GrayscaleLabel.Text = $"{(int)(Settings.GlobalFilterGrayscale * 100)}%";
        ColorTempSlider.Value = Settings.GlobalFilterColorTemp * 100;
        ColorTempLabel.Text = Settings.GlobalFilterColorTemp.ToString("F0");
        GlobalFilterPanel.Visibility = Settings.GlobalFilterEnabled ? Visibility.Visible : Visibility.Collapsed;

        // AOD
        AodCheck.IsChecked = Settings.AodEnabled;
        AodIdleSlider.Value = Settings.AodIdleMinutes;
        AodIdleLabel.Text = Settings.AodIdleMinutes.ToString();
        FollowSystemThemeCheck.IsChecked = Settings.FollowSystemTheme;
        HoverOpacityCheck.IsChecked = Settings.HoverOpacityEnabled;
        HoverOpacitySlider.Value = Settings.HoverOpacity * 100;
        HoverOpacityLabel.Text = $"{(int)(Settings.HoverOpacity * 100)}%";

        // 夜间降透明度
        NightDimEnabledCheck.IsChecked = Settings.NightDimEnabled;
        NightDimStartHourBox.Text = Settings.NightDimStartHour.ToString();
        NightDimEndHourBox.Text = Settings.NightDimEndHour.ToString();
        NightDimOpacitySlider.Value = Settings.NightDimOpacity * 100;
        NightDimOpacityLabel.Text = $"{(int)(Settings.NightDimOpacity * 100)}%";

        // SysMon
        SysMonCheck.IsChecked = Settings.SysMonEnabled;
        SysMonCpuCheck.IsChecked = Settings.SysMonShowCpu;
        SysMonMemCheck.IsChecked = Settings.SysMonShowMemory;
        SysMonNetCheck.IsChecked = Settings.SysMonShowNetwork;
        SysMonBatCheck.IsChecked = Settings.SysMonShowBattery;
        SysMonPanel.Visibility = Settings.SysMonEnabled ? Visibility.Visible : Visibility.Collapsed;
        PopulateFontCombo(SysMonFontCombo, Settings.SysMonFontFamily);
        SysMonFontSizeSlider.Value = Settings.SysMonFontSize;
        SysMonFontSizeLabel.Text = Settings.SysMonFontSize.ToString("F0");
        SysMonColorBox.Text = Settings.SysMonFontColor;
        try { SysMonColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.SysMonFontColor)); } catch { }

        // Weather
        WeatherCheck.IsChecked = Settings.WeatherEnabled;
        WeatherCityBox.Text = Settings.WeatherCity;
        WeatherLatBox.Text = Settings.WeatherLatitude.ToString();
        WeatherLonBox.Text = Settings.WeatherLongitude.ToString();
        WeatherFontSizeSlider.Value = Settings.WeatherFontSize;
        WeatherFontSizeText.Text = Settings.WeatherFontSize.ToString("F0");
        WeatherDetailFontSizeSlider.Value = Settings.WeatherDetailFontSize;
        WeatherDetailFontSizeText.Text = Settings.WeatherDetailFontSize.ToString("F0");
        WeatherMainColorBox.Text = Settings.WeatherFontColor;
        WeatherDetailColorBox.Text = Settings.WeatherDetailColor;
        try { WeatherMainColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.WeatherFontColor)); } catch { }
        try { WeatherDetailColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.WeatherDetailColor)); } catch { }
        foreach (var item in WeatherAlignmentCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.WeatherAlignment)
                WeatherAlignmentCombo.SelectedItem = item;
        foreach (var item in WeatherPositionCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.WeatherPosition)
                WeatherPositionCombo.SelectedItem = item;
        WeatherPanel.Visibility = Settings.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;

        // Countdown:加载逻辑已迁移至 LoadCountdownSettings()

        // Todo scroll
        TodoScrollCheck.IsChecked = Settings.TodoScrollEnabled;
        TodoScrollTextBox.Text = Settings.TodoScrollText;
        TodoScrollSpeedSlider.Value = Settings.TodoScrollSpeed;
        TodoScrollSpeedLabel.Text = Settings.TodoScrollSpeed.ToString("F0");
        PopulateFontCombo(TodoScrollFontCombo, Settings.TodoScrollFontFamily);
        TodoScrollFontSizeSlider.Value = Settings.TodoScrollFontSize;
        TodoScrollFontSizeLabel.Text = Settings.TodoScrollFontSize.ToString("F0");
        TodoScrollColorBox.Text = Settings.TodoScrollFontColor;
        try { TodoScrollColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.TodoScrollFontColor)); } catch { }
        TodoScrollPanel.Visibility = Settings.TodoScrollEnabled ? Visibility.Visible : Visibility.Collapsed;

        // Auto switch
        AutoSwitchCheck.IsChecked = Settings.AutoSwitchEnabled;
        foreach (var item in AutoSwitchDayCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.AutoSwitchDayMode)
                AutoSwitchDayCombo.SelectedItem = item;
        foreach (var item in AutoSwitchNightCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.AutoSwitchNightMode)
                AutoSwitchNightCombo.SelectedItem = item;
        AutoSwitchDayHourBox.Text = Settings.AutoSwitchDayStartHour.ToString();
        AutoSwitchNightHourBox.Text = Settings.AutoSwitchNightStartHour.ToString();

        LoadCountdownSettings();

        _loaded = true;
    }

    /// <summary>
    /// 加载倒计时挂件配置到 UI 控件。
    /// 字段映射 AppSettings.Countdown* 系列(独立于时钟配置,互不干扰)。
    /// </summary>
    private void LoadCountdownSettings()
    {
        // 启用开关
        CountdownEnabledCheck.IsChecked = Settings.CountdownEnabled;

        // 目标时间(本地时间显示,UTC 存储)
        var target = Settings.CountdownTarget?.ToLocalTime() ?? DateTime.Now.AddDays(1);
        CountdownDatePicker.SelectedDate = target.Date;
        CountdownHourBox.Text = target.Hour.ToString("D2");
        CountdownMinuteBox.Text = target.Minute.ToString("D2");
        CountdownSecondBox.Text = target.Second.ToString("D2");

        // 标题
        CountdownLabelBox.Text = Settings.CountdownLabel;
        CountdownShowTitleCheck.IsChecked = Settings.CountdownShowTitle;

        // 显示模式
        foreach (var item in CountdownDisplayModeCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.CountdownDisplayMode)
                CountdownDisplayModeCombo.SelectedItem = item;

        // 结束动作
        foreach (var item in CountdownEndActionCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.CountdownEndAction)
                CountdownEndActionCombo.SelectedItem = item;

        CountdownStopAtZeroCheck.IsChecked = Settings.CountdownStopAtZero;

        // 字体下拉:枚举系统已安装字体
        CountdownFontFamilyCombo.Items.Clear();
        foreach (var ff in System.Windows.Media.Fonts.SystemFontFamilies)
        {
            var item = new ComboBoxItem { Content = ff.Source, Tag = ff.Source };
            CountdownFontFamilyCombo.Items.Add(item);
            if (string.Equals(ff.Source, Settings.CountdownFontFamily, StringComparison.OrdinalIgnoreCase))
                CountdownFontFamilyCombo.SelectedItem = item;
        }
        if (CountdownFontFamilyCombo.SelectedItem == null && CountdownFontFamilyCombo.Items.Count > 0)
            CountdownFontFamilyCombo.SelectedIndex = 0;

        // 字号
        CountdownFontSizeSlider.Value = Settings.CountdownFontSize;
        CountdownFontSizeLabel.Text = Settings.CountdownFontSize.ToString("F0");

        // 文字颜色
        CountdownFontColorBox.Text = Settings.CountdownFontColor;
        UpdateCountdownFontColorPreview();

        // 透明度
        CountdownOpacitySlider.Value = Settings.CountdownOpacity * 100;
        CountdownOpacityLabel.Text = $"{(int)(Settings.CountdownOpacity * 100)}%";

        // 描边
        CountdownStrokeEnabledCheck.IsChecked = Settings.CountdownStrokeEnabled;
        CountdownStrokeThicknessSlider.Value = Settings.CountdownStrokeThickness;
        CountdownStrokeThicknessLabel.Text = Settings.CountdownStrokeThickness.ToString("F1");
        CountdownStrokeColorBox.Text = Settings.CountdownStrokeColor;
        UpdateCountdownStrokeColorPreview();

        // 阴影
        CountdownShadowEnabledCheck.IsChecked = Settings.CountdownShadowEnabled;
        CountdownShadowSizeSlider.Value = Settings.CountdownShadowSize;
        CountdownShadowSizeLabel.Text = Settings.CountdownShadowSize.ToString("F0");
        CountdownShadowColorBox.Text = Settings.CountdownShadowColor;
        UpdateCountdownShadowColorPreview();
    }

    private void UpdateCountdownFontColorPreview()
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CountdownFontColorBox.Text);
            CountdownFontColorPreview.Background = new System.Windows.Media.SolidColorBrush(color);
        }
        catch { }
    }

    private void UpdateCountdownStrokeColorPreview()
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CountdownStrokeColorBox.Text);
            CountdownStrokeColorPreview.Background = new System.Windows.Media.SolidColorBrush(color);
        }
        catch { }
    }

    private void UpdateCountdownShadowColorPreview()
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CountdownShadowColorBox.Text);
            CountdownShadowColorPreview.Background = new System.Windows.Media.SolidColorBrush(color);
        }
        catch { }
    }

    private void PopulateTimeZones()
    {
        TimeZoneCombo.Items.Clear();
        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            var item = new ComboBoxItem
            {
                Content = tz.DisplayName,
                Tag = tz.Id
            };
            TimeZoneCombo.Items.Add(item);
        }
    }

    private void PopulateDualAnalogTimeZones()
    {
        DualAnalogTimeZoneCombo.Items.Clear();
        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            var item = new ComboBoxItem
            {
                Content = tz.DisplayName,
                Tag = tz.Id
            };
            DualAnalogTimeZoneCombo.Items.Add(item);
        }
    }

    private void UpdateDualAnalogVisibility()
    {
        bool isDualAnalog = (DisplayModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "dual_analog";
        DualAnalogRow.Visibility = isDualAnalog ? Visibility.Visible : Visibility.Collapsed;
        DualAnalogLabelRow.Visibility = isDualAnalog ? Visibility.Visible : Visibility.Collapsed;

        bool isAnalogSkin = (DisplayModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "analog_skin";
        AnalogSkinColorPanel.Visibility = isAnalogSkin ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DisplayModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // InitializeComponent 加载 XAML 时会先触发一次 SelectionChanged,此时 DualAnalogRow 等 UI 可能尚未初始化
        if (!_loaded || DualAnalogRow == null || DualAnalogLabelRow == null) return;
        UpdateDualAnalogVisibility();
    }

    private void SelectTimeZone(string id)
    {
        foreach (var item in TimeZoneCombo.Items)
        {
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == id)
            {
                TimeZoneCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void DisplaySegment_Click(object sender, MouseButtonEventArgs e)
    {
        ActivateSegment(DisplaySegment, DisplayPanel);
    }

    private void AppearanceSegment_Click(object sender, MouseButtonEventArgs e)
    {
        ActivateSegment(AppearanceSegment, AppearancePanel);
    }

    private void DateSegment2_Click(object sender, MouseButtonEventArgs e)
    {
        ActivateSegment(DateSegment2, DatePanel2);
    }

    private void FeaturesSegment_Click(object sender, MouseButtonEventArgs e)
    {
        ActivateSegment(FeaturesSegment, FeaturesPanel);
    }

    private void SystemSegment_Click(object sender, MouseButtonEventArgs e)
    {
        ActivateSegment(SystemSegment, SystemPanel);
    }

    private void CountdownSegment_Click(object sender, MouseButtonEventArgs e)
    {
        ActivateSegment(CountdownSegment, CountdownPanel);
    }

    private void ActivateSegment(Border active, ScrollViewer panel)
    {
        var segments = new[] { DisplaySegment, AppearanceSegment, DateSegment2, FeaturesSegment, SystemSegment, CountdownSegment };
        var panels = new ScrollViewer[] { DisplayPanel, AppearancePanel, DatePanel2, FeaturesPanel, SystemPanel, CountdownPanel };

        for (int i = 0; i < segments.Length; i++)
        {
            var isActive = segments[i] == active;
            segments[i].Background = isActive
                ? new SolidColorBrush(Colors.White)
                : Brushes.Transparent;
            ((TextBlock)segments[i].Child).Foreground = isActive
                ? new SolidColorBrush(Color.FromRgb(0x1D, 0x1D, 0x1F))
                : new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B));
            panels[i].Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void WorldClockCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.WorldClockEnabled = WorldClockCheck.IsChecked == true;
        TimeZoneRow.Visibility = Settings.WorldClockEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.FontFamily = (FontFamilyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.FontSize = e.NewValue;
        FontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.BackgroundOpacity = e.NewValue / 100.0;
        OpacityLabel.Text = $"{(int)e.NewValue}%";
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.FontColor = ColorBox.Text;
        UpdateColorPreview();
    }

    private void UpdateColorPreview()
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(ColorBox.Text);
            ColorPreview.Background = new SolidColorBrush(color);
        }
        catch
        {
            ColorPreview.Background = new SolidColorBrush(Colors.Gray);
        }
    }

    /// <summary>
    /// 填充字体下拉框,枚举系统已安装字体,并选中指定字体。
    /// </summary>
    private void PopulateFontCombo(ComboBox combo, string selectedFont)
    {
        combo.Items.Clear();
        foreach (var ff in System.Windows.Media.Fonts.SystemFontFamilies)
        {
            var item = new ComboBoxItem { Content = ff.Source, Tag = ff.Source };
            combo.Items.Add(item);
            if (string.Equals(ff.Source, selectedFont, StringComparison.OrdinalIgnoreCase))
                combo.SelectedItem = item;
        }
        if (combo.SelectedItem == null && combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void ColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)ColorPreview.Background).Color.R,
                ((SolidColorBrush)ColorPreview.Background).Color.G,
                ((SolidColorBrush)ColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = System.Drawing.Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            ColorBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private void BackgroundTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        var tag = (BackgroundTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "solid";
        Settings.BackgroundType = tag;
        GradientSettingsPanel.Visibility = tag == "gradient" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GradientStartBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.GradientStartColor = GradientStartBox.Text;
        UpdateGradientPreviews();
    }

    private void GradientEndBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.GradientEndColor = GradientEndBox.Text;
        UpdateGradientPreviews();
    }

    private void UpdateGradientPreviews()
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(GradientStartBox.Text);
            GradientStartPreview.Background = new SolidColorBrush(c);
        }
        catch { GradientStartPreview.Background = new SolidColorBrush(Colors.Gray); }
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(GradientEndBox.Text);
            GradientEndPreview.Background = new SolidColorBrush(c);
        }
        catch { GradientEndPreview.Background = new SolidColorBrush(Colors.Gray); }
    }

    private void GradientAngleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.GradientAngle = e.NewValue;
        GradientAngleLabel.Text = e.NewValue.ToString("F0");
    }

    private void BorderColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.BorderColor = BorderColorBox.Text;
        UpdateBorderColorPreview();
    }

    private void UpdateBorderColorPreview()
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(BorderColorBox.Text);
            BorderColorPreview.Background = new SolidColorBrush(color);
        }
        catch
        {
            BorderColorPreview.Background = new SolidColorBrush(Colors.Gray);
        }
    }

    private void BorderColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)BorderColorPreview.Background).Color.R,
                ((SolidColorBrush)BorderColorPreview.Background).Color.G,
                ((SolidColorBrush)BorderColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = System.Drawing.Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            BorderColorBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private void BorderThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.BorderThickness = e.NewValue;
        BorderThicknessLabel.Text = e.NewValue.ToString("F0");
    }

    private void ThemePresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        var tag = (ThemePresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default";
        Settings.ThemePreset = tag;
    }

    private void SkinBackgroundEnable_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.SkinBackgroundEnabled = SkinBackgroundEnableCheck.IsChecked == true;
        SkinBackgroundPanel.Visibility = Settings.SkinBackgroundEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SkinBackgroundPath_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.SkinBackgroundPath = SkinBackgroundPathBox.Text;
    }

    private void SkinBackgroundBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
            Title = "选择相册背景图片"
        };
        if (dialog.ShowDialog() == true)
            SkinBackgroundPathBox.Text = dialog.FileName;
    }

    private void SkinBackgroundOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.SkinBackgroundOpacity = e.NewValue / 100.0;
        SkinBackgroundOpacityLabel.Text = $"{(int)e.NewValue}%";
    }

    private void SkinBackgroundBlur_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.SkinBackgroundBlur = e.NewValue;
        SkinBackgroundBlurLabel.Text = e.NewValue.ToString("F0");
    }

    private void SkinBackgroundStretch_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.SkinBackgroundStretch = (SkinBackgroundStretchCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "UniformToFill";
    }

    private void ShowDateCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.ShowDate = ShowDateCheck.IsChecked == true;
    }

    private void DateFontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.DateFontFamily = (DateFontFamilyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
    }

    private void DateFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.DateFontSize = e.NewValue;
        DateFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void DateColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.DateColor = DateColorBox.Text;
        UpdateDateColorPreview();
    }

    private void UpdateDateColorPreview()
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(DateColorBox.Text);
            DateColorPreview.Background = new SolidColorBrush(color);
        }
        catch
        {
            DateColorPreview.Background = new SolidColorBrush(Colors.Gray);
        }
    }

    private void DateColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)DateColorPreview.Background).Color.R,
                ((SolidColorBrush)DateColorPreview.Background).Color.G,
                ((SolidColorBrush)DateColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = System.Drawing.Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            DateColorBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private void LunarCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.LunarEnabled = LunarCheck.IsChecked == true;
        LunarSettingsPanel.Visibility = Settings.LunarEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SolarTermCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.ShowSolarTerm = SolarTermCheck.IsChecked == true;
    }

    private void ZodiacCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.ShowZodiac = ZodiacCheck.IsChecked == true;
    }

    private void LunarFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.LunarFontSize = e.NewValue;
        LunarFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void LunarColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.LunarColor = LunarColorBox.Text;
        UpdateLunarColorPreview();
    }

    private void UpdateLunarColorPreview()
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(LunarColorBox.Text);
            LunarColorPreview.Background = new SolidColorBrush(color);
        }
        catch
        {
            LunarColorPreview.Background = new SolidColorBrush(Colors.Gray);
        }
    }

    private void LunarColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)LunarColorPreview.Background).Color.R,
                ((SolidColorBrush)LunarColorPreview.Background).Color.G,
                ((SolidColorBrush)LunarColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = System.Drawing.Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            LunarColorBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    #region 指针表盘颜色

    private void LoadAnalogSkinConfig()
    {
        var hc = Settings.GetComponentSetting("analog_clock_skin", "hourColor", "#3a2a1a");
        var mc = Settings.GetComponentSetting("analog_clock_skin", "minuteColor", "#2a2a2a");
        var sc = Settings.GetComponentSetting("analog_clock_skin", "secondColor", "#cc3333");
        var th = Settings.GetComponentSetting("analog_clock_skin", "handThickness", 1.0);
        var ss = Settings.GetComponentSetting("analog_clock_skin", "showSecondHand", true);
        var st = Settings.GetComponentSetting("analog_clock_skin", "showTicks", true);
        var cd = Settings.GetComponentSetting("analog_clock_skin", "showCenterDot", true);
        var tkc = Settings.GetComponentSetting("analog_clock_skin", "tickColor", "#808080");

        AnalogHourColorBox.Text = hc;
        AnalogMinuteColorBox.Text = mc;
        AnalogSecondColorBox.Text = sc;
        AnalogTickColorBox.Text = tkc;
        AnalogHandThicknessSlider.Value = th;
        AnalogHandThicknessLabel.Text = th.ToString("F1");
        AnalogShowSecondHandCheck.IsChecked = ss;
        AnalogShowTicksCheck.IsChecked = st;
        AnalogShowCenterDotCheck.IsChecked = cd;
        UpdateAnalogColorPreviews();
    }

    private void UpdateAnalogColorPreviews()
    {
        try { AnalogHourColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AnalogHourColorBox.Text)); } catch { }
        try { AnalogMinuteColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AnalogMinuteColorBox.Text)); } catch { }
        try { AnalogSecondColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AnalogSecondColorBox.Text)); } catch { }
        try { AnalogTickColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AnalogTickColorBox.Text)); } catch { }
    }

    private void AnalogHourColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)AnalogHourColorPreview.Background).Color.R,
                ((SolidColorBrush)AnalogHourColorPreview.Background).Color.G,
                ((SolidColorBrush)AnalogHourColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AnalogHourColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void AnalogMinuteColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)AnalogMinuteColorPreview.Background).Color.R,
                ((SolidColorBrush)AnalogMinuteColorPreview.Background).Color.G,
                ((SolidColorBrush)AnalogMinuteColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AnalogMinuteColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void AnalogSecondColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)AnalogSecondColorPreview.Background).Color.R,
                ((SolidColorBrush)AnalogSecondColorPreview.Background).Color.G,
                ((SolidColorBrush)AnalogSecondColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AnalogSecondColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void AnalogTickColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)AnalogTickColorPreview.Background).Color.R,
                ((SolidColorBrush)AnalogTickColorPreview.Background).Color.G,
                ((SolidColorBrush)AnalogTickColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AnalogTickColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void AnalogHourColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        UpdateAnalogColorPreviews();
    }

    private void AnalogMinuteColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        UpdateAnalogColorPreviews();
    }

    private void AnalogSecondColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        UpdateAnalogColorPreviews();
    }

    private void AnalogTickColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        UpdateAnalogColorPreviews();
    }

    private void AnalogHandThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || AnalogHandThicknessLabel == null) return;
        AnalogHandThicknessLabel.Text = e.NewValue.ToString("F1");
    }

    #endregion

    private void ReminderCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.ReminderEnabled = ReminderCheck.IsChecked == true;
        ReminderSettingsPanel.Visibility = Settings.ReminderEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private List<ReminderItem> GetReminders()
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<ReminderItem>>(Settings.RemindersJson) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private void SaveReminders(List<ReminderItem> list)
    {
        Settings.RemindersJson = System.Text.Json.JsonSerializer.Serialize(list);
    }

    private void LoadReminderList()
    {
        var list = GetReminders();
        ReminderListBox.Items.Clear();
        foreach (var r in list)
            ReminderListBox.Items.Add(r);
    }

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ReminderDialog();
        if (dialog.ShowDialog() == true)
        {
            var list = GetReminders();
            list.Add(dialog.Reminder);
            SaveReminders(list);
            LoadReminderList();
        }
    }

    private void EditReminder_Click(object sender, RoutedEventArgs e)
    {
        if (ReminderListBox.SelectedItem is not ReminderItem item) return;
        var dialog = new ReminderDialog(item);
        if (dialog.ShowDialog() == true)
        {
            var list = GetReminders();
            int idx = list.FindIndex(r => r.Id == item.Id);
            if (idx >= 0)
            {
                list[idx] = dialog.Reminder;
                SaveReminders(list);
                LoadReminderList();
            }
        }
    }

    private void PluginCheck_Changed(object sender, RoutedEventArgs e)
    {
        // Handled via data binding directly to PluginItem.Enabled
    }

    private void DeleteReminder_Click(object sender, RoutedEventArgs e)
    {
        if (ReminderListBox.SelectedItem is not ReminderItem item) return;
        var result = MessageBox.Show("确定删除此提醒？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            var list = GetReminders();
            list.RemoveAll(r => r.Id == item.Id);
            SaveReminders(list);
            LoadReminderList();
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Settings.Use24Hour = (HourFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "true";
        Settings.ShowSeconds = ShowSecondsCheck.IsChecked == true;
        Settings.DisplayMode = (DisplayModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "digital";
        Settings.WorldClockEnabled = WorldClockCheck.IsChecked == true;
        Settings.WorldClockTimeZone = (TimeZoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "China Standard Time";
        Settings.ChimeEnabled = ChimeCheck.IsChecked == true;

        Settings.FontFamily = (FontFamilyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
        Settings.FontSize = FontSizeSlider.Value;
        Settings.BackgroundOpacity = OpacitySlider.Value / 100.0;
        Settings.FontColor = ColorBox.Text;
        Settings.BackgroundType = (BackgroundTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "solid";
        Settings.GradientStartColor = GradientStartBox.Text;
        Settings.GradientEndColor = GradientEndBox.Text;
        Settings.GradientAngle = GradientAngleSlider.Value;
        Settings.BorderColor = BorderColorBox.Text;
        Settings.BorderThickness = BorderThicknessSlider.Value;
        Settings.ThemePreset = (ThemePresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default";

        Settings.SkinBackgroundEnabled = SkinBackgroundEnableCheck.IsChecked == true;
        Settings.SkinBackgroundPath = SkinBackgroundPathBox.Text;
        Settings.SkinBackgroundOpacity = SkinBackgroundOpacitySlider.Value / 100.0;
        Settings.SkinBackgroundBlur = SkinBackgroundBlurSlider.Value;
        Settings.SkinBackgroundStretch = (SkinBackgroundStretchCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "UniformToFill";

        Settings.ShowDate = ShowDateCheck.IsChecked == true;
        Settings.DateFontFamily = (DateFontFamilyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
        Settings.DateFontSize = DateFontSizeSlider.Value;
        Settings.DateColor = DateColorBox.Text;
        Settings.DatePosition = (DatePositionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "top";

        Settings.ClickThrough = ClickThroughCheck.IsChecked == true;
        Settings.SnapToEdge = SnapToEdgeCheck.IsChecked == true;
        Settings.SnapDistance = (int)SnapDistanceSlider.Value;
        Settings.LockPosition = LockPositionCheck.IsChecked == true;
        Settings.HotkeyHide = HotkeyBox.Text;
        Settings.HotkeyCountdown = HotkeyCountdownBox.Text;

        Settings.AutoStart = AutoStartCheck.IsChecked == true;
        // 同步注册表写入,使开关即时生效(下次开机自启/取消)
        try { App.SetAutoStart(Settings.AutoStart); }
        catch { /* 注册表写入失败不阻塞设置保存 */ }
        Settings.Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "zh";
        // 即时应用语言切换(zh / en / ja),所有 DynamicResource 绑定自动刷新
        try { I18n.Apply(Settings.Language); }
        catch { /* 语言切换失败不阻塞设置保存 */ }

        Settings.LunarEnabled = LunarCheck.IsChecked == true;
        Settings.ShowSolarTerm = SolarTermCheck.IsChecked == true;
        Settings.ShowZodiac = ZodiacCheck.IsChecked == true;
        Settings.LunarFontSize = LunarFontSizeSlider.Value;
        Settings.LunarColor = LunarColorBox.Text;
        Settings.ReminderEnabled = ReminderCheck.IsChecked == true;

        Settings.Layout.Mode = (LayoutModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "stack";

        // Backdrop / Filter
        Settings.BackdropType = (BackdropTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "none";
        Settings.GlobalFilterEnabled = GlobalFilterEnableCheck.IsChecked == true;
        Settings.GlobalFilterVignette = VignetteSlider.Value / 100.0;
        Settings.GlobalFilterGrayscale = GrayscaleSlider.Value / 100.0;
        Settings.GlobalFilterColorTemp = ColorTempSlider.Value / 100.0;

        // AOD / Theme / Hover
        Settings.AodEnabled = AodCheck.IsChecked == true;
        Settings.AodIdleMinutes = (int)AodIdleSlider.Value;
        Settings.FollowSystemTheme = FollowSystemThemeCheck.IsChecked == true;
        Settings.HoverOpacityEnabled = HoverOpacityCheck.IsChecked == true;
        Settings.HoverOpacity = HoverOpacitySlider.Value / 100.0;

        // 夜间降透明度
        Settings.NightDimEnabled = NightDimEnabledCheck.IsChecked == true;
        if (int.TryParse(NightDimStartHourBox.Text, out var nsh)) Settings.NightDimStartHour = Math.Clamp(nsh, 0, 23);
        if (int.TryParse(NightDimEndHourBox.Text, out var neh)) Settings.NightDimEndHour = Math.Clamp(neh, 0, 23);
        Settings.NightDimOpacity = NightDimOpacitySlider.Value / 100.0;

        // SysMon
        Settings.SysMonEnabled = SysMonCheck.IsChecked == true;
        Settings.SysMonShowCpu = SysMonCpuCheck.IsChecked == true;
        Settings.SysMonShowMemory = SysMonMemCheck.IsChecked == true;
        Settings.SysMonShowNetwork = SysMonNetCheck.IsChecked == true;
        Settings.SysMonShowBattery = SysMonBatCheck.IsChecked == true;
        Settings.SysMonFontFamily = (SysMonFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Consolas";
        Settings.SysMonFontSize = SysMonFontSizeSlider.Value;
        Settings.SysMonFontColor = SysMonColorBox.Text;

        // Weather
        Settings.WeatherEnabled = WeatherCheck.IsChecked == true;
        Settings.WeatherCity = WeatherCityBox.Text;
        if (double.TryParse(WeatherLatBox.Text, out var lat)) Settings.WeatherLatitude = lat;
        if (double.TryParse(WeatherLonBox.Text, out var lon)) Settings.WeatherLongitude = lon;
        Settings.WeatherFontSize = WeatherFontSizeSlider.Value;
        Settings.WeatherDetailFontSize = WeatherDetailFontSizeSlider.Value;
        Settings.WeatherFontColor = WeatherMainColorBox.Text;
        Settings.WeatherDetailColor = WeatherDetailColorBox.Text;
        Settings.WeatherAlignment = (WeatherAlignmentCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "center";
        Settings.WeatherPosition = (WeatherPositionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "bottom";

        // Countdown
        // Countdown:保存逻辑已迁移至 SaveCountdownSettings()

        // Todo scroll
        Settings.TodoScrollEnabled = TodoScrollCheck.IsChecked == true;
        Settings.TodoScrollText = TodoScrollTextBox.Text;
        Settings.TodoScrollSpeed = TodoScrollSpeedSlider.Value;
        Settings.TodoScrollFontFamily = (TodoScrollFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei";
        Settings.TodoScrollFontSize = TodoScrollFontSizeSlider.Value;
        Settings.TodoScrollFontColor = TodoScrollColorBox.Text;

        // Auto switch
        Settings.AutoSwitchEnabled = AutoSwitchCheck.IsChecked == true;
        Settings.AutoSwitchDayMode = (AutoSwitchDayCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "digital";
        Settings.AutoSwitchNightMode = (AutoSwitchNightCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "minimal";
        if (int.TryParse(AutoSwitchDayHourBox.Text, out var dsh)) Settings.AutoSwitchDayStartHour = Math.Clamp(dsh, 0, 23);
        if (int.TryParse(AutoSwitchNightHourBox.Text, out var asnh)) Settings.AutoSwitchNightStartHour = Math.Clamp(asnh, 0, 23);

        // Dual analog
        Settings.DualAnalogTimeZone = (DualAnalogTimeZoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Eastern Standard Time";
        Settings.DualAnalogLabel = DualAnalogLabelBox.Text;

        // Analog skin colors
        Settings.SetComponentSetting("analog_clock_skin", "hourColor", AnalogHourColorBox.Text);
        Settings.SetComponentSetting("analog_clock_skin", "minuteColor", AnalogMinuteColorBox.Text);
        Settings.SetComponentSetting("analog_clock_skin", "secondColor", AnalogSecondColorBox.Text);
        Settings.SetComponentSetting("analog_clock_skin", "tickColor", AnalogTickColorBox.Text);
        Settings.SetComponentSetting("analog_clock_skin", "handThickness", AnalogHandThicknessSlider.Value);
        Settings.SetComponentSetting("analog_clock_skin", "showSecondHand", AnalogShowSecondHandCheck.IsChecked == true);
        Settings.SetComponentSetting("analog_clock_skin", "showTicks", AnalogShowTicksCheck.IsChecked == true);
        Settings.SetComponentSetting("analog_clock_skin", "showCenterDot", AnalogShowCenterDotCheck.IsChecked == true);

        // Save plugin enabled states
        foreach (var pi in _plugins)
            Settings.Plugins[pi.Id] = pi.Enabled;

        var finalList = new System.Collections.Generic.List<ReminderItem>();
        foreach (ReminderItem item in ReminderListBox.Items)
            finalList.Add(item);
        Settings.RemindersJson = System.Text.Json.JsonSerializer.Serialize(finalList);

        // 保存倒计时挂件配置
        SaveCountdownSettings();

        Settings.Save();
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 将 UI 控件的倒计时配置写回 Settings。
    /// 目标时间以 UTC 存储,显示时转回本地时间。
    /// </summary>
    private void SaveCountdownSettings()
    {
        Settings.CountdownEnabled = CountdownEnabledCheck.IsChecked == true;

        // 目标时间:合并 DatePicker + 时分秒 TextBox(使用 TryBuildCountdownTarget)
        var built = TryBuildCountdownTarget();
        if (built.HasValue) Settings.CountdownTarget = built.Value;

        Settings.CountdownLabel = CountdownLabelBox.Text;
        Settings.CountdownShowTitle = CountdownShowTitleCheck.IsChecked == true;
        Settings.CountdownDisplayMode = (CountdownDisplayModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "days";
        Settings.CountdownEndAction = (CountdownEndActionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "blink";
        Settings.CountdownStopAtZero = CountdownStopAtZeroCheck.IsChecked == true;

        Settings.CountdownFontFamily = (CountdownFontFamilyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei UI";
        Settings.CountdownFontSize = CountdownFontSizeSlider.Value;
        Settings.CountdownFontColor = CountdownFontColorBox.Text;
        Settings.CountdownOpacity = CountdownOpacitySlider.Value / 100.0;

        Settings.CountdownStrokeEnabled = CountdownStrokeEnabledCheck.IsChecked == true;
        Settings.CountdownStrokeThickness = CountdownStrokeThicknessSlider.Value;
        Settings.CountdownStrokeColor = CountdownStrokeColorBox.Text;

        Settings.CountdownShadowEnabled = CountdownShadowEnabledCheck.IsChecked == true;
        Settings.CountdownShadowSize = CountdownShadowSizeSlider.Value;
        Settings.CountdownShadowColor = CountdownShadowColorBox.Text;
    }

    #region New Event Handlers

    private void BackdropTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.BackdropType = (BackdropTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "none";
    }

    private void GlobalFilterEnable_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.GlobalFilterEnabled = GlobalFilterEnableCheck.IsChecked == true;
        GlobalFilterPanel.Visibility = Settings.GlobalFilterEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void VignetteSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.GlobalFilterVignette = e.NewValue / 100.0;
        VignetteLabel.Text = $"{(int)e.NewValue}%";
    }

    private void GrayscaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.GlobalFilterGrayscale = e.NewValue / 100.0;
        GrayscaleLabel.Text = $"{(int)e.NewValue}%";
    }

    private void ColorTempSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.GlobalFilterColorTemp = e.NewValue / 100.0;
        ColorTempLabel.Text = e.NewValue.ToString("F0");
    }

    private void SysMonCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.SysMonEnabled = SysMonCheck.IsChecked == true;
        SysMonPanel.Visibility = Settings.SysMonEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SysMonFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || SysMonFontSizeLabel == null) return;
        Settings.SysMonFontSize = e.NewValue;
        SysMonFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void SysMonColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)SysMonColorPreview.Background).Color.R,
                ((SolidColorBrush)SysMonColorPreview.Background).Color.G,
                ((SolidColorBrush)SysMonColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SysMonColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void SysMonColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.SysMonFontColor = SysMonColorBox.Text;
        try { SysMonColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SysMonColorBox.Text)); }
        catch { }
    }

    private void WeatherCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.WeatherEnabled = WeatherCheck.IsChecked == true;
        WeatherPanel.Visibility = Settings.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WeatherFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || WeatherFontSizeText == null) return;
        WeatherFontSizeText.Text = e.NewValue.ToString("F0");
    }

    private void WeatherDetailFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || WeatherDetailFontSizeText == null) return;
        WeatherDetailFontSizeText.Text = e.NewValue.ToString("F0");
    }

    private void WeatherMainColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded || WeatherMainColorPreview == null) return;
        try { WeatherMainColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(WeatherMainColorBox.Text)); }
        catch { WeatherMainColorPreview.Background = new SolidColorBrush(Colors.Gray); }
    }

    private void WeatherMainColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var dialog = new System.Windows.Forms.ColorDialog
            {
                AllowFullOpen = true,
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(
                    ((SolidColorBrush)WeatherMainColorPreview.Background).Color.R,
                    ((SolidColorBrush)WeatherMainColorPreview.Background).Color.G,
                    ((SolidColorBrush)WeatherMainColorPreview.Background).Color.B)
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = System.Drawing.Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                var hex = $"#{dialog.Color.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                WeatherMainColorBox.Text = hex;
                WeatherMainColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
        }
        catch { }
    }

    private void WeatherDetailColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded || WeatherDetailColorPreview == null) return;
        try { WeatherDetailColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(WeatherDetailColorBox.Text)); }
        catch { WeatherDetailColorPreview.Background = new SolidColorBrush(Colors.Gray); }
    }

    private void WeatherDetailColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var dialog = new System.Windows.Forms.ColorDialog
            {
                AllowFullOpen = true,
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(
                    ((SolidColorBrush)WeatherDetailColorPreview.Background).Color.R,
                    ((SolidColorBrush)WeatherDetailColorPreview.Background).Color.G,
                    ((SolidColorBrush)WeatherDetailColorPreview.Background).Color.B)
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = System.Drawing.Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                var hex = $"#{dialog.Color.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                WeatherDetailColorBox.Text = hex;
                WeatherDetailColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
        }
        catch { }
    }

    // CountdownCheck_Changed 已废弃:倒计时配置迁移至独立 Tab,见 CountdownEnabledCheck_Changed

    private void TodoScrollCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.TodoScrollEnabled = TodoScrollCheck.IsChecked == true;
        TodoScrollPanel.Visibility = Settings.TodoScrollEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TodoScrollSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.TodoScrollSpeed = e.NewValue;
        TodoScrollSpeedLabel.Text = e.NewValue.ToString("F0");
    }

    private void TodoScrollFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || TodoScrollFontSizeLabel == null) return;
        Settings.TodoScrollFontSize = e.NewValue;
        TodoScrollFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void TodoScrollColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)TodoScrollColorPreview.Background).Color.R,
                ((SolidColorBrush)TodoScrollColorPreview.Background).Color.G,
                ((SolidColorBrush)TodoScrollColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TodoScrollColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void TodoScrollColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.TodoScrollFontColor = TodoScrollColorBox.Text;
        try { TodoScrollColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(TodoScrollColorBox.Text)); }
        catch { }
    }

    private void MediaInfoCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.MediaInfoEnabled = MediaInfoCheck.IsChecked == true;
        MediaInfoPanel.Visibility = Settings.MediaInfoEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AodIdleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.AodIdleMinutes = (int)e.NewValue;
        AodIdleLabel.Text = e.NewValue.ToString("F0");
    }

    private void HoverOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.HoverOpacity = e.NewValue / 100.0;
        HoverOpacityLabel.Text = $"{(int)e.NewValue}%";
    }

    private void NightDimOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.NightDimOpacity = e.NewValue / 100.0;
        NightDimOpacityLabel.Text = $"{(int)e.NewValue}%";
    }

    private void SnapDistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.SnapDistance = (int)e.NewValue;
        SnapDistanceLabel.Text = $"{(int)e.NewValue}px";
    }

    #endregion

    #region Countdown Tab Handlers

    private void CountdownEnabledCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.CountdownEnabled = CountdownEnabledCheck.IsChecked == true;
    }

    /// <summary>
    /// 倒计时日历控件日期变更:与时分秒一起合并到 CountdownTarget。
    /// </summary>
    private void CountdownDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        TryBuildCountdownTarget();
    }

    /// <summary>
    /// 倒计时目标时间变化:合并 DatePicker + 时分秒 TextBox 到 CountdownTarget。
    /// 非法输入使用 Clamp 修正(小时 0-23,分/秒 0-59)。
    /// </summary>
    private void CountdownTimeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        TryBuildCountdownTarget();
    }

    private DateTime? TryBuildCountdownTarget()
    {
        var d = CountdownDatePicker.SelectedDate ?? DateTime.Today.AddDays(1);
        if (!int.TryParse(CountdownHourBox?.Text ?? "", out var h)) h = 0;
        if (!int.TryParse(CountdownMinuteBox?.Text ?? "", out var m)) m = 0;
        if (!int.TryParse(CountdownSecondBox?.Text ?? "", out var s)) s = 0;
        h = Math.Clamp(h, 0, 23);
        m = Math.Clamp(m, 0, 59);
        s = Math.Clamp(s, 0, 59);
        var localTarget = new DateTime(d.Year, d.Month, d.Day, h, m, s, DateTimeKind.Local);
        Settings.CountdownTarget = localTarget.ToUniversalTime();
        return Settings.CountdownTarget;
    }

    private void CountdownLabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.CountdownLabel = CountdownLabelBox.Text;
    }

    private void CountdownFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.CountdownFontSize = e.NewValue;
        CountdownFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void CountdownFontColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.CountdownFontColor = CountdownFontColorBox.Text;
        UpdateCountdownFontColorPreview();
    }

    private void CountdownFontColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)CountdownFontColorPreview.Background).Color.R,
                ((SolidColorBrush)CountdownFontColorPreview.Background).Color.G,
                ((SolidColorBrush)CountdownFontColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dialog.Color;
            CountdownFontColorBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private void CountdownOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.CountdownOpacity = e.NewValue / 100.0;
        CountdownOpacityLabel.Text = $"{(int)e.NewValue}%";
    }

    private void CountdownStrokeThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.CountdownStrokeThickness = e.NewValue;
        CountdownStrokeThicknessLabel.Text = e.NewValue.ToString("F1");
    }

    private void CountdownStrokeColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.CountdownStrokeColor = CountdownStrokeColorBox.Text;
        UpdateCountdownStrokeColorPreview();
    }

    private void CountdownStrokeColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)CountdownStrokeColorPreview.Background).Color.R,
                ((SolidColorBrush)CountdownStrokeColorPreview.Background).Color.G,
                ((SolidColorBrush)CountdownStrokeColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dialog.Color;
            CountdownStrokeColorBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private void CountdownShadowSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        Settings.CountdownShadowSize = e.NewValue;
        CountdownShadowSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void CountdownShadowColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.CountdownShadowColor = CountdownShadowColorBox.Text;
        UpdateCountdownShadowColorPreview();
    }

    private void CountdownShadowColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)CountdownShadowColorPreview.Background).Color.R,
                ((SolidColorBrush)CountdownShadowColorPreview.Background).Color.G,
                ((SolidColorBrush)CountdownShadowColorPreview.Background).Color.B)
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dialog.Color;
            CountdownShadowColorBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    #endregion

    #region Log Folder

    /// <summary>
    /// 打开 Serilog 日志目录(%LOCALAPPDATA%\DesktopClock\logs)。
    /// 失败时弹窗提示,不阻塞设置窗口。
    /// </summary>
    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopClock", "logs");
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开日志文件夹:{ex.Message}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    #region Plugin Manager UI

    /// <summary>
    /// 刷新插件列表:优先从已加载的 PluginManager 读取,
    /// 若为空则直接扫描 Plugins 目录所有 DLL 反射元数据(不启用 LoadContext,确保无需重启)。
    /// </summary>
    private void RefreshPluginsList()
    {
        _plugins.Clear();
        if (_pluginManager != null && _pluginManager.LoadedPlugins.Count > 0)
        {
            foreach (var kvp in _pluginManager.LoadedPlugins)
            {
                var plugin = kvp.Value;
                var enabled = !Settings.Plugins.TryGetValue(plugin.Id, out var e) || e;
                _plugins.Add(new PluginItem
                {
                    Id = plugin.Id,
                    Name = plugin.Name,
                    Version = $"v{plugin.Version}",
                    Description = plugin.Description,
                    Enabled = enabled,
                    InitiallyEnabled = enabled
                });
            }
            return;
        }
        // PluginManager 尚未加载 → 直接扫描 DLL 反射元数据
        try
        {
            var pluginsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            if (!System.IO.Directory.Exists(pluginsDir)) return;
            foreach (var dll in System.IO.Directory.GetFiles(pluginsDir, "*.dll",
                System.IO.SearchOption.AllDirectories))
            {
                try
                {
                    var asm = System.Reflection.Assembly.LoadFrom(dll);
                    foreach (var t in asm.GetTypes())
                    {
                        if (!typeof(Contracts.IPlugin).IsAssignableFrom(t) || t.IsAbstract || t.IsInterface) continue;
                        var plugin = (Contracts.IPlugin)Activator.CreateInstance(t)!;
                        var enabled = !Settings.Plugins.TryGetValue(plugin.Id, out var e) || e;
                        _plugins.Add(new PluginItem
                        {
                            Id = plugin.Id,
                            Name = plugin.Name,
                            Version = $"v{plugin.Version}",
                            Description = plugin.Description,
                            Enabled = enabled,
                            InitiallyEnabled = enabled
                        });
                        break;
                    }
                }
                catch { /* 单个 DLL 加载失败不影响其他 */ }
            }
        }
        catch { }
    }

    private void RefreshPluginsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPluginsList();
        PluginListBox.Visibility = _plugins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PluginStatusText.Text = _plugins.Count > 0 ? $"已加载 {_plugins.Count} 个插件(更改需重启程序生效)" : "未检测到任何插件";
    }

    private void OpenPluginFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开插件目录:{ex.Message}", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    #endregion

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
