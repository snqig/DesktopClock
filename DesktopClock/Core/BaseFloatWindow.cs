using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopClock.Core;

/// <summary>
/// 悬浮窗口基类：所有独立桌面组件的统一容器。
/// 无边框、透明、不在任务栏显示、点击不抢占焦点(WS_EX_NOACTIVATE)。
/// 提供拖拽、位置锁定、右键菜单、位置持久化等通用能力。
/// </summary>
public abstract class BaseFloatWindow : Window
{
    private IntPtr _handle;
    private bool _positionLoaded;
    private bool _isClosing;

    /// <summary>组件唯一标识(由子类在构造函数中设置)</summary>
    public string ComponentId { get; protected set; } = "";

    /// <summary>是否锁定位置(锁定后不可拖拽)</summary>
    public bool IsLocked { get; set; }

    /// <summary>是否置顶</summary>
    public bool IsTopmost { get; set; } = true;

    /// <summary>窗口透明度 0~1</summary>
    public double WindowOpacity { get; set; } = 1.0;

    /// <summary>WPF XAML 需要无参构造函数</summary>
    protected BaseFloatWindow()
    {

        // 无边框透明窗口
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // 拖拽支持
        MouseLeftButtonDown += OnDragMove;

        // 右键菜单
        MouseRightButtonDown += OnRightClick;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _handle = new WindowInteropHelper(this).Handle;

        // WS_EX_NOACTIVATE: 点击不抢占焦点
        // WS_EX_TOOLWINDOW: 不在 Alt+Tab 中显示
        int ex = NativeMethods.GetWindowLong(_handle, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_LAYERED;
        NativeMethods.SetWindowLong(_handle, NativeMethods.GWL_EXSTYLE, ex);

        // 应用透明度与置顶
        NativeMethods.SetLayeredAlpha(_handle, (byte)Math.Clamp((int)(WindowOpacity * 255), 0, 255));
        NativeMethods.SetTopmost(_handle, IsTopmost);

        // 加载持久化位置
        if (!_positionLoaded)
        {
            LoadFromConfig();
            _positionLoaded = true;
        }
    }

    /// <summary>拖拽移动(未锁定时)</summary>
    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !IsLocked)
        {
            try { DragMove(); } catch { }
        }
    }

    /// <summary>右键菜单</summary>
    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        var miLock = new MenuItem { Header = IsLocked ? "解锁位置" : "锁定位置" };
        miLock.Click += (_, _) => { IsLocked = !IsLocked; SavePosition(); };
        menu.Items.Add(miLock);

        var miTopmost = new MenuItem { Header = IsTopmost ? "取消置顶" : "窗口置顶", IsCheckable = true, IsChecked = IsTopmost };
        miTopmost.Click += (_, _) =>
        {
            IsTopmost = !IsTopmost;
            NativeMethods.SetTopmost(_handle, IsTopmost);
            SavePosition();
        };
        menu.Items.Add(miTopmost);

        var miSettings = new MenuItem { Header = "组件设置" };
        miSettings.Click += (_, _) => OpenComponentSettings();
        menu.Items.Add(miSettings);

        menu.Items.Add(new Separator());

        var miClose = new MenuItem { Header = "关闭组件" };
        miClose.Click += (_, _) =>
        {
            _isClosing = true;
            Hide();
            SavePosition();
            OnComponentClosed();
        };
        menu.Items.Add(miClose);

        menu.IsOpen = true;
    }

    /// <summary>从配置加载位置与状态</summary>
    public abstract void LoadFromConfig();

    /// <summary>保存位置与状态到配置</summary>
    public abstract void SavePosition();

    /// <summary>组件被关闭时的回调(通知 ComponentManager)</summary>
    protected virtual void OnComponentClosed() { }

    /// <summary>打开该组件的设置面板</summary>
    protected virtual void OpenComponentSettings()
    {
        // 默认打开全局设置窗口
        ComponentManager.Instance.OpenGlobalSettings();
    }

    /// <summary>应用配置变更(由 ComponentManager 在设置修改后调用)</summary>
    public virtual void ApplyConfigChange() { }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosing)
        {
            // 阻止真正的关闭，改为隐藏
            e.Cancel = true;
            Hide();
            SavePosition();
            OnComponentClosed();
        }
        else
        {
            SavePosition();
            base.OnClosing(e);
        }
    }

    /// <summary>确保窗口在屏幕可见范围内(防止拖出屏幕找不到)</summary>
    protected void ClampToScreen()
    {
        var screen = SystemParameters.WorkArea;
        if (Left < -Width + 40) Left = -Width + 40;
        if (Top < 0) Top = 0;
        if (Left > screen.Width - 40) Left = screen.Width - 40;
        if (Top > screen.Height - 40) Top = screen.Height - 40;
    }
}
