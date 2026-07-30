using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopClock.Components;
using DesktopClock.Models;

namespace DesktopClock.Services;

public class LayoutEngine
{
    private Panel? _activeContainer;
    private Point _dragStart;
    private bool _isDragging;
    private bool _freeModeEnabled;
    private Grid? _selectedContainer;
    private ComponentRegistry? _registry;
    private LayoutConfig? _config;

    private static readonly SolidColorBrush SelectedBorderBrush = new(Color.FromArgb(180, 0, 122, 255));
    private static readonly SolidColorBrush DefaultBorderBrush = new(Colors.Transparent);
    private static readonly Thickness SelectedBorderThickness = new(2);
    private static readonly Thickness DefaultBorderThickness = new(0);

    public event Action? LayoutChanged;

    public bool IsFreeMode => _freeModeEnabled;

    public void BuildLayout(Panel container, ComponentRegistry registry, LayoutConfig config)
    {
        _activeContainer = container;
        _registry = registry;
        _config = config;
        container.Children.Clear();
        ClearSelection();

        if (config.Mode == "free")
            BuildFreeLayout(container, registry, config);
        else
            BuildStackLayout(container, registry, config);
    }

    private void BuildStackLayout(Panel container, ComponentRegistry registry, LayoutConfig config)
    {
        _freeModeEnabled = false;

        // 使用 StackPanel 垂直排列组件,让每个组件使用自身的尺寸显示
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 用 ZOrder 做排序参考,但最终以 ActiveComponents 为准,
        // 避免 ZOrder 默认值未包含某组件(如 analog_clock)时该组件不显示。
        var orderedIds = new List<string>();
        foreach (var id in config.ZOrder)
        {
            if (config.ActiveComponents.Contains(id))
                orderedIds.Add(id);
        }
        // ZOrder 未列出的激活组件追加在后面
        foreach (var id in config.ActiveComponents)
        {
            if (!orderedIds.Contains(id))
                orderedIds.Add(id);
        }

        // 根据 DatePosition 调整 date 组件位置(top=最前,bottom=最后)
        if (orderedIds.Contains("date"))
        {
            orderedIds.Remove("date");
            if (config.DatePosition == "bottom")
                orderedIds.Add("date");
            else
                orderedIds.Insert(0, "date");
        }

        foreach (var id in orderedIds)
        {
            var comp = registry.Get(id);
            if (comp == null) continue;

            // 断开旧父级,避免"已是另一个元素的逻辑子元素"异常
            DetachFromParent(comp.View as FrameworkElement);
            stackPanel.Children.Add(comp.View);
        }

        container.Children.Add(stackPanel);
    }

    /// <summary>
    /// 将 FrameworkElement 从其当前逻辑父元素断开(Parent→null)。
    /// 用于重建布局前,避免"指定的元素已经是另一个元素的逻辑子元素"异常。
    /// </summary>
    private static void DetachFromParent(FrameworkElement element)
    {
        if (element == null || element.Parent == null) return;
        switch (element.Parent)
        {
            case Panel p: p.Children.Remove(element); break;
            case ContentControl cc: cc.Content = null; break;
            case Decorator dec: dec.Child = null; break; // Border 继承自 Decorator,此处一并处理
        }
    }

    /// <summary>
    /// Safely adds a UIElement to a Panel, removing it from any existing parent first.
    /// </summary>
    private static void SafeAddChild(Panel panel, UIElement element)
    {
        if (element == null) return;
        if (element is FrameworkElement fe) DetachFromParent(fe);
        panel.Children.Add(element);
    }

    private void BuildFreeLayout(Panel container, ComponentRegistry registry, LayoutConfig config)
    {
        _freeModeEnabled = true;
        var canvas = new Canvas
        {
            Width = container.ActualWidth > 0 ? container.ActualWidth : 500,
            Height = container.ActualHeight > 0 ? container.ActualHeight : 120,
            Background = Brushes.Transparent
        };

        if (container.Background == null || container.Background == Brushes.Transparent)
            container.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

        canvas.MouseDown += Canvas_MouseDown;

        foreach (var id in config.ActiveComponents)
        {
            AddComponentToCanvas(canvas, registry, config, id);
        }

        container.Children.Add(canvas);
    }

