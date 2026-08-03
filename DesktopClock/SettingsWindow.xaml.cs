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

        // 全局设置加载
        AutoStartCheck.IsChecked = Settings.AutoStart;
        foreach (var item in LanguageCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.Language)
                LanguageCombo.SelectedItem = item;
        HotkeyBox.Text = Settings.HotkeyHide;

        // Plugins
        PluginListBox.ItemsSource = _plugins;
        RefreshPluginsList();
        PluginListBox.Visibility = _plugins.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PluginStatusText.Text = _plugins.Count > 0 ? $"已加载 {_plugins.Count} 个插件" : "未检测到任何插件";

        LoadCountdownSettings();

        // === 组件设置加载 ===
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

    private void PluginCheck_Changed(object sender, RoutedEventArgs e)
    {
        // Handled via data binding directly to PluginItem.Enabled
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Services.Logger.Information("[SettingsWindow] OkButton_Click entered");

        Settings.HotkeyHide = HotkeyBox.Text;

        Settings.AutoStart = AutoStartCheck.IsChecked == true;
        // 同步注册表写入,使开关即时生效(下次开机自启/取消)
        try { App.SetAutoStart(Settings.AutoStart); }
        catch { /* 注册表写入失败不阻塞设置保存 */ }
        Settings.Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "zh";
        // 即时应用语言切换(zh / en / ja),所有 DynamicResource 绑定自动刷新
        try { I18n.Apply(Settings.Language); }
        catch { /* 语言切换失败不阻塞设置保存 */ }

        // Save plugin enabled states
        foreach (var pi in _plugins)
            Settings.Plugins[pi.Id] = pi.Enabled;

        // 保存倒计时挂件配置
        SaveCountdownSettings();

        // 保存独立悬浮窗口配置
        SaveFloatWindowSettings();

        Services.Logger.Information($"[SettingsWindow] Before Save: DisplayMode={Settings.DisplayMode}, FontSize={Settings.FontSize}");
        Settings.Save();
        Services.Logger.Information("[SettingsWindow] After Save, settings.json written");
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

        Settings.CountdownFontFamily = (CountdownFontFamilyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
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

    #region P0 多任务倒计时 CountdownTask

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

        // 时钟(窗口级行为读 ComponentWindowConfig,外观样式读 AppSettings 与"外观"面板同步)
        var clock = mgr.EnsureConfig("clock");
        CompClockEnabled.IsChecked = clock.Enabled;
        CompClockTopmost.IsChecked = clock.Topmost;
        CompClockLocked.IsChecked = clock.LockPosition;

        // 时间格式与显示模式(来自 AppSettings)
        foreach (var item in CompClockHourFormat.Items)
            if (item is ComboBoxItem ci && string.Equals(ci.Tag?.ToString(), Settings.Use24Hour.ToString(), StringComparison.OrdinalIgnoreCase))
                CompClockHourFormat.SelectedItem = item;
        CompClockShowSec.IsChecked = Settings.ShowSeconds;
        foreach (var item in CompClockDisplayMode.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.DisplayMode)
                CompClockDisplayMode.SelectedItem = item;

        // 文字外观(来自 AppSettings)
        PopulateFloatFontCombo(CompClockFontCombo, Settings.FontFamily);
        CompClockFontSize.Value = Settings.FontSize > 0 ? Settings.FontSize : 56;
        CompClockColor.Text = Settings.FontColor;

        // 背景(来自 AppSettings)
        CompClockBgOpacity.Value = Settings.BackgroundOpacity * 100;
        CompClockBgOpacityLabel.Text = $"{(int)(Settings.BackgroundOpacity * 100)}%";
        foreach (var item in CompClockBgType.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.BackgroundType)
                CompClockBgType.SelectedItem = item;
        CompClockGradStart.Text = Settings.GradientStartColor;
        CompClockGradEnd.Text = Settings.GradientEndColor;
        CompClockGradAngle.Value = Settings.GradientAngle;
        CompClockGradAngleLabel.Text = Settings.GradientAngle.ToString("F0");

        // 边框(来自 AppSettings)
        CompClockBorderColor.Text = Settings.BorderColor;
        CompClockBorderThickness.Value = Settings.BorderThickness;
        CompClockBorderThicknessLabel.Text = Settings.BorderThickness.ToString("F0");

        // 主题预设(来自 AppSettings)
        foreach (var item in CompClockThemePreset.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.ThemePreset)
                CompClockThemePreset.SelectedItem = item;

        // 阴影(来自 ComponentWindowConfig)
        CompClockShadow.IsChecked = clock.ShadowEnabled;
        CompClockShadowSize.Value = clock.ShadowSize;

        // 日期显示(来自 AppSettings)
        CompClockShowDate.IsChecked = Settings.ShowDate;
        PopulateFloatFontCombo(CompClockDateFontCombo, Settings.DateFontFamily);
        CompClockDateFontSize.Value = Settings.DateFontSize > 0 ? Settings.DateFontSize : 16;
        CompClockDateFontSizeLabel.Text = Settings.DateFontSize.ToString("F0");
        CompClockDateColor.Text = Settings.DateColor;

        // 世界时钟与整点报时(来自 AppSettings)
        CompClockWorldClock.IsChecked = Settings.WorldClockEnabled;
        CompClockTimeZoneRow.Visibility = Settings.WorldClockEnabled ? Visibility.Visible : Visibility.Collapsed;
        CompClockTimeZoneCombo.Items.Clear();
        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            var item = new ComboBoxItem { Content = tz.DisplayName, Tag = tz.Id };
            CompClockTimeZoneCombo.Items.Add(item);
            if (string.Equals(tz.Id, Settings.WorldClockTimeZone, StringComparison.OrdinalIgnoreCase))
                CompClockTimeZoneCombo.SelectedItem = item;
        }
        CompClockWorldClock.Checked += (_, _) => CompClockTimeZoneRow.Visibility = Visibility.Visible;
        CompClockWorldClock.Unchecked += (_, _) => CompClockTimeZoneRow.Visibility = Visibility.Collapsed;
        CompClockChime.IsChecked = Settings.ChimeEnabled;

        // 背景图片(来自 AppSettings)
        CompClockSkinBgEnable.IsChecked = Settings.SkinBackgroundEnabled;
        CompClockSkinBgPanel.Visibility = Settings.SkinBackgroundEnabled ? Visibility.Visible : Visibility.Collapsed;
        CompClockSkinBgPath.Text = Settings.SkinBackgroundPath;
        CompClockSkinBgOpacity.Value = Settings.SkinBackgroundOpacity * 100;
        CompClockSkinBgOpacityLabel.Text = $"{(int)(Settings.SkinBackgroundOpacity * 100)}%";
        CompClockSkinBgBlur.Value = Settings.SkinBackgroundBlur;
        CompClockSkinBgBlurLabel.Text = Settings.SkinBackgroundBlur.ToString("F0");
        foreach (var item in CompClockSkinBgStretch.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.SkinBackgroundStretch)
                CompClockSkinBgStretch.SelectedItem = item;
        CompClockSkinBgEnable.Checked += (_, _) => CompClockSkinBgPanel.Visibility = Visibility.Visible;
        CompClockSkinBgEnable.Unchecked += (_, _) => CompClockSkinBgPanel.Visibility = Visibility.Collapsed;

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
        CompCountdownTopmost.IsChecked = cd.Topmost;
        CompCountdownLocked.IsChecked = cd.LockPosition;

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

    private void CompClockSkinBgBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
            Title = "选择背景图片"
        };
        if (dialog.ShowDialog() == true)
            CompClockSkinBgPath.Text = dialog.FileName;
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

            // 显示模式 → AppSettings(统一来源:CompClockDisplayMode)
            var mode = (CompClockDisplayMode.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            Services.Logger.Information($"[SettingsWindow] Save clock: CompClockDisplayMode.SelectedItem.Tag={mode}");
            Settings.DisplayMode = mode ?? "digital";

            // 时间格式与文字外观 → AppSettings
            Settings.Use24Hour = (CompClockHourFormat.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "true";
            Settings.ShowSeconds = CompClockShowSec.IsChecked == true;
            Settings.FontFamily = (CompClockFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
            Settings.FontSize = CompClockFontSize.Value;
            Settings.FontColor = CompClockColor.Text;

            // 背景 → AppSettings
            Settings.BackgroundOpacity = CompClockBgOpacity.Value / 100.0;
            Settings.BackgroundType = (CompClockBgType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "solid";
            Settings.GradientStartColor = CompClockGradStart.Text;
            Settings.GradientEndColor = CompClockGradEnd.Text;
            Settings.GradientAngle = CompClockGradAngle.Value;

            // 边框与主题 → AppSettings
            Settings.BorderColor = CompClockBorderColor.Text;
            Settings.BorderThickness = CompClockBorderThickness.Value;
            Settings.ThemePreset = (CompClockThemePreset.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default";

            // 日期显示 → AppSettings
            Settings.ShowDate = CompClockShowDate.IsChecked == true;
            Settings.DateFontFamily = (CompClockDateFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
            Settings.DateFontSize = CompClockDateFontSize.Value;
            Settings.DateColor = CompClockDateColor.Text;

            // 世界时钟与整点报时 → AppSettings
            Settings.WorldClockEnabled = CompClockWorldClock.IsChecked == true;
            Settings.WorldClockTimeZone = (CompClockTimeZoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "China Standard Time";
            Settings.ChimeEnabled = CompClockChime.IsChecked == true;

            // 背景图片 → AppSettings
            Settings.SkinBackgroundEnabled = CompClockSkinBgEnable.IsChecked == true;
            Settings.SkinBackgroundPath = CompClockSkinBgPath.Text;
            Settings.SkinBackgroundOpacity = CompClockSkinBgOpacity.Value / 100.0;
            Settings.SkinBackgroundBlur = CompClockSkinBgBlur.Value;
            Settings.SkinBackgroundStretch = (CompClockSkinBgStretch.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "UniformToFill";

            // 阴影保留在组件配置
            c.ShadowEnabled = CompClockShadow.IsChecked == true;
            c.ShadowSize = CompClockShadowSize.Value;
        });

        Save("calendar", c =>
        {
            c.Enabled = CompCalendarEnabled.IsChecked == true;
            c.Topmost = CompCalendarTopmost.IsChecked == true;
            c.LockPosition = CompCalendarLocked.IsChecked == true;
            c.FontFamily = (CompCalendarFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
            c.FontSize = CompCalendarFontSize.Value;
            c.FontColor = CompCalendarColor.Text;
            c.Settings["showLunar"] = CompCalendarLunar.IsChecked == true;
        });

        Save("weather", c =>
        {
            c.Enabled = CompWeatherEnabled.IsChecked == true;
            c.Topmost = CompWeatherTopmost.IsChecked == true;
            c.LockPosition = CompWeatherLocked.IsChecked == true;
            c.FontFamily = (CompWeatherFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
            c.FontSize = CompWeatherFontSize.Value;
            c.FontColor = CompWeatherColor.Text;
            if (double.TryParse(CompWeatherLat.Text, out var lat)) c.Settings["latitude"] = lat;
            if (double.TryParse(CompWeatherLon.Text, out var lon)) c.Settings["longitude"] = lon;
        });

        Save("countdown", c =>
        {
            // CountdownEnabledCheck 同时作为业务开关和组件窗口开关
            c.Enabled = CountdownEnabledCheck.IsChecked == true;
            c.Topmost = CompCountdownTopmost.IsChecked == true;
            c.LockPosition = CompCountdownLocked.IsChecked == true;
        });

        Save("interval_reminder", c =>
        {
            c.Enabled = CompReminderEnabled.IsChecked == true;
            c.Topmost = CompReminderTopmost.IsChecked == true;
            c.LockPosition = CompReminderLocked.IsChecked == true;
            c.FontFamily = (CompReminderFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
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
            c.FontFamily = (CompDailyFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
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
            c.FontFamily = (CompHabitFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DS-Digital";
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
        CompReminderEnabled.IsChecked = false;
        CompPomodoroEnabled.IsChecked = false;
        CompDailyEnabled.IsChecked = false;
        CompHabitEnabled.IsChecked = false;
    }

    #endregion
}
