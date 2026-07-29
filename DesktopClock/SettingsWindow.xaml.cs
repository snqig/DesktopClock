using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopClock;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }

    private bool _loaded;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        Settings = new AppSettings
        {
            FontSize = settings.FontSize,
            BackgroundOpacity = settings.BackgroundOpacity,
            FontColor = settings.FontColor,
            FontFamily = settings.FontFamily,
            ShowDate = settings.ShowDate,
            DateFontFamily = settings.DateFontFamily,
            DateFontSize = settings.DateFontSize,
            DateColor = settings.DateColor,
            DatePosition = settings.DatePosition,

            Use24Hour = settings.Use24Hour,
            ShowSeconds = settings.ShowSeconds,
            DisplayMode = settings.DisplayMode,
            BackgroundType = settings.BackgroundType,
            GradientStartColor = settings.GradientStartColor,
            GradientEndColor = settings.GradientEndColor,
            GradientAngle = settings.GradientAngle,
            BorderColor = settings.BorderColor,
            BorderThickness = settings.BorderThickness,
            ChimeEnabled = settings.ChimeEnabled,
            WorldClockEnabled = settings.WorldClockEnabled,
            WorldClockTimeZone = settings.WorldClockTimeZone,
            HotkeyHide = settings.HotkeyHide,
            Language = settings.Language,
            ThemePreset = settings.ThemePreset,
            SnapToEdge = settings.SnapToEdge,
            AutoStart = settings.AutoStart,
            LockPosition = settings.LockPosition,
            ClickThrough = settings.ClickThrough
        };

        PopulateTimeZones();

        foreach (var item in HourFormatCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.Use24Hour.ToString())
                HourFormatCombo.SelectedItem = item;
        ShowSecondsCheck.IsChecked = Settings.ShowSeconds;
        foreach (var item in DisplayModeCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.DisplayMode)
                DisplayModeCombo.SelectedItem = item;
        WorldClockCheck.IsChecked = Settings.WorldClockEnabled;
        SelectTimeZone(Settings.WorldClockTimeZone);
        ChimeCheck.IsChecked = Settings.ChimeEnabled;

        FontFamilyBox.Text = Settings.FontFamily;
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

        ShowDateCheck.IsChecked = Settings.ShowDate;
        DateFontFamilyBox.Text = Settings.DateFontFamily;
        DateFontSizeSlider.Value = Settings.DateFontSize;
        DateFontSizeLabel.Text = Settings.DateFontSize.ToString("F0");
        DateColorBox.Text = Settings.DateColor;
        UpdateDateColorPreview();
        foreach (var item in DatePositionCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.DatePosition)
                DatePositionCombo.SelectedItem = item;

        ClickThroughCheck.IsChecked = Settings.ClickThrough;
        SnapToEdgeCheck.IsChecked = Settings.SnapToEdge;
        LockPositionCheck.IsChecked = Settings.LockPosition;
        HotkeyBox.Text = Settings.HotkeyHide;

        AutoStartCheck.IsChecked = Settings.AutoStart;
        foreach (var item in LanguageCombo.Items)
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == Settings.Language)
                LanguageCombo.SelectedItem = item;

        TimeZoneRow.Visibility = Settings.WorldClockEnabled ? Visibility.Visible : Visibility.Collapsed;

        _loaded = true;
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

    private void ActivateSegment(Border active, ScrollViewer panel)
    {
        var segments = new[] { DisplaySegment, AppearanceSegment, DateSegment2, FeaturesSegment, SystemSegment };
        var panels = new ScrollViewer[] { DisplayPanel, AppearancePanel, DatePanel2, FeaturesPanel, SystemPanel };

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

    private void FontFamilyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.FontFamily = FontFamilyBox.Text;
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

    private void ShowDateCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.ShowDate = ShowDateCheck.IsChecked == true;
    }

    private void DateFontFamilyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.DateFontFamily = DateFontFamilyBox.Text;
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

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Settings.Use24Hour = (HourFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "true";
        Settings.ShowSeconds = ShowSecondsCheck.IsChecked == true;
        Settings.DisplayMode = (DisplayModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "digital";
        Settings.WorldClockEnabled = WorldClockCheck.IsChecked == true;
        Settings.WorldClockTimeZone = (TimeZoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "China Standard Time";
        Settings.ChimeEnabled = ChimeCheck.IsChecked == true;

        Settings.FontFamily = FontFamilyBox.Text;
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

        Settings.ShowDate = ShowDateCheck.IsChecked == true;
        Settings.DateFontFamily = DateFontFamilyBox.Text;
        Settings.DateFontSize = DateFontSizeSlider.Value;
        Settings.DateColor = DateColorBox.Text;
        Settings.DatePosition = (DatePositionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "top";

        Settings.ClickThrough = ClickThroughCheck.IsChecked == true;
        Settings.SnapToEdge = SnapToEdgeCheck.IsChecked == true;
        Settings.LockPosition = LockPositionCheck.IsChecked == true;
        Settings.HotkeyHide = HotkeyBox.Text;

        Settings.AutoStart = AutoStartCheck.IsChecked == true;
        Settings.Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "zh";

        Settings.Save();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