    private void AddComponentToCanvas(Canvas canvas, ComponentRegistry registry, LayoutConfig config, string id)
    {
        var comp = registry.Get(id);
        FrameworkElement? externalView = null;
        string displayName;

        if (comp != null)
        {
            displayName = comp.DisplayName;
        }
        else
        {
            externalView = registry.GetExternal(id);
            if (externalView == null) return;
            displayName = id;
        }

        var pos = config.Positions.GetValueOrDefault(id, new ComponentPosition
        {
            X = 20,
            Y = 20 + GetTakenSlots(canvas) * 20,
            Width = double.NaN,
            Height = double.NaN
        });

        // Selection border wrapper
        var cornerRadius = new CornerRadius(6);
        var selectionBorder = new Border
        {
            BorderBrush = DefaultBorderBrush,
            BorderThickness = DefaultBorderThickness,
            CornerRadius = cornerRadius,
            Padding = new Thickness(2),
            Child = CreateDragContainer(id, externalView ?? comp!.View as FrameworkElement ?? new TextBlock(), displayName, pos, config)
        };

        Canvas.SetLeft(selectionBorder, pos.X);
        Canvas.SetTop(selectionBorder, pos.Y);
        canvas.Children.Add(selectionBorder);
    }

    private Grid CreateDragContainer(string id, FrameworkElement view, string displayName, ComponentPosition pos, LayoutConfig config)
    {
        var dragContainer = new Grid { Tag = id };
        dragContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dragContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Drag handle with right-click menu
        var handle = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Height = 20,
            Cursor = Cursors.SizeAll,
            ToolTip = displayName,
            Child = new TextBlock
            {
                Text = "≡ " + displayName,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            }
        };

