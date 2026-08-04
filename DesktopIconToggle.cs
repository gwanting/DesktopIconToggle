using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
    private const string AppName = "DesktopIconToggle";
    private const string AppTitle = "桌面图标开关";

    [STAThread]
    private static void Main()
    {
        // 鼠标钩子坐标和桌面窗口坐标必须处于同一 DPI 坐标空间。
        // 必须在创建任何 WinForms/Win32 窗口之前调用。
        NativeMethods.EnablePerMonitorDpiAwareness();

        bool createdNew;
        using (Mutex mutex = new Mutex(true, @"Local\DesktopIconToggle.9D323887", out createdNew))
        {
            if (!createdNew)
            {
                MessageBox.Show("桌面图标开关已经在运行。", AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (TrayApplicationContext context = new TrayApplicationContext())
            {
                Application.Run(context);
            }
        }
    }

    internal static string ExecutablePath
    {
        get { return Process.GetCurrentProcess().MainModule.FileName; }
    }

    internal static string RunValueName
    {
        get { return AppName; }
    }
}

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem toggleItem;
    private readonly ToolStripMenuItem startupItem;
    private readonly DesktopMouseWatcher watcher;

    internal TrayApplicationContext()
    {
        toggleItem = new ToolStripMenuItem("隐藏桌面图标", null, OnToggleClicked);
        startupItem = new ToolStripMenuItem("开机自动启动", null, OnStartupClicked);
        startupItem.Checked = StartupManager.IsEnabled();

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add(toggleItem);
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, OnExitClicked));

        trayIcon = new NotifyIcon();
        trayIcon.Icon = Icon.ExtractAssociatedIcon(Program.ExecutablePath);
        trayIcon.Text = "桌面图标开关：双击桌面空白处切换图标";
        trayIcon.ContextMenuStrip = menu;
        trayIcon.Visible = true;
        trayIcon.DoubleClick += OnTrayDoubleClick;

        watcher = new DesktopMouseWatcher();
        watcher.ToggleRequested += OnToggleRequested;
        watcher.Start();
        UpdateToggleText();

        trayIcon.ShowBalloonTip(2500, "桌面图标开关已启动",
            "双击桌面空白处，可隐藏或显示全部桌面图标。",
            ToolTipIcon.Info);
    }

    private void OnToggleRequested(object sender, EventArgs e)
    {
        ToggleIcons();
    }

    private void OnToggleClicked(object sender, EventArgs e)
    {
        ToggleIcons();
    }

    private void OnTrayDoubleClick(object sender, EventArgs e)
    {
        ToggleIcons();
    }

    private void ToggleIcons()
    {
        if (!DesktopIcons.Toggle())
        {
            trayIcon.ShowBalloonTip(2000, "切换失败",
                "暂时找不到 Windows 桌面窗口，请稍后再试。", ToolTipIcon.Warning);
        }
        UpdateToggleText();
    }

    private void UpdateToggleText()
    {
        toggleItem.Text = DesktopIcons.AreVisible() ? "隐藏桌面图标" : "显示桌面图标";
    }

    private void OnStartupClicked(object sender, EventArgs e)
    {
        bool enable = !startupItem.Checked;
        try
        {
            StartupManager.SetEnabled(enable);
            startupItem.Checked = enable;
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法修改开机启动设置。\n\n" + ex.Message,
                "桌面图标开关", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        watcher.Dispose();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        base.ExitThreadCore();
    }
}

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    internal static bool IsEnabled()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
        {
            return key != null && key.GetValue(Program.RunValueName) != null;
        }
    }

    internal static void SetEnabled(bool enabled)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (key == null)
                throw new InvalidOperationException("无法打开当前用户的启动项注册表。 ");

            if (enabled)
                key.SetValue(Program.RunValueName, "\"" + Program.ExecutablePath + "\"");
            else
                key.DeleteValue(Program.RunValueName, false);
        }
    }
}

