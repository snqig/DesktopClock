using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DesktopClock.Core;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopClock.Contracts;
using DesktopClock.Services;
using DesktopClock.Models;
using DesktopClock.Components;

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

        // === P0-P4 组件设置加载 ===
        LoadHealthReminderSettings();
        LoadPomodoroSettings();
        LoadDailyQuoteSettings();
        LoadHabitTrackerSettings();
        LoadFloatWindowSettings();

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

        // P0:多任务倒计时
        CountdownTasksEnabledCheck.IsChecked = Settings.CountdownTasks.Count > 0;
        CountdownRotationSecBox.Text = Settings.CountdownTaskRotationSeconds.ToString();
        RenderCountdownTasksUI();
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

    private void ComponentsSegment_Click(object sender, MouseButtonEventArgs e)
    {
        ActivateSegment(ComponentsSegment, ComponentsPanel);
    }

    private void ActivateSegment(Border active, ScrollViewer panel)
    {
        var segments = new[] { DisplaySegment, AppearanceSegment, DateSegment2, FeaturesSegment, SystemSegment, CountdownSegment, ComponentsSegment };
        var panels = new ScrollViewer[] { DisplayPanel, AppearancePanel, DatePanel2, FeaturesPanel, SystemPanel, CountdownPanel, ComponentsPanel };

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

        // === P1-P4 组件设置写回 ===
        // P1:健康提醒
        Settings.HealthReminderEnabled = HealthReminderCheck.IsChecked == true;
        if (int.TryParse(HealthWorkStartBox.Text, out var hws)) Settings.HealthReminderWorkStartHour = Math.Clamp(hws, 0, 23);
        if (int.TryParse(HealthWorkEndBox.Text, out var hwe)) Settings.HealthReminderWorkEndHour = Math.Clamp(hwe, 0, 24);
        Settings.HealthReminderFontFamily = (HealthReminderFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei UI";
        Settings.HealthReminderFontSize = HealthReminderFontSizeSlider.Value;
        Settings.HealthReminderFontColor = HealthReminderColorBox.Text;
        SaveHealthRemindersFromUI();

        // P2:番茄钟
        Settings.PomodoroEnabled = PomodoroCheck.IsChecked == true;
        Settings.PomodoroFocusMinutes = (int)PomodoroFocusSlider.Value;
        Settings.PomodoroShortBreakMinutes = (int)PomodoroShortSlider.Value;
        Settings.PomodoroLongBreakMinutes = (int)PomodoroLongSlider.Value;
        Settings.PomodoroLongBreakInterval = (int)PomodoroIntervalSlider.Value;
        Settings.PomodoroAutoStart = PomodoroAutoStartCheck.IsChecked == true;
        Settings.PomodoroFontFamily = (PomodoroFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei UI";
        Settings.PomodoroFontSize = PomodoroFontSizeSlider.Value;
        Settings.PomodoroFontColor = PomodoroColorBox.Text;

        // P3:每日一言
        Settings.DailyQuoteEnabled = DailyQuoteCheck.IsChecked == true;
        Settings.DailyQuoteApiEnabled = DailyQuoteApiCheck.IsChecked == true;
        Settings.DailyQuoteApiUrl = DailyQuoteApiBox.Text;
        Settings.DailyQuoteSpeed = DailyQuoteSpeedSlider.Value;
        Settings.DailyQuoteFontFamily = (DailyQuoteFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei";
        Settings.DailyQuoteFontSize = DailyQuoteFontSizeSlider.Value;
        Settings.DailyQuoteFontColor = DailyQuoteColorBox.Text;

        // P4:习惯打卡
        Settings.HabitTrackerEnabled = HabitTrackerCheck.IsChecked == true;
        Settings.HabitTrackerFontFamily = (HabitTrackerFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei UI";
        Settings.HabitTrackerFontSize = HabitTrackerFontSizeSlider.Value;
        Settings.HabitTrackerFontColor = HabitTrackerColorBox.Text;
        SaveHabitsFromUI();

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

        // 保存独立悬浮窗口配置
        SaveFloatWindowSettings();

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

        // P0:多任务倒计时任务列表 + 轮播配置
        if (int.TryParse(CountdownRotationSecBox.Text, out var rsec))
            Settings.CountdownTaskRotationSeconds = Math.Clamp(rsec, 1, 120);
        SaveCountdownTasksFromUI();
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

    #region P0-P4 组件设置辅助方法

    // ========================================================================================
    // P0: 多任务倒计时 CountdownTask
    // ========================================================================================

    // UI 模型:一行对应一个 CountdownTask
    private class CountdownTaskRowItem
    {
        public CheckBox EnabledCheck = null!;
        public TextBox TitleBox = null!;
        public DatePicker DatePicker = null!;
        public TextBox HourBox = null!;
        public TextBox MinuteBox = null!;
        public TextBox SecondBox = null!;
        public ComboBox DisplayModeCombo = null!;
        public CheckBox ShowTitleCheck = null!;
        public Button DeleteButton = null!;
        public Grid Row = null!;
        public CountdownTask Model = null!;
    }
    private readonly List<CountdownTaskRowItem> _countdownTaskRows = new();

    private void RenderCountdownTasksUI()
    {
        _countdownTaskRows.Clear();
        CountdownTasksListBox.ItemsSource = null;
        var list = new List<Grid>();
        foreach (var task in Settings.CountdownTasks)
        {
            var row = BuildCountdownTaskRow(task);
            list.Add(row.Row);
        }
        CountdownTasksListBox.ItemsSource = list;
    }

    private CountdownTaskRowItem BuildCountdownTaskRow(CountdownTask model)
    {
        var item = new CountdownTaskRowItem { Model = model };
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var enabled = new CheckBox { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        enabled.IsChecked = model.Enabled;
        enabled.Click += (_, _) => { if (_loaded) SaveCountdownTasksFromUI(); };
        Grid.SetColumn(enabled, 0);
        grid.Children.Add(enabled);
        item.EnabledCheck = enabled;

        var title = new TextBox { Text = model.Title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);
        item.TitleBox = title;

        var targetLocal = model.TargetTimeLocal;
        var dateRow = new StackPanel { Orientation = Orientation.Horizontal };
        var dp = new DatePicker { SelectedDate = targetLocal.Date, SelectedDateFormat = DatePickerFormat.Short, VerticalAlignment = VerticalAlignment.Center };
        var hour = new TextBox { Width = 36, Text = targetLocal.Hour.ToString(), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
        var colon1 = new TextBlock { Text = ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
        var min = new TextBox { Width = 36, Text = targetLocal.Minute.ToString(), VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
        dateRow.Children.Add(dp);
        dateRow.Children.Add(hour);
        dateRow.Children.Add(colon1);
        dateRow.Children.Add(min);
        Grid.SetColumn(dateRow, 2);
        grid.Children.Add(dateRow);
        item.DatePicker = dp;
        item.HourBox = hour;
        item.MinuteBox = min;
        item.SecondBox = new TextBox { Text = targetLocal.Second.ToString() };

        var dispMode = new ComboBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };
        dispMode.Items.Add(new ComboBoxItem { Content = "D天HH:MM:SS", Tag = "days" });
        dispMode.Items.Add(new ComboBoxItem { Content = "HH:MM:SS", Tag = "time" });
        foreach (var it in dispMode.Items)
            if (it is ComboBoxItem ci && ci.Tag?.ToString() == (string.IsNullOrEmpty(model.DisplayMode) ? "days" : model.DisplayMode))
                dispMode.SelectedItem = it;
        if (dispMode.SelectedIndex < 0) dispMode.SelectedIndex = 0;
        Grid.SetColumn(dispMode, 3);
        grid.Children.Add(dispMode);
        item.DisplayModeCombo = dispMode;

        var showTitle = new CheckBox { Content = "标题", VerticalAlignment = VerticalAlignment.Center };
        showTitle.IsChecked = true;
        Grid.SetColumn(showTitle, 4);
        grid.Children.Add(showTitle);
        item.ShowTitleCheck = showTitle;

        var del = new Button { Content = "×", Width = 22, Height = 22, Style = (Style)FindResource("SecondaryButton"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        del.Click += (_, _) =>
        {
            CountdownTasksListBox.ItemsSource = null;
            _countdownTaskRows.Remove(item);
            var l = _countdownTaskRows.Select(x => x.Row).ToList();
            CountdownTasksListBox.ItemsSource = l;
        };
        Grid.SetColumn(del, 5);
        grid.Children.Add(del);
        item.DeleteButton = del;

        item.Row = grid;
        _countdownTaskRows.Add(item);
        return item;
    }

    private void CountdownTaskAddButton_Click(object sender, RoutedEventArgs e)
    {
        var target = DateTime.Now.AddDays(1);
        var newTask = new CountdownTask
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "新倒计时",
            TargetTimeUtc = target.ToUniversalTime(),
            Enabled = true,
            DisplayMode = "days"
        };
        CountdownTasksListBox.ItemsSource = null;
        var row = BuildCountdownTaskRow(newTask);
        var list = _countdownTaskRows.Select(x => x.Row).ToList();
        CountdownTasksListBox.ItemsSource = list;
    }

    private void SaveCountdownTasksFromUI()
    {
        var newList = new List<CountdownTask>();
        foreach (var row in _countdownTaskRows)
        {
            var m = new CountdownTask
            {
                Id = row.Model.Id,
                Title = row.TitleBox.Text,
                Enabled = row.EnabledCheck.IsChecked == true,
                DisplayMode = (row.DisplayModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "days"
            };
            var date = row.DatePicker.SelectedDate ?? DateTime.Now.AddDays(1);
            int h = int.TryParse(row.HourBox.Text, out var v1) ? Math.Clamp(v1, 0, 23) : 0;
            int mi = int.TryParse(row.MinuteBox.Text, out var v2) ? Math.Clamp(v2, 0, 59) : 0;
            m.TargetTimeUtc = new DateTime(date.Year, date.Month, date.Day, h, mi, 0).ToUniversalTime();
            newList.Add(m);
        }
        Settings.CountdownTasks = newList;
    }

    // ========================================================================================
    // P1: 健康提醒 HealthReminder
    // ========================================================================================

    private class HealthReminderRowItem
    {
        public CheckBox EnabledCheck = null!;
        public TextBox NameBox = null!;
        public TextBox IntervalBox = null!;
        public Button DeleteButton = null!;
        public IntervalReminderItem Model = null!;
        public Grid Row = null!;
    }
    private readonly List<HealthReminderRowItem> _healthReminderRows = new();

    private void LoadHealthReminderSettings()
    {
        HealthReminderCheck.IsChecked = Settings.HealthReminderEnabled;
        HealthReminderPanel.Visibility = Settings.HealthReminderEnabled ? Visibility.Visible : Visibility.Collapsed;
        HealthWorkStartBox.Text = Settings.HealthReminderWorkStartHour.ToString();
        HealthWorkEndBox.Text = Settings.HealthReminderWorkEndHour.ToString();
        PopulateFontCombo(HealthReminderFontCombo, Settings.HealthReminderFontFamily);
        HealthReminderFontSizeSlider.Value = Settings.HealthReminderFontSize;
        HealthReminderFontSizeLabel.Text = Settings.HealthReminderFontSize.ToString("F0");
        HealthReminderColorBox.Text = Settings.HealthReminderFontColor;
        try { HealthReminderColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.HealthReminderFontColor)); } catch { }

        RenderHealthRemindersUI();
    }

    private void RenderHealthRemindersUI()
    {
        _healthReminderRows.Clear();
        HealthReminderListBox.ItemsSource = null;
        List<IntervalReminderItem> items;
        try { items = JsonSerializer.Deserialize<List<IntervalReminderItem>>(Settings.HealthRemindersJson) ?? new(); }
        catch { items = new(); }
        var list = new List<Grid>();
        if (items.Count == 0)
        {
            // 默认三条:喝水/站立/眼操
            items.Add(new IntervalReminderItem { Id = "water", Label = "喝水", IntervalMinutes = 60, Enabled = true });
            items.Add(new IntervalReminderItem { Id = "stand", Label = "站立", IntervalMinutes = 45, Enabled = true });
            items.Add(new IntervalReminderItem { Id = "eyes", Label = "眼保健操", IntervalMinutes = 20, Enabled = false });
        }
        foreach (var it in items) list.Add(BuildHealthReminderRow(it).Row);
        HealthReminderListBox.ItemsSource = list;
    }

    private HealthReminderRowItem BuildHealthReminderRow(IntervalReminderItem model)
    {
        var item = new HealthReminderRowItem { Model = model };
        var g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        cb.IsChecked = model.Enabled;
        Grid.SetColumn(cb, 0); g.Children.Add(cb);
        item.EnabledCheck = cb;

        var nb = new TextBox { Text = model.Label, Margin = new Thickness(2, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(nb, 1); g.Children.Add(nb);
        item.NameBox = nb;

        var ib = new TextBox { Text = model.IntervalMinutes.ToString(), Margin = new Thickness(10, 0, 2, 0), Width = 60, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(ib, 2); g.Children.Add(ib);
        item.IntervalBox = ib;

        var del = new Button { Content = "×", Width = 22, Height = 22, Style = (Style)FindResource("SecondaryButton"), VerticalAlignment = VerticalAlignment.Center };
        del.Click += (_, _) =>
        {
            HealthReminderListBox.ItemsSource = null;
            _healthReminderRows.Remove(item);
            HealthReminderListBox.ItemsSource = _healthReminderRows.Select(x => x.Row).ToList();
        };
        Grid.SetColumn(del, 3); g.Children.Add(del);
        item.DeleteButton = del;

        item.Row = g;
        _healthReminderRows.Add(item);
        return item;
    }

    private void HealthAddButton_Click(object sender, RoutedEventArgs e)
    {
        var m = new IntervalReminderItem { Id = Guid.NewGuid().ToString("N"), Label = "新提醒", IntervalMinutes = 30, Enabled = true };
        BuildHealthReminderRow(m);
        HealthReminderListBox.ItemsSource = null;
        HealthReminderListBox.ItemsSource = _healthReminderRows.Select(x => x.Row).ToList();
    }
    private void HealthPresetWater_Click(object sender, RoutedEventArgs e) => AddPresetHealth("喝水", 60);
    private void HealthPresetStand_Click(object sender, RoutedEventArgs e) => AddPresetHealth("站立", 45);
    private void HealthPresetEyes_Click(object sender, RoutedEventArgs e) => AddPresetHealth("眼保健操", 20);

    private void AddPresetHealth(string label, int interval)
    {
        if (_healthReminderRows.Any(x => x.NameBox.Text == label)) return;
        BuildHealthReminderRow(new IntervalReminderItem { Id = Guid.NewGuid().ToString("N"), Label = label, IntervalMinutes = interval, Enabled = true });
        HealthReminderListBox.ItemsSource = null;
        HealthReminderListBox.ItemsSource = _healthReminderRows.Select(x => x.Row).ToList();
    }

    private void SaveHealthRemindersFromUI()
    {
        var list = new List<IntervalReminderItem>();
        foreach (var r in _healthReminderRows)
        {
            list.Add(new IntervalReminderItem
            {
                Id = r.Model.Id,
                Label = r.NameBox.Text,
                IntervalMinutes = int.TryParse(r.IntervalBox.Text, out var v) ? Math.Clamp(v, 1, 1440) : 60,
                Enabled = r.EnabledCheck.IsChecked == true
            });
        }
        Settings.HealthRemindersJson = JsonSerializer.Serialize(list);
    }

    private void HealthReminderCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.HealthReminderEnabled = HealthReminderCheck.IsChecked == true;
        HealthReminderPanel.Visibility = Settings.HealthReminderEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HealthReminderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || HealthReminderFontSizeLabel == null) return;
        Settings.HealthReminderFontSize = e.NewValue;
        HealthReminderFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void HealthReminderColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(((SolidColorBrush)HealthReminderColorPreview.Background).Color.R, ((SolidColorBrush)HealthReminderColorPreview.Background).Color.G, ((SolidColorBrush)HealthReminderColorPreview.Background).Color.B) };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                HealthReminderColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
        catch { }
    }

    private void HealthReminderColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.HealthReminderFontColor = HealthReminderColorBox.Text;
        try { HealthReminderColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(HealthReminderColorBox.Text)); } catch { }
    }

    // ========================================================================================
    // P2: 番茄钟 Pomodoro
    // ========================================================================================

    private void LoadPomodoroSettings()
    {
        PomodoroCheck.IsChecked = Settings.PomodoroEnabled;
        PomodoroPanel.Visibility = Settings.PomodoroEnabled ? Visibility.Visible : Visibility.Collapsed;
        PomodoroFocusSlider.Value = Settings.PomodoroFocusMinutes;
        PomodoroFocusLabel.Text = Settings.PomodoroFocusMinutes.ToString();
        PomodoroShortSlider.Value = Settings.PomodoroShortBreakMinutes;
        PomodoroShortLabel.Text = Settings.PomodoroShortBreakMinutes.ToString();
        PomodoroLongSlider.Value = Settings.PomodoroLongBreakMinutes;
        PomodoroLongLabel.Text = Settings.PomodoroLongBreakMinutes.ToString();
        PomodoroIntervalSlider.Value = Settings.PomodoroLongBreakInterval;
        PomodoroIntervalLabel.Text = Settings.PomodoroLongBreakInterval.ToString();
        PomodoroAutoStartCheck.IsChecked = Settings.PomodoroAutoStart;
        PopulateFontCombo(PomodoroFontCombo, Settings.PomodoroFontFamily);
        PomodoroFontSizeSlider.Value = Settings.PomodoroFontSize;
        PomodoroFontSizeLabel.Text = Settings.PomodoroFontSize.ToString("F0");
        PomodoroColorBox.Text = Settings.PomodoroFontColor;
        try { PomodoroColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.PomodoroFontColor)); } catch { }
    }

    private void PomodoroCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.PomodoroEnabled = PomodoroCheck.IsChecked == true;
        PomodoroPanel.Visibility = Settings.PomodoroEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PomodoroFocusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || PomodoroFocusLabel == null) return;
        PomodoroFocusLabel.Text = e.NewValue.ToString("F0");
    }

    private void PomodoroShortSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || PomodoroShortLabel == null) return;
        PomodoroShortLabel.Text = e.NewValue.ToString("F0");
    }

    private void PomodoroLongSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || PomodoroLongLabel == null) return;
        PomodoroLongLabel.Text = e.NewValue.ToString("F0");
    }

    private void PomodoroIntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || PomodoroIntervalLabel == null) return;
        PomodoroIntervalLabel.Text = e.NewValue.ToString("F0");
    }

    private void PomodoroFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || PomodoroFontSizeLabel == null) return;
        PomodoroFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void PomodoroColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(((SolidColorBrush)PomodoroColorPreview.Background).Color.R, ((SolidColorBrush)PomodoroColorPreview.Background).Color.G, ((SolidColorBrush)PomodoroColorPreview.Background).Color.B) };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                PomodoroColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
        catch { }
    }

    private void PomodoroColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        try { PomodoroColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(PomodoroColorBox.Text)); } catch { }
    }

    // ========================================================================================
    // P3: 每日一言 DailyQuote
    // ========================================================================================

    private void LoadDailyQuoteSettings()
    {
        DailyQuoteCheck.IsChecked = Settings.DailyQuoteEnabled;
        DailyQuotePanel.Visibility = Settings.DailyQuoteEnabled ? Visibility.Visible : Visibility.Collapsed;
        DailyQuoteApiCheck.IsChecked = Settings.DailyQuoteApiEnabled;
        DailyQuoteApiBox.Text = Settings.DailyQuoteApiUrl;
        DailyQuoteSpeedSlider.Value = Settings.DailyQuoteSpeed;
        DailyQuoteSpeedLabel.Text = Settings.DailyQuoteSpeed.ToString("F0");
        PopulateFontCombo(DailyQuoteFontCombo, Settings.DailyQuoteFontFamily);
        DailyQuoteFontSizeSlider.Value = Settings.DailyQuoteFontSize;
        DailyQuoteFontSizeLabel.Text = Settings.DailyQuoteFontSize.ToString("F0");
        DailyQuoteColorBox.Text = Settings.DailyQuoteFontColor;
        try { DailyQuoteColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.DailyQuoteFontColor)); } catch { }
    }

    private void DailyQuoteCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.DailyQuoteEnabled = DailyQuoteCheck.IsChecked == true;
        DailyQuotePanel.Visibility = Settings.DailyQuoteEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DailyQuoteSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || DailyQuoteSpeedLabel == null) return;
        DailyQuoteSpeedLabel.Text = e.NewValue.ToString("F0");
    }

    private void DailyQuoteFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || DailyQuoteFontSizeLabel == null) return;
        DailyQuoteFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void DailyQuoteColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(((SolidColorBrush)DailyQuoteColorPreview.Background).Color.R, ((SolidColorBrush)DailyQuoteColorPreview.Background).Color.G, ((SolidColorBrush)DailyQuoteColorPreview.Background).Color.B) };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                DailyQuoteColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
        catch { }
    }

    private void DailyQuoteColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        try { DailyQuoteColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(DailyQuoteColorBox.Text)); } catch { }
    }

    // ========================================================================================
    // P4: 习惯打卡 HabitTracker
    // ========================================================================================

    private class HabitRowItem
    {
        public CheckBox EnabledCheck = null!;
        public TextBox NameBox = null!;
        public Button DeleteButton = null!;
        public HabitItem Model = null!;
        public Grid Row = null!;
    }
    private readonly List<HabitRowItem> _habitRows = new();

    private void LoadHabitTrackerSettings()
    {
        HabitTrackerCheck.IsChecked = Settings.HabitTrackerEnabled;
        HabitTrackerPanel.Visibility = Settings.HabitTrackerEnabled ? Visibility.Visible : Visibility.Collapsed;
        PopulateFontCombo(HabitTrackerFontCombo, Settings.HabitTrackerFontFamily);
        HabitTrackerFontSizeSlider.Value = Settings.HabitTrackerFontSize;
        HabitTrackerFontSizeLabel.Text = Settings.HabitTrackerFontSize.ToString("F0");
        HabitTrackerColorBox.Text = Settings.HabitTrackerFontColor;
        try { HabitTrackerColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Settings.HabitTrackerFontColor)); } catch { }

        RenderHabitsUI();
    }

    private void RenderHabitsUI()
    {
        _habitRows.Clear();
        HabitListBox.ItemsSource = null;
        List<HabitItem> items;
        try { items = JsonSerializer.Deserialize<List<HabitItem>>(Settings.HabitsJson) ?? new(); }
        catch { items = new(); }
        var list = new List<Grid>();
        if (items.Count == 0)
        {
            items.Add(new HabitItem { Id = "sport", Name = "运动", Enabled = true });
            items.Add(new HabitItem { Id = "read", Name = "阅读30分钟", Enabled = true });
            items.Add(new HabitItem { Id = "meditation", Name = "冥想", Enabled = false });
        }
        foreach (var it in items) list.Add(BuildHabitRow(it).Row);
        HabitListBox.ItemsSource = list;
    }

    private HabitRowItem BuildHabitRow(HabitItem model)
    {
        var item = new HabitRowItem { Model = model };
        var g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        cb.IsChecked = model.Enabled;
        Grid.SetColumn(cb, 0); g.Children.Add(cb);
        item.EnabledCheck = cb;

        var nb = new TextBox { Text = model.Name, Margin = new Thickness(2, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(nb, 1); g.Children.Add(nb);
        item.NameBox = nb;

        var del = new Button { Content = "×", Width = 22, Height = 22, Style = (Style)FindResource("SecondaryButton"), VerticalAlignment = VerticalAlignment.Center };
        del.Click += (_, _) =>
        {
            HabitListBox.ItemsSource = null;
            _habitRows.Remove(item);
            HabitListBox.ItemsSource = _habitRows.Select(x => x.Row).ToList();
        };
        Grid.SetColumn(del, 2); g.Children.Add(del);
        item.DeleteButton = del;

        item.Row = g;
        _habitRows.Add(item);
        return item;
    }

    private void HabitAddButton_Click(object sender, RoutedEventArgs e)
    {
        BuildHabitRow(new HabitItem { Id = Guid.NewGuid().ToString("N"), Name = "新习惯", Enabled = true });
        HabitListBox.ItemsSource = null;
        HabitListBox.ItemsSource = _habitRows.Select(x => x.Row).ToList();
    }

    private void SaveHabitsFromUI()
    {
        var list = new List<HabitItem>();
        foreach (var r in _habitRows)
        {
            list.Add(new HabitItem
            {
                Id = r.Model.Id,
                Name = r.NameBox.Text,
                Enabled = r.EnabledCheck.IsChecked == true
            });
        }
        Settings.HabitsJson = JsonSerializer.Serialize(list);
    }

    private void HabitTrackerCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.HabitTrackerEnabled = HabitTrackerCheck.IsChecked == true;
        HabitTrackerPanel.Visibility = Settings.HabitTrackerEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HabitTrackerFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded || HabitTrackerFontSizeLabel == null) return;
        HabitTrackerFontSizeLabel.Text = e.NewValue.ToString("F0");
    }

    private void HabitTrackerColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.Color.FromArgb(((SolidColorBrush)HabitTrackerColorPreview.Background).Color.R, ((SolidColorBrush)HabitTrackerColorPreview.Background).Color.G, ((SolidColorBrush)HabitTrackerColorPreview.Background).Color.B) };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                HabitTrackerColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
        catch { }
    }

    private void HabitTrackerColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        try { HabitTrackerColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(HabitTrackerColorBox.Text)); } catch { }
    }

    #endregion

    #region 独立悬浮窗口设置

    /// <summary>系统字体列表(延迟初始化)</summary>
    private static List<string>? _systemFonts;
    private static List<string> GetSystemFonts()
        => _systemFonts ??= System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();

    /// <summary>填充字体下拉框(悬浮窗口专用)</summary>
    private static void PopulateFloatFontCombo(ComboBox combo, string selected)
    {
        combo.Items.Clear();
        foreach (var font in GetSystemFonts())
        {
            var item = new ComboBoxItem { Content = font, Tag = font };
            combo.Items.Add(item);
            if (string.Equals(font, selected, StringComparison.OrdinalIgnoreCase))
                combo.SelectedItem = item;
        }
    }

    /// <summary>加载悬浮窗口配置到 UI</summary>
    private void LoadFloatWindowSettings()
    {
        var mgr = ComponentManager.Instance;

        // 时钟
        var clock = mgr.EnsureConfig("clock");
        CompClockEnabled.IsChecked = clock.Enabled;
        CompClockTopmost.IsChecked = clock.Topmost;
        CompClockLocked.IsChecked = clock.LockPosition;
        CompClock24Hour.IsChecked = clock.GetBool("use24hour", true);
        CompClockShowSec.IsChecked = clock.GetBool("showSeconds", true);
        PopulateFloatFontCombo(CompClockFontCombo, clock.FontFamily);
        CompClockFontSize.Value = clock.FontSize > 0 ? clock.FontSize : 56;
        CompClockColor.Text = clock.FontColor;
        CompClockShadow.IsChecked = clock.ShadowEnabled;
        CompClockShadowSize.Value = clock.ShadowSize;

        // 日历
        var cal = mgr.EnsureConfig("calendar");
        CompCalendarEnabled.IsChecked = cal.Enabled;
        CompCalendarTopmost.IsChecked = cal.Topmost;
        CompCalendarLocked.IsChecked = cal.LockPosition;
        CompCalendarLunar.IsChecked = cal.GetBool("showLunar", false);
        PopulateFloatFontCombo(CompCalendarFontCombo, cal.FontFamily);
        CompCalendarFontSize.Value = cal.FontSize > 0 ? cal.FontSize : 16;
        CompCalendarColor.Text = cal.FontColor;

        // 天气
        var weather = mgr.EnsureConfig("weather");
        CompWeatherEnabled.IsChecked = weather.Enabled;
        CompWeatherTopmost.IsChecked = weather.Topmost;
        CompWeatherLocked.IsChecked = weather.LockPosition;
        CompWeatherLat.Text = weather.GetDouble("latitude", 39.9).ToString();
        CompWeatherLon.Text = weather.GetDouble("longitude", 116.4).ToString();
        PopulateFloatFontCombo(CompWeatherFontCombo, weather.FontFamily);
        CompWeatherFontSize.Value = weather.FontSize > 0 ? weather.FontSize : 13;
        CompWeatherColor.Text = weather.FontColor;

        // 倒计时
        var cd = mgr.EnsureConfig("countdown");
        CompCountdownEnabled.IsChecked = cd.Enabled;
        CompCountdownTopmost.IsChecked = cd.Topmost;
        CompCountdownLocked.IsChecked = cd.LockPosition;
        CompCountdownRotation.Text = cd.GetInt("rotationSeconds", 10).ToString();
        PopulateFontCombo(CompCountdownFontCombo, cd.FontFamily);
        CompCountdownFontSize.Value = cd.FontSize > 0 ? cd.FontSize : 20;
        CompCountdownColor.Text = cd.FontColor;
        CompCountdownShadow.IsChecked = cd.ShadowEnabled;

        // 间隔提醒
        var rem = mgr.EnsureConfig("interval_reminder");
        CompReminderEnabled.IsChecked = rem.Enabled;
        CompReminderTopmost.IsChecked = rem.Topmost;
        CompReminderLocked.IsChecked = rem.LockPosition;
        CompReminderWorkStart.Text = rem.GetInt("workStartHour", 9).ToString();
        CompReminderWorkEnd.Text = rem.GetInt("workEndHour", 18).ToString();
        PopulateFloatFontCombo(CompReminderFontCombo, rem.FontFamily);
        CompReminderFontSize.Value = rem.FontSize > 0 ? rem.FontSize : 14;
        CompReminderColor.Text = rem.FontColor;

        // 番茄钟
        var pomo = mgr.EnsureConfig("pomodoro");
        CompPomodoroEnabled.IsChecked = pomo.Enabled;
        CompPomodoroTopmost.IsChecked = pomo.Topmost;
        CompPomodoroLocked.IsChecked = pomo.LockPosition;
        CompPomodoroFocus.Value = pomo.GetInt("focusMinutes", 25);
        CompPomodoroShort.Value = pomo.GetInt("shortBreakMinutes", 5);
        CompPomodoroLong.Value = pomo.GetInt("longBreakMinutes", 15);
        CompPomodoroInterval.Value = pomo.GetInt("longBreakInterval", 4);
        CompPomodoroAutoStart.IsChecked = pomo.GetBool("autoStart", false);
        PopulateFloatFontCombo(CompPomodoroFontCombo, pomo.FontFamily);
        CompPomodoroFontSize.Value = pomo.FontSize > 0 ? pomo.FontSize : 20;
        CompPomodoroColor.Text = pomo.FontColor;

        // 每日一言
        var daily = mgr.EnsureConfig("daily_sentence");
        CompDailyEnabled.IsChecked = daily.Enabled;
        CompDailyTopmost.IsChecked = daily.Topmost;
        CompDailyLocked.IsChecked = daily.LockPosition;
        CompDailyApi.IsChecked = daily.GetBool("apiEnabled", false);
        CompDailyApiUrl.Text = daily.GetString("apiUrl", "");
        CompDailySpeed.Value = daily.GetDouble("speed", 30.0);
        PopulateFloatFontCombo(CompDailyFontCombo, daily.FontFamily);
        CompDailyFontSize.Value = daily.FontSize > 0 ? daily.FontSize : 12;
        CompDailyColor.Text = daily.FontColor;

        // 习惯打卡
        var habit = mgr.EnsureConfig("habit_check");
        CompHabitEnabled.IsChecked = habit.Enabled;
        CompHabitTopmost.IsChecked = habit.Topmost;
        CompHabitLocked.IsChecked = habit.LockPosition;
        PopulateFloatFontCombo(CompHabitFontCombo, habit.FontFamily);
        CompHabitFontSize.Value = habit.FontSize > 0 ? habit.FontSize : 11;
        CompHabitColor.Text = habit.FontColor;
    }

    /// <summary>保存 UI 配置到 ComponentManager 并即时下发</summary>
    private void SaveFloatWindowSettings()
    {
        var mgr = ComponentManager.Instance;

        void Save(string id, Action<ComponentWindowConfig> apply)
        {
            var cfg = mgr.EnsureConfig(id);
            apply(cfg);
        }

        Save("clock", c =>
        {
            c.Enabled = CompClockEnabled.IsChecked == true;
            c.Topmost = CompClockTopmost.IsChecked == true;
            c.LockPosition = CompClockLocked.IsChecked == true;
            c.FontFamily = (CompClockFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Consolas";
            c.FontSize = CompClockFontSize.Value;
            c.FontColor = CompClockColor.Text;
            c.ShadowEnabled = CompClockShadow.IsChecked == true;
            c.ShadowSize = CompClockShadowSize.Value;
            c.Settings["use24hour"] = CompClock24Hour.IsChecked == true;
            c.Settings["showSeconds"] = CompClockShowSec.IsChecked == true;
        });

        Save("calendar", c =>
        {
            c.Enabled = CompCalendarEnabled.IsChecked == true;
            c.Topmost = CompCalendarTopmost.IsChecked == true;
            c.LockPosition = CompCalendarLocked.IsChecked == true;
            c.FontFamily = (CompCalendarFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei UI";
            c.FontSize = CompCalendarFontSize.Value;
            c.FontColor = CompCalendarColor.Text;
            c.Settings["showLunar"] = CompCalendarLunar.IsChecked == true;
        });

        Save("weather", c =>
        {
            c.Enabled = CompWeatherEnabled.IsChecked == true;
            c.Topmost = CompWeatherTopmost.IsChecked == true;
            c.LockPosition = CompWeatherLocked.IsChecked == true;
            c.FontFamily = (CompWeatherFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei UI";
            c.FontSize = CompWeatherFontSize.Value;
            c.FontColor = CompWeatherColor.Text;
            if (double.TryParse(CompWeatherLat.Text, out var lat)) c.Settings["latitude"] = lat;
            if (double.TryParse(CompWeatherLon.Text, out var lon)) c.Settings["longitude"] = lon;
        });

        Save("countdown", c =>
        {
            c.Enabled = CompCountdownEnabled.IsChecked == true;
            c.Topmost = CompCountdownTopmost.IsChecked == true;
            c.LockPosition = CompCountdownLocked.IsChecked == true;
            c.FontFamily = (CompCountdownFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Consolas";
            c.FontSize = CompCountdownFontSize.Value;
            c.FontColor = CompCountdownColor.Text;
            c.ShadowEnabled = CompCountdownShadow.IsChecked == true;
            if (int.TryParse(CompCountdownRotation.Text, out var rs)) c.Settings["rotationSeconds"] = rs;
        });

        Save("interval_reminder", c =>
        {
            c.Enabled = CompReminderEnabled.IsChecked == true;
            c.Topmost = CompReminderTopmost.IsChecked == true;
            c.LockPosition = CompReminderLocked.IsChecked == true;
            c.FontFamily = (CompReminderFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei UI";
            c.FontSize = CompReminderFontSize.Value;
            c.FontColor = CompReminderColor.Text;
            if (int.TryParse(CompReminderWorkStart.Text, out var ws)) c.Settings["workStartHour"] = ws;
            if (int.TryParse(CompReminderWorkEnd.Text, out var we)) c.Settings["workEndHour"] = we;
        });

        Save("pomodoro", c =>
        {
            c.Enabled = CompPomodoroEnabled.IsChecked == true;
            c.Topmost = CompPomodoroTopmost.IsChecked == true;
            c.LockPosition = CompPomodoroLocked.IsChecked == true;
            c.FontFamily = (CompPomodoroFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Consolas";
            c.FontSize = CompPomodoroFontSize.Value;
            c.FontColor = CompPomodoroColor.Text;
            c.Settings["focusMinutes"] = (int)CompPomodoroFocus.Value;
            c.Settings["shortBreakMinutes"] = (int)CompPomodoroShort.Value;
            c.Settings["longBreakMinutes"] = (int)CompPomodoroLong.Value;
            c.Settings["longBreakInterval"] = (int)CompPomodoroInterval.Value;
            c.Settings["autoStart"] = CompPomodoroAutoStart.IsChecked == true;
        });

        Save("daily_sentence", c =>
        {
            c.Enabled = CompDailyEnabled.IsChecked == true;
            c.Topmost = CompDailyTopmost.IsChecked == true;
            c.LockPosition = CompDailyLocked.IsChecked == true;
            c.FontFamily = (CompDailyFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei";
            c.FontSize = CompDailyFontSize.Value;
            c.FontColor = CompDailyColor.Text;
            c.Settings["apiEnabled"] = CompDailyApi.IsChecked == true;
            c.Settings["apiUrl"] = CompDailyApiUrl.Text;
            c.Settings["speed"] = CompDailySpeed.Value;
        });

        Save("habit_check", c =>
        {
            c.Enabled = CompHabitEnabled.IsChecked == true;
            c.Topmost = CompHabitTopmost.IsChecked == true;
            c.LockPosition = CompHabitLocked.IsChecked == true;
            c.FontFamily = (CompHabitFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Microsoft YaHei UI";
            c.FontSize = CompHabitFontSize.Value;
            c.FontColor = CompHabitColor.Text;
        });

        // 持久化并即时下发到窗口
        mgr.SaveConfig();

        // 根据启用状态显示/隐藏窗口
        var ids = new[] { "clock", "calendar", "weather", "countdown", "interval_reminder", "pomodoro", "daily_sentence", "habit_check" };
        foreach (var id in ids)
        {
            var cfg = mgr.GetConfig(id);
            if (cfg == null) continue;
            if (cfg.Enabled)
                mgr.Show(id);
            else
                mgr.Hide(id);
        }

        // 通知所有窗口刷新配置
        mgr.NotifyConfigChange();
    }

    private void ShowAllComponentsBtn_Click(object sender, RoutedEventArgs e)
    {
        ComponentManager.Instance.ShowAll();
        // 勾选所有启用复选框
        CompClockEnabled.IsChecked = true;
        CompCalendarEnabled.IsChecked = true;
        CompWeatherEnabled.IsChecked = true;
        CompCountdownEnabled.IsChecked = true;
        CompReminderEnabled.IsChecked = true;
        CompPomodoroEnabled.IsChecked = true;
        CompDailyEnabled.IsChecked = true;
        CompHabitEnabled.IsChecked = true;
    }

    private void HideAllComponentsBtn_Click(object sender, RoutedEventArgs e)
    {
        ComponentManager.Instance.HideAll();
        // 取消勾选所有启用复选框
        CompClockEnabled.IsChecked = false;
        CompCalendarEnabled.IsChecked = false;
        CompWeatherEnabled.IsChecked = false;
        CompCountdownEnabled.IsChecked = false;
        CompReminderEnabled.IsChecked = false;
        CompPomodoroEnabled.IsChecked = false;
        CompDailyEnabled.IsChecked = false;
        CompHabitEnabled.IsChecked = false;
    }

    #endregion
}