        handle.MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                SelectComponent(dragContainer);
                BeginDrag(dragContainer, e, config);
            }
        };

        Grid.SetRow(handle, 0);

        var contentBorder = new Border
        {
            Background = Brushes.Transparent,
            MinWidth = 60,
            MinHeight = 40
        };
        // 重建布局时 view 可能仍挂在旧布局树的 Border 上,先断开再设 Child
        DetachFromParent(view);
        contentBorder.Child = view;

        // Click on content to select
        contentBorder.MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                SelectComponent(dragContainer);
        };

        Grid.SetRow(contentBorder, 1);

        // Context menu on drag container
        var contextMenu = new ContextMenu();
        var lockItem = new MenuItem { Header = pos.Locked ? "解锁位置" : "锁定位置" };
        lockItem.Click += (s, e) =>
        {
            pos.Locked = !pos.Locked;
            lockItem.Header = pos.Locked ? "解锁位置" : "锁定位置";
            handle.Cursor = pos.Locked ? Cursors.Arrow : Cursors.SizeAll;
            if (pos.Locked) ClearSelection();
        };

        var resetItem = new MenuItem { Header = "重置大小" };
        resetItem.Click += (s, e) =>
        {
            dragContainer.Width = double.NaN;
            dragContainer.Height = double.NaN;
            if (config.Positions.ContainsKey(id))
            {
                config.Positions[id].Width = double.NaN;
                config.Positions[id].Height = double.NaN;
            }
        };

        var removeItem = new MenuItem { Header = "移除" };
        removeItem.Click += (s, e) => RemoveComponentFromCanvas(dragContainer, id, config);

        contextMenu.Items.Add(lockItem);
        contextMenu.Items.Add(resetItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(removeItem);
        dragContainer.ContextMenu = contextMenu;

        dragContainer.Children.Add(handle);
        dragContainer.Children.Add(contentBorder);

        if (!double.IsNaN(pos.Width))
            dragContainer.Width = pos.Width;
        if (!double.IsNaN(pos.Height))
            dragContainer.Height = pos.Height;

        if (pos.Locked)
            handle.Cursor = Cursors.Arrow;

        return dragContainer;
    }

    private int GetTakenSlots(Canvas canvas)
    {
        return canvas.Children.Count;
    }

    private void SelectComponent(Grid dragContainer)
    {
        ClearSelection();
        _selectedContainer = dragContainer;

        if (dragContainer.Parent is Border border)
        {
            border.BorderBrush = SelectedBorderBrush;
            border.BorderThickness = SelectedBorderThickness;
        }
    }

    private void ClearSelection()
    {
        if (_selectedContainer != null && _selectedContainer.Parent is Border oldBorder)
        {
            oldBorder.BorderBrush = DefaultBorderBrush;
            oldBorder.BorderThickness = DefaultBorderThickness;
        }
        _selectedContainer = null;
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click on canvas background to add component
        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
        {
            if (_registry == null || _config == null) return;

            var available = _registry.GetAll()
                .Where(c => !_config.ActiveComponents.Contains(c.Id))
                .ToList();

            if (available.Count == 0) return;

            var first = available[0];
            _config.ActiveComponents.Add(first.Id);

            var canvas = sender as Canvas;
            if (canvas == null) return;

            AddComponentToCanvas(canvas, _registry, _config, first.Id);
            LayoutChanged?.Invoke();
            e.Handled = true;
            return;
        }

        ClearSelection();
    }

    private void RemoveComponentFromCanvas(Grid dragContainer, string id, LayoutConfig config)
    {
        config.ActiveComponents.Remove(id);

        if (dragContainer.Parent is Border border && border.Parent is Canvas canvas)
        {
            canvas.Children.Remove(border);
        }

        if (_selectedContainer == dragContainer)
            _selectedContainer = null;

        LayoutChanged?.Invoke();
    }

    private void BeginDrag(UIElement element, MouseButtonEventArgs e, LayoutConfig config)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        // Check if locked
        if (element is Grid g && g.Tag is string id && config.Positions.TryGetValue(id, out var pos) && pos.Locked)
            return;

        _dragStart = e.GetPosition(_activeContainer);
        _isDragging = true;
        element.CaptureMouse();
        e.Handled = true;
    }

    public void HandleMouseMove(Point position)
    {
        if (!_isDragging || _activeContainer == null) return;

        var element = Mouse.Captured as UIElement;
        if (element == null) return;

        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null && parent is not Canvas && parent is not Border)
            parent = VisualTreeHelper.GetParent(parent);

        if (parent is Border border && border.Parent is Canvas)
        {
            var delta = position - _dragStart;
            double newLeft = Canvas.GetLeft(border) + delta.X;
            double newTop = Canvas.GetTop(border) + delta.Y;
            Canvas.SetLeft(border, newLeft);
            Canvas.SetTop(border, newTop);
            _dragStart = position;
        }
    }

    public void HandleMouseUp(Point position, LayoutConfig config)
    {
        if (!_isDragging)
        {
            // Handle right-click deselection on canvas background
            return;
        }
        _isDragging = false;

        var element = Mouse.Captured as UIElement;
        if (element != null)
        {
            element.ReleaseMouseCapture();
        }
    }

    public void HandleKeyDown(Key key, LayoutConfig config)
    {
        if (key == Key.Delete && _selectedContainer != null)
        {
            var id = _selectedContainer.Tag?.ToString();
            if (id != null)
            {
                RemoveComponentFromCanvas(_selectedContainer, id, config);
            }
        }
    }

    public void SaveFreePositions(Panel container, LayoutConfig config)
    {
        if (container.Children.Count == 0) return;
        var canvas = container.Children[0] as Canvas;
        if (canvas == null) return;

        foreach (var child in canvas.Children)
        {
            if (child is Border border && border.Child is Grid dragContainer)
            {
                var id = dragContainer.Tag?.ToString();
                if (id == null) continue;

                config.Positions[id] = new ComponentPosition
                {
                    X = Canvas.GetLeft(border),
                    Y = Canvas.GetTop(border),
                    Width = dragContainer.Width,
                    Height = dragContainer.Height,
                    Locked = config.Positions.TryGetValue(id, out var existing) && existing.Locked
                };
            }
        }
    }
}
