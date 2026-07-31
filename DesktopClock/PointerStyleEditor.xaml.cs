using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopClock.Models;
using DesktopClock.Services;
using Microsoft.Win32;

namespace DesktopClock;

/// <summary>
/// 指针样式编辑器:可视化编辑三根指针(时/分/秒)的 PNG 素材、锚点、缩放、染色、阴影、发光、透明度。
/// 三列布局:方案列表 / 参数编辑 TabControl / 实时预览 + 元信息。
/// </summary>
public partial class PointerStyleEditor : Window
{
    private readonly PointerStyleManager _manager;
    private PointerSet? _current;
    private bool _isLoading;
    private bool _ready;

    private readonly DispatcherTimer _previewTimer;
    private readonly ObservableCollection<PointerSet> _filteredList = new();

    // 预览渲染缓存
    private Image? _previewHourImage;
    private Image? _previewMinuteImage;
    private Image? _previewSecondImage;
    private Line? _previewHourLine;
    private Line? _previewMinuteLine;
    private Line? _previewSecondLine;

    /// <summary>应用方案回调:MainWindow 注册后用于刷新 AnalogClockSkin。</summary>
    public Action<PointerSet>? OnApply;

    public PointerStyleEditor(PointerStyleManager manager)
    {
        _manager = manager;
        InitializeComponent();

        PointerSetList.ItemsSource = _filteredList;
        RefreshList();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _previewTimer.Tick += (_, _) => UpdatePreviewAngles();
        _previewTimer.Start();
        this.Closed += (_, _) => _previewTimer.Stop();

        _ready = true;
        if (PointerSetList.Items.Count > 0)
            PointerSetList.SelectedIndex = 0;
        else
        {
            LoadCurrentToControls();
            RebuildPreview();
        }
    }

    // ============================================================
    //  方案列表 / 分类筛选
    // ============================================================