internal sealed class DesktopMouseWatcher : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;

    private NativeMethods.LowLevelMouseProc callback;
    private IntPtr hookHandle;
    private uint lastClickTime;
    private NativeMethods.POINT lastClickPoint;
    private bool lastClickWasDesktopBlank;

    internal event EventHandler ToggleRequested;

    internal void Start()
    {
        callback = HookCallback;
        using (Process process = Process.GetCurrentProcess())
        using (ProcessModule module = process.MainModule)
        {
            IntPtr moduleHandle = NativeMethods.GetModuleHandle(module.ModuleName);
            hookHandle = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, callback, moduleHandle, 0);
        }

        if (hookHandle == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == WM_LBUTTONDOWN)
        {
            NativeMethods.MSLLHOOKSTRUCT data =
                (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
            HandleLeftButtonDown(data.pt, data.time);
        }
        return NativeMethods.CallNextHookEx(hookHandle, code, wParam, lParam);
    }

    private void HandleLeftButtonDown(NativeMethods.POINT point, uint time)
    {
        bool blank = DesktopIcons.IsBlankDesktopPoint(point);
        uint elapsed = time - lastClickTime;
        Size doubleClickSize = SystemInformation.DoubleClickSize;
        bool closeEnough = Math.Abs(point.X - lastClickPoint.X) <= doubleClickSize.Width / 2 &&
                           Math.Abs(point.Y - lastClickPoint.Y) <= doubleClickSize.Height / 2;

        if (blank && lastClickWasDesktopBlank && elapsed <= NativeMethods.GetDoubleClickTime() && closeEnough)
        {
            lastClickTime = 0;
            lastClickWasDesktopBlank = false;
            EventHandler handler = ToggleRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
            return;
        }

        lastClickTime = time;
        lastClickPoint = point;
        lastClickWasDesktopBlank = blank;
    }

    public void Dispose()
    {
        if (hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(hookHandle);
            hookHandle = IntPtr.Zero;
        }
        callback = null;
    }
}

internal static class DesktopIcons
{
    private const int WM_COMMAND = 0x0111;
    private const int TOGGLE_DESKTOP_ICONS = 0x7402;

    internal static bool Toggle()
    {
        bool visible;
        if (ShellFolderView.TryGetIconsVisible(out visible) &&
            ShellFolderView.TrySetIconsVisible(!visible))
            return true;

        // 旧版 Shell 的后备路径。
        IntPtr defView = FindDesktopDefView();
        if (defView == IntPtr.Zero)
            return false;

        // 使用资源管理器自己的“显示桌面图标”命令。直接 ShowWindow 会被
        // Windows 10/11 的 Explorer 自动纠正，表现为闪烁后立刻重新显示。
        NativeMethods.SendMessage(defView, WM_COMMAND,
            new IntPtr(TOGGLE_DESKTOP_ICONS), IntPtr.Zero);
        return true;
    }

    internal static bool AreVisible()
    {
        bool visible;
        if (ShellFolderView.TryGetIconsVisible(out visible))
            return visible;

        IntPtr listView = FindDesktopListView();
        return listView != IntPtr.Zero && NativeMethods.IsWindowVisible(listView);
    }

    internal static bool IsBlankDesktopPoint(NativeMethods.POINT screenPoint)
    {
        IntPtr listView = FindDesktopListView();
        if (listView == IntPtr.Zero)
            return false;

        IntPtr hitWindow = NativeMethods.WindowFromPoint(screenPoint);
        if (NativeMethods.IsWindowVisible(listView))
        {
            if (hitWindow != listView && !NativeMethods.IsChild(listView, hitWindow))
                return false;

            bool isIcon;
            if (!NativeMethods.TryIsAccessibleListItemAtPoint(screenPoint, out isIcon))
                return false;
            return !isIcon;
        }

        // 图标控件隐藏后，点击会落到它后面的 SHELLDLL_DefView、WorkerW 或 Progman。
        IntPtr desktopHost = NativeMethods.GetAncestor(listView, 2);
        IntPtr current = hitWindow;
        while (current != IntPtr.Zero)
        {
            if (current == desktopHost)
                return true;

            string className = NativeMethods.GetClassNameString(current);
            if (className == "SHELLDLL_DefView")
                return true;
            if ((className == "WorkerW" || className == "Progman") &&
                NativeMethods.FindWindowEx(current, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                return true;

            current = NativeMethods.GetParent(current);
        }
        return false;
    }

    private static IntPtr FindDesktopListView()
    {
        IntPtr defView = FindDesktopDefView();
        if (defView == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr listView = NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
        if (listView == IntPtr.Zero)
            listView = NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
        return listView;
    }

    private static IntPtr FindDesktopDefView()
    {
        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        IntPtr defView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView != IntPtr.Zero)
            return defView;

        IntPtr found = IntPtr.Zero;
        NativeMethods.EnumWindows(delegate(IntPtr window, IntPtr parameter)
        {
            IntPtr view = NativeMethods.FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view != IntPtr.Zero)
            {
                found = view;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}

// 通过 Windows 支持的 Shell 文件夹视图接口切换 FWF_NOICONS。
// 不再依赖 Explorer 内部的 SysListView32 显示状态。
internal static class ShellFolderView
{
    private const int CSIDL_DESKTOP = 0;
    private const int SWC_DESKTOP = 8;
    private const int SWFO_NEEDDISPATCH = 1;
    private const uint FWF_NOICONS = 0x00001000;

    private static readonly Guid CLSID_ShellWindows =
        new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static readonly Guid SID_STopLevelBrowser =
        new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IID_IShellBrowser =
        new Guid("000214E2-0000-0000-C000-000000000046");
    private static readonly Guid IID_IFolderView2 =
        new Guid("1AF3A467-214F-4298-908E-06B03E0B39F9");

    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid service, ref Guid riid, out IntPtr result);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryActiveShellViewDelegate(IntPtr browser, out IntPtr shellView);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetCurrentFolderFlagsDelegate(IntPtr folderView, out uint flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetCurrentFolderFlagsDelegate(IntPtr folderView, uint mask, uint flags);

    internal static bool TryGetIconsVisible(out bool visible)
    {
        visible = true;
        IntPtr folderView = IntPtr.Zero;
        try
        {
            folderView = GetDesktopFolderView2();
            if (folderView == IntPtr.Zero)
                return false;

            IntPtr method = GetVTableMethod(folderView, 25);
            GetCurrentFolderFlagsDelegate getFlags =
                (GetCurrentFolderFlagsDelegate)Marshal.GetDelegateForFunctionPointer(
                    method, typeof(GetCurrentFolderFlagsDelegate));
            uint flags;
            int result = getFlags(folderView, out flags);
            if (result < 0)
                return false;

            visible = (flags & FWF_NOICONS) == 0;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (folderView != IntPtr.Zero)
                Marshal.Release(folderView);
        }
    }

    internal static bool TrySetIconsVisible(bool visible)
    {
        IntPtr folderView = IntPtr.Zero;
        try
        {
            folderView = GetDesktopFolderView2();
            if (folderView == IntPtr.Zero)
                return false;

            IntPtr method = GetVTableMethod(folderView, 24);
            SetCurrentFolderFlagsDelegate setFlags =
                (SetCurrentFolderFlagsDelegate)Marshal.GetDelegateForFunctionPointer(
                    method, typeof(SetCurrentFolderFlagsDelegate));
            uint flags = visible ? 0u : FWF_NOICONS;
            return setFlags(folderView, FWF_NOICONS, flags) >= 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (folderView != IntPtr.Zero)
                Marshal.Release(folderView);
        }
    }

    private static IntPtr GetDesktopFolderView2()
    {
        object shellWindows = null;
        object desktopDispatch = null;
        IntPtr browser = IntPtr.Zero;
        IntPtr shellView = IntPtr.Zero;
        IntPtr folderView = IntPtr.Zero;
        try
        {
            Type type = Type.GetTypeFromCLSID(CLSID_ShellWindows, true);
            shellWindows = Activator.CreateInstance(type);

            object[] arguments = new object[]
            {
                CSIDL_DESKTOP, 0, SWC_DESKTOP, 0, SWFO_NEEDDISPATCH
            };
            desktopDispatch = type.InvokeMember("FindWindowSW",
                System.Reflection.BindingFlags.InvokeMethod, null,
                shellWindows, arguments);
            if (desktopDispatch == null)
                return IntPtr.Zero;

            IServiceProvider provider = desktopDispatch as IServiceProvider;
            if (provider == null)
                return IntPtr.Zero;

            Guid service = SID_STopLevelBrowser;
            Guid browserId = IID_IShellBrowser;
            if (provider.QueryService(ref service, ref browserId, out browser) < 0 ||
                browser == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr queryMethod = GetVTableMethod(browser, 15);
            QueryActiveShellViewDelegate queryView =
                (QueryActiveShellViewDelegate)Marshal.GetDelegateForFunctionPointer(
                    queryMethod, typeof(QueryActiveShellViewDelegate));
            if (queryView(browser, out shellView) < 0 || shellView == IntPtr.Zero)
                return IntPtr.Zero;

            Guid folderViewId = IID_IFolderView2;
            if (Marshal.QueryInterface(shellView, ref folderViewId, out folderView) < 0)
                return IntPtr.Zero;

            IntPtr result = folderView;
            folderView = IntPtr.Zero;
            return result;
        }
        finally
        {
            if (folderView != IntPtr.Zero)
                Marshal.Release(folderView);
            if (shellView != IntPtr.Zero)
                Marshal.Release(shellView);
            if (browser != IntPtr.Zero)
                Marshal.Release(browser);
            if (desktopDispatch != null && Marshal.IsComObject(desktopDispatch))
                Marshal.FinalReleaseComObject(desktopDispatch);
            if (shellWindows != null && Marshal.IsComObject(shellWindows))
                Marshal.FinalReleaseComObject(shellWindows);
        }
    }

    private static IntPtr GetVTableMethod(IntPtr instance, int index)
    {
        IntPtr table = Marshal.ReadIntPtr(instance);
        return Marshal.ReadIntPtr(table, index * IntPtr.Size);
    }
}

internal static class NativeMethods
{
    private const int ROLE_SYSTEM_LISTITEM = 0x22;

    internal delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);
    internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    internal static void EnablePerMonitorDpiAwareness()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                return;
        }
        catch (EntryPointNotFoundException)
        {
            // Windows 10 1703 之前没有 Per-Monitor V2，退回系统 DPI 感知。
        }

        try
        {
            SetProcessDPIAware();
        }
        catch (EntryPointNotFoundException)
        {
            // 仅为极老系统保留；支持的 Windows 10/11 不会进入这里。
        }
    }

    internal static bool TryIsAccessibleListItemAtPoint(POINT point, out bool isListItem)
    {
        isListItem = false;
        Accessibility.IAccessible accessible = null;
        object child = null;
        try
        {
            int result = AccessibleObjectFromPoint(point, out accessible, out child);
            if (result < 0 || accessible == null)
                return false;

            object role = accessible.get_accRole(child ?? 0);
            if (role == null)
                return false;

            isListItem = Convert.ToInt32(role) == ROLE_SYSTEM_LISTITEM;
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            if (accessible != null && Marshal.IsComObject(accessible))
                Marshal.FinalReleaseComObject(accessible);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        internal POINT pt;
        internal uint mouseData;
        internal uint flags;
        internal uint time;
        internal IntPtr extraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int hookId, LowLevelMouseProc callback, IntPtr module, uint threadId);

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromPoint(
        POINT point,
        [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible accessible,
        [MarshalAs(UnmanagedType.Struct)] out object child);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr GetModuleHandle(string moduleName);

    [DllImport("user32.dll")]
    internal static extern uint GetDoubleClickTime();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsChild(IntPtr parent, IntPtr child);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, System.Text.StringBuilder className, int maxCount);

    internal static string GetClassNameString(IntPtr window)
    {
        System.Text.StringBuilder value = new System.Text.StringBuilder(256);
        GetClassName(window, value, value.Capacity);
        return value.ToString();
    }
}