    private void RefreshList()
    {
        string? category = CategoryFilter != null && CategoryFilter.SelectedIndex > 0
            ? (CategoryFilter.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? CategoryFilter.SelectedItem?.ToString()
            : null;

        var selectedId = _current?.Id;
        _filteredList.Clear();
        foreach (var s in _manager.Sets)
        {
            if (category != null && s.Category != category) continue;
            _filteredList.Add(s);
        }

        if (selectedId != null)
        {
            var target = _filteredList.FirstOrDefault(s => s.Id == selectedId);
            if (target != null) PointerSetList.SelectedItem = target;
        }
    }

    private void SelectById(string? id)
    {
        if (id == null) return;
        var target = _filteredList.FirstOrDefault(s => s.Id == id);
        if (target != null) PointerSetList.SelectedItem = target;
    }

    private void PointerSetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _current = PointerSetList.SelectedItem as PointerSet;
        LoadCurrentToControls();
        RebuildPreview();
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || !_ready) return;
        RefreshList();
    }

    // ============================================================
    //  新建 / 复制 / 删除 / 收藏
    // ============================================================

    private void BtnNewSet_Click(object sender, RoutedEventArgs e)
    {
        var set = _manager.CreateNew();
        RefreshList();
        SelectById(set.Id);
    }

    private void BtnDuplicateSet_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        ApplyAllFromControls();
        var copy = _manager.Duplicate(_current.Id);
        RefreshList();
        SelectById(copy?.Id);
    }

    private void BtnDeleteSet_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var id = _current.Id;
        _manager.Delete(id);
        _current = null;
        RefreshList();
        if (PointerSetList.Items.Count > 0)
            PointerSetList.SelectedIndex = 0;
        else
        {
            LoadCurrentToControls();
            RebuildPreview();
        }
    }

    private void BtnFavoriteSet_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var id = _current.Id;
        _manager.ToggleFavorite(id);
        RefreshList();
        SelectById(id);
    }

    // ============================================================
    //  浏览图片
    // ============================================================

    private void BrowseHourImage_Click(object sender, RoutedEventArgs e) => BrowseImage(HourImagePath, "hour");
    private void BrowseMinuteImage_Click(object sender, RoutedEventArgs e) => BrowseImage(MinuteImagePath, "minute");
    private void BrowseSecondImage_Click(object sender, RoutedEventArgs e) => BrowseImage(SecondImagePath, "second");

    private void BrowseImage(TextBox pathBox, string which)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "PNG 图片|*.png|所有图片|*.png;*.jpg;*.jpeg",
            Title = "选择指针图片"
        };
        if (ofd.ShowDialog() != true) return;
        pathBox.Text = ofd.FileName;
        if (_current == null) return;
        ApplyPointerFromControls(which);
        RebuildPreview();
    }

    // ============================================================
    //  时针参数变更
    // ============================================================

    private void HourAnchorX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        HourAnchorXValue.Text = HourAnchorX.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("hour");
        RebuildPreview();
    }

    private void HourAnchorY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        HourAnchorYValue.Text = HourAnchorY.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("hour");
        RebuildPreview();
    }

    private void HourScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        HourScaleValue.Text = HourScale.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("hour");
        RebuildPreview();
    }

    private void HourColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateColorPreview(HourColorPreview, HourColorBox.Text);
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("hour");
        RebuildPreview();
    }

    private void HourColorPreview_MouseDown(object sender, MouseButtonEventArgs e) => PickColor(HourColorBox);

    private void HourShadow_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || !_ready || _current == null) return;
        ApplyPointerFromControls("hour");
        RebuildPreview();
    }

    private void HourGlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        HourGlowValue.Text = HourGlow.Value.ToString("F1");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("hour");
        RebuildPreview();
    }

    private void HourOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        HourOpacityValue.Text = HourOpacity.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("hour");
        RebuildPreview();
    }

    // ============================================================
    //  分针参数变更
    // ============================================================

    private void MinuteAnchorX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        MinuteAnchorXValue.Text = MinuteAnchorX.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("minute");
        RebuildPreview();
    }

    private void MinuteAnchorY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        MinuteAnchorYValue.Text = MinuteAnchorY.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("minute");
        RebuildPreview();
    }

    private void MinuteScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        MinuteScaleValue.Text = MinuteScale.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("minute");
        RebuildPreview();
    }

    private void MinuteColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateColorPreview(MinuteColorPreview, MinuteColorBox.Text);
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("minute");
        RebuildPreview();
    }

    private void MinuteColorPreview_MouseDown(object sender, MouseButtonEventArgs e) => PickColor(MinuteColorBox);

    private void MinuteShadow_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || !_ready || _current == null) return;
        ApplyPointerFromControls("minute");
        RebuildPreview();
    }

    private void MinuteGlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        MinuteGlowValue.Text = MinuteGlow.Value.ToString("F1");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("minute");
        RebuildPreview();
    }

    private void MinuteOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        MinuteOpacityValue.Text = MinuteOpacity.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("minute");
        RebuildPreview();
    }

    // ============================================================
    //  秒针参数变更
    // ============================================================

    private void SecondAnchorX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        SecondAnchorXValue.Text = SecondAnchorX.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("second");
        RebuildPreview();
    }

    private void SecondAnchorY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        SecondAnchorYValue.Text = SecondAnchorY.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("second");
        RebuildPreview();
    }

    private void SecondScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        SecondScaleValue.Text = SecondScale.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("second");
        RebuildPreview();
    }

    private void SecondColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateColorPreview(SecondColorPreview, SecondColorBox.Text);
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("second");
        RebuildPreview();
    }

    private void SecondColorPreview_MouseDown(object sender, MouseButtonEventArgs e) => PickColor(SecondColorBox);

    private void SecondShadow_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || !_ready || _current == null) return;
        ApplyPointerFromControls("second");
        RebuildPreview();
    }

    private void SecondGlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        SecondGlowValue.Text = SecondGlow.Value.ToString("F1");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("second");
        RebuildPreview();
    }

    private void SecondOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        SecondOpacityValue.Text = SecondOpacity.Value.ToString("F2");
        if (_isLoading || _current == null) return;
        ApplyPointerFromControls("second");
        RebuildPreview();
    }

    // ============================================================
    //  方案元信息变更
    // ============================================================

    private void SetNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || !_ready || _current == null) return;
        _current.Name = SetNameBox.Text;
    }

    private void SetCategoryBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || !_ready || _current == null) return;
        _current.Category = SetCategoryBox.Text;
    }

    private void SetNoteBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || !_ready || _current == null) return;
        _current.Note = SetNoteBox.Text;
    }

    // ============================================================
    //  底部按钮:应用 / 另存为新方案 / 关闭
    // ============================================================

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        ApplyAllFromControls();
        _manager.Update(_current);
        OnApply?.Invoke(_current);
    }

    private void BtnSaveAsNew_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        ApplyAllFromControls();
        var name = ShowInputDialog("另存为新方案", "请输入新方案名称:", _current.Name + " 副本");
        if (string.IsNullOrWhiteSpace(name)) return;
        var set = _manager.CreateMix(name, _current.Category, _current.HourStyle, _current.MinuteStyle, _current.SecondStyle);
        RefreshList();
        SelectById(set.Id);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    // ============================================================
    //  控件 ↔ 模型 绑定辅助
    // ============================================================

    private void LoadCurrentToControls()
    {
        _isLoading = true;
        try
        {
            if (_current == null)
            {
                SetNameBox.Text = string.Empty;
                SetCategoryBox.Text = string.Empty;
                SetNoteBox.Text = string.Empty;
                LoadStyle(new SinglePointerStyle(), HourImagePath, HourAnchorX, HourAnchorXValue, HourAnchorY, HourAnchorYValue, HourScale, HourScaleValue, HourColorBox, HourColorPreview, HourShadow, HourGlow, HourGlowValue, HourOpacity, HourOpacityValue);
                LoadStyle(new SinglePointerStyle(), MinuteImagePath, MinuteAnchorX, MinuteAnchorXValue, MinuteAnchorY, MinuteAnchorYValue, MinuteScale, MinuteScaleValue, MinuteColorBox, MinuteColorPreview, MinuteShadow, MinuteGlow, MinuteGlowValue, MinuteOpacity, MinuteOpacityValue);
                LoadStyle(new SinglePointerStyle(), SecondImagePath, SecondAnchorX, SecondAnchorXValue, SecondAnchorY, SecondAnchorYValue, SecondScale, SecondScaleValue, SecondColorBox, SecondColorPreview, SecondShadow, SecondGlow, SecondGlowValue, SecondOpacity, SecondOpacityValue);
                return;
            }

            LoadStyle(_current.HourStyle, HourImagePath, HourAnchorX, HourAnchorXValue, HourAnchorY, HourAnchorYValue, HourScale, HourScaleValue, HourColorBox, HourColorPreview, HourShadow, HourGlow, HourGlowValue, HourOpacity, HourOpacityValue);
            LoadStyle(_current.MinuteStyle, MinuteImagePath, MinuteAnchorX, MinuteAnchorXValue, MinuteAnchorY, MinuteAnchorYValue, MinuteScale, MinuteScaleValue, MinuteColorBox, MinuteColorPreview, MinuteShadow, MinuteGlow, MinuteGlowValue, MinuteOpacity, MinuteOpacityValue);
            LoadStyle(_current.SecondStyle, SecondImagePath, SecondAnchorX, SecondAnchorXValue, SecondAnchorY, SecondAnchorYValue, SecondScale, SecondScaleValue, SecondColorBox, SecondColorPreview, SecondShadow, SecondGlow, SecondGlowValue, SecondOpacity, SecondOpacityValue);

            SetNameBox.Text = _current.Name;
            SetCategoryBox.Text = _current.Category;
            SetNoteBox.Text = _current.Note;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static void LoadStyle(
        SinglePointerStyle s,
        TextBox path, Slider ax, TextBlock axv, Slider ay, TextBlock ayv,
        Slider sc, TextBlock scv, TextBox color, ContentControl preview,
        CheckBox shadow, Slider glow, TextBlock glowv, Slider op, TextBlock opv)
    {
        path.Text = s.ImagePath ?? string.Empty;
        ax.Value = Math.Clamp(s.RotationCenterX, 0, 1);
        axv.Text = ax.Value.ToString("F2");
        ay.Value = Math.Clamp(s.RotationCenterY, 0, 1);
        ayv.Text = ay.Value.ToString("F2");
        sc.Value = Math.Clamp(s.Scale, 0.1, 3.0);
        scv.Text = sc.Value.ToString("F2");
        color.Text = s.ColorTint ?? string.Empty;
        UpdateColorPreview(preview, s.ColorTint);
        shadow.IsChecked = s.ShadowEnabled;
        glow.Value = Math.Clamp(s.GlowIntensity, 0, 10);
        glowv.Text = glow.Value.ToString("F1");
        op.Value = Math.Clamp(s.Opacity, 0, 1);
        opv.Text = op.Value.ToString("F2");
    }

    private void ApplyAllFromControls()
    {
        if (_current == null) return;
        ApplyPointerFromControls("hour");
        ApplyPointerFromControls("minute");
        ApplyPointerFromControls("second");
        _current.Name = SetNameBox.Text;
        _current.Category = SetCategoryBox.Text;
        _current.Note = SetNoteBox.Text;
    }

    private void ApplyPointerFromControls(string which)
    {
        if (_current == null) return;
        switch (which)
        {
            case "hour":
                ApplyStyle(_current.HourStyle, HourImagePath, HourAnchorX, HourAnchorY, HourScale, HourColorBox, HourShadow, HourGlow, HourOpacity);
                break;
            case "minute":
                ApplyStyle(_current.MinuteStyle, MinuteImagePath, MinuteAnchorX, MinuteAnchorY, MinuteScale, MinuteColorBox, MinuteShadow, MinuteGlow, MinuteOpacity);
                break;
            case "second":
                ApplyStyle(_current.SecondStyle, SecondImagePath, SecondAnchorX, SecondAnchorY, SecondScale, SecondColorBox, SecondShadow, SecondGlow, SecondOpacity);
                break;
        }
    }

    private static void ApplyStyle(
        SinglePointerStyle s,
        TextBox path, Slider ax, Slider ay, Slider sc, TextBox color,
        CheckBox shadow, Slider glow, Slider op)
    {
        s.ImagePath = path.Text ?? string.Empty;
        s.RotationCenterX = ax.Value;
        s.RotationCenterY = ay.Value;
        s.Scale = sc.Value;
        s.ColorTint = color.Text ?? string.Empty;
        s.ShadowEnabled = shadow.IsChecked == true;
        s.GlowIntensity = glow.Value;
        s.Opacity = op.Value;
    }

    // ============================================================
    //  颜色预览 + ColorDialog
    // ============================================================

    private static void UpdateColorPreview(ContentControl preview, string? hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                preview.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                return;
            }
            var color = (Color)ColorConverter.ConvertFromString(hex);
            preview.Background = new SolidColorBrush(color);
        }
        catch
        {
            preview.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
        }
    }

    private void PickColor(TextBox colorBox)
    {
        try
        {
            using var cd = new System.Windows.Forms.ColorDialog();
            if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = cd.Color;
                colorBox.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }
        catch
        {
            // 忽略 ColorDialog 异常
        }
    }

    // ============================================================
    //  预览渲染
    // ============================================================

    private void RebuildPreview()
    {
        if (PreviewCanvas == null) return;
        PreviewCanvas.Children.Clear();
        _previewHourImage = null;
        _previewMinuteImage = null;
        _previewSecondImage = null;
        _previewHourLine = null;
        _previewMinuteLine = null;
        _previewSecondLine = null;

        if (_current == null) return;

        const double cx = 120, cy = 120, baseSize = 120;
        var (hourAngle, minAngle, secAngle) = ComputeAngles();

        _previewHourImage = PointerRenderer.CreateOrUpdate(PreviewCanvas, _previewHourImage, _current.HourStyle, cx, cy, hourAngle, baseSize);
        _previewMinuteImage = PointerRenderer.CreateOrUpdate(PreviewCanvas, _previewMinuteImage, _current.MinuteStyle, cx, cy, minAngle, baseSize);
        _previewSecondImage = PointerRenderer.CreateOrUpdate(PreviewCanvas, _previewSecondImage, _current.SecondStyle, cx, cy, secAngle, baseSize);

        if (_previewHourImage == null)
            _previewHourLine = AddFallbackLine(cx, cy, 55, 5, Color.FromRgb(0x3a, 0x2a, 0x1a), _current.HourStyle, hourAngle);
        if (_previewMinuteImage == null)
            _previewMinuteLine = AddFallbackLine(cx, cy, 80, 3, Color.FromRgb(0x2a, 0x2a, 0x2a), _current.MinuteStyle, minAngle);
        if (_previewSecondImage == null)
            _previewSecondLine = AddFallbackLine(cx, cy, 95, 1.5, Color.FromRgb(0xcc, 0x33, 0x33), _current.SecondStyle, secAngle);
    }

    private void UpdatePreviewAngles()
    {
        if (_current == null || PreviewCanvas == null) return;
        var (hourAngle, minAngle, secAngle) = ComputeAngles();

        if (_previewHourImage != null) PointerRenderer.UpdateAngle(_previewHourImage, hourAngle);
        else if (_previewHourLine?.RenderTransform is RotateTransform hr) hr.Angle = hourAngle;

        if (_previewMinuteImage != null) PointerRenderer.UpdateAngle(_previewMinuteImage, minAngle);
        else if (_previewMinuteLine?.RenderTransform is RotateTransform mr) mr.Angle = minAngle;

        if (_previewSecondImage != null) PointerRenderer.UpdateAngle(_previewSecondImage, secAngle);
        else if (_previewSecondLine?.RenderTransform is RotateTransform sr) sr.Angle = secAngle;
    }

    private static (double hour, double minute, double second) ComputeAngles()
    {
        var now = DateTime.Now;
        double ms = now.Millisecond / 1000.0;
        double sec = now.Second + ms;
        double min = now.Minute + sec / 60.0;
        double hour = (now.Hour % 12) + min / 60.0;
        return (hour * 30.0, min * 6.0, sec * 6.0);
    }

    private Line AddFallbackLine(double cx, double cy, double length, double thickness, Color defaultColor, SinglePointerStyle style, double angle)
    {
        var color = !string.IsNullOrWhiteSpace(style.ColorTint)
            ? TryParseColor(style.ColorTint, defaultColor)
            : defaultColor;

        var line = new Line
        {
            X1 = cx,
            Y1 = cy,
            X2 = cx,
            Y2 = cy - length,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = Math.Clamp(style.Opacity, 0, 1)
        };
        line.RenderTransform = new RotateTransform { Angle = angle, CenterX = cx, CenterY = cy };
        PreviewCanvas.Children.Add(line);
        return line;
    }

    private static Color TryParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    // ============================================================
    //  简易输入对话框(另存为新方案用)
    // ============================================================

    private static string? ShowInputDialog(string title, string prompt, string defaultValue)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 360,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e))
        };

        var panel = new StackPanel { Margin = new Thickness(18) };
        var label = new TextBlock
        {
            Text = prompt,
            Foreground = Brushes.White,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var box = new TextBox
        {
            Text = defaultValue,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x3a)),
            Padding = new Thickness(6, 4, 6, 4)
        };

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var ok = new Button { Content = "确定", Width = 78, Height = 30, Margin = new Thickness(8, 0, 0, 0) };
        var cancel = new Button { Content = "取消", Width = 78, Height = 30, Margin = new Thickness(8, 0, 0, 0) };
        btnPanel.Children.Add(ok);
        btnPanel.Children.Add(cancel);

        panel.Children.Add(label);
        panel.Children.Add(box);
        panel.Children.Add(btnPanel);
        dlg.Content = panel;

        ok.Click += (_, _) => { dlg.DialogResult = true; dlg.Close(); };
        cancel.Click += (_, _) => { dlg.DialogResult = false; dlg.Close(); };

        box.Focus();
        box.SelectAll();

        return dlg.ShowDialog() == true ? box.Text : null;
    }
}
