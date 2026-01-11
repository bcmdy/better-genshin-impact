using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Helpers.DpiAwareness;
using BetterGenshinImpact.Helpers.Ui;
using BetterGenshinImpact.View.Windows;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TorchSharp.Data;
using Vanara.PInvoke;
using Wpf.Ui.Controls;
using System.Management;
using System.Runtime.InteropServices;

namespace BetterGenshinImpact.View;

public class CapturableWindow
{
    public IntPtr Handle { get; }
    public string Name { get; }
    public string ProcessName { get; }
    public ImageSource? Icon { get; }
    
    public string Owner { get; }      // 新增：进程所属 Windows 帐户

    public CapturableWindow(IntPtr handle, string name, string processName, string owner, ImageSource? icon)
    {
        Handle = handle;
        Name = name;
        ProcessName = processName;
        Owner = owner;
        Icon = icon;
    }
}

public partial class PickerWindow : FluentWindow
{
    private bool _isSelected;
    private readonly bool _captureTest;

    private const User32.WindowStylesEx IgnoreExStyle = User32.WindowStylesEx.WS_EX_TOOLWINDOW |
                                                        User32.WindowStylesEx.WS_EX_NOREDIRECTIONBITMAP |
                                                        User32.WindowStylesEx.WS_EX_LAYERED;

    public PickerWindow(bool captureTest = false)
    {
        InitializeComponent();
        this.InitializeDpiAwareness();

        // 应用当前主题
        WindowHelper.TryApplySystemBackdrop(this);

        Loaded += OnLoaded;
        MouseLeftButtonDown += PickerWindow_MouseLeftButtonDown;
        _captureTest = captureTest;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FindWindows();
    }
    private void PickerWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 当用户按住鼠标左键时，允许拖拽窗口
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            this.DragMove();
        }
    }

    public bool PickCaptureTarget(IntPtr hWnd, out IntPtr pickedWindow)
    {
        new WindowInteropHelper(this).Owner = hWnd;
        ShowDialog();
        if (!_isSelected)
        {
            pickedWindow = IntPtr.Zero;
            return false;
        }
        pickedWindow = ((CapturableWindow?)WindowList.SelectedItem)?.Handle ?? IntPtr.Zero;
        return true;
    }

    private void FindWindows()
    {
        var wih = new WindowInteropHelper(this);
        var windows = new List<CapturableWindow>();

        User32.EnumWindows((hWnd, lParam) =>
        {
            if (!User32.IsWindowVisible(hWnd) || wih.Handle == (IntPtr)hWnd)
                return true;

            var exStyle = User32.GetWindowLong<User32.WindowStylesEx>(hWnd, User32.WindowLongFlags.GWL_EXSTYLE);
            if ((exStyle & IgnoreExStyle) != 0)
                return true;

            var title = new StringBuilder(1024);
            _ = User32.GetWindowText(hWnd, title, title.Capacity);
            if (string.IsNullOrWhiteSpace(title.ToString()))
                return true;

            _ = User32.GetWindowThreadProcessId(hWnd, out var processId);
            var process = Process.GetProcessById((int)processId);

            // 获取进程所属用户
            var owner = GetProcessOwner((int)processId) ?? string.Empty;

            // 获取窗口图标
            var icon = GetWindowIcon((IntPtr)hWnd);

            windows.Add(new CapturableWindow((IntPtr)hWnd, title.ToString(), process.ProcessName, owner, icon));

            return true;
        }, IntPtr.Zero);

        // 排序：优先原神窗口 -> 优先属于当前登录用户的进程 -> 再按句柄/其它
        var currentUser = Environment.UserName;
        var sortedWindows = windows
            .OrderByDescending(IsGenshinWindow)
            .ThenByDescending(x => string.Equals(x.Owner, currentUser, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Handle)
            .ToList();

        WindowList.ItemsSource = sortedWindows;
        Debug.WriteLine("找到窗口数量: " + sortedWindows.Count);
        //输出找到的LOG信息
        foreach (var window in sortedWindows)
        {
            Debug.WriteLine($"窗口句柄: {window.Handle}, 标题: {window.Name}, 进程名: {window.ProcessName}, 所属用户: {window.Owner}");
        }
        
    }
    
   // 替换原有 GetProcessOwner 方法为以下实现
private static string? GetProcessOwner(int processId)
{
    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    const uint TOKEN_QUERY = 0x0008;
    const int TokenUser = 1;

    IntPtr hProcess = IntPtr.Zero;
    IntPtr hToken = IntPtr.Zero;
    IntPtr tokenInfo = IntPtr.Zero;

    try
    {
        hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
        if (hProcess == IntPtr.Zero)
            return null;

        if (!OpenProcessToken(hProcess, TOKEN_QUERY, out hToken) || hToken == IntPtr.Zero)
            return null;

        // 首次调用获得所需缓冲区大小
        if (GetTokenInformation(hToken, TokenUser, IntPtr.Zero, 0, out var requiredLength) || Marshal.GetLastWin32Error() == 122) // ERROR_INSUFFICIENT_BUFFER
        {
            tokenInfo = Marshal.AllocHGlobal(requiredLength);
            if (!GetTokenInformation(hToken, TokenUser, tokenInfo, requiredLength, out _))
                return null;

            var tokenUser = Marshal.PtrToStructure<TOKEN_USER>(tokenInfo);
            var sid = tokenUser.User.Sid;
            if (sid == IntPtr.Zero)
                return null;

            // Lookup account name and domain
            uint nameLen = 0;
            uint domainLen = 0;
            _ = LookupAccountSid(null, sid, null, ref nameLen, null, ref domainLen, out _);
            var nameSb = new StringBuilder((int)nameLen);
            var domainSb = new StringBuilder((int)domainLen);
            if (LookupAccountSid(null, sid, nameSb, ref nameLen, domainSb, ref domainLen, out _))
            {
                var domain = domainSb.ToString();
                var name = nameSb.ToString();
                return string.IsNullOrEmpty(domain) ? name : $"{domain}\\{name}";
            }
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"GetProcessOwner 异常: {ex.Message}");
    }
    finally
    {
        if (tokenInfo != IntPtr.Zero) Marshal.FreeHGlobal(tokenInfo);
        if (hToken != IntPtr.Zero) CloseHandle(hToken);
        if (hProcess != IntPtr.Zero) CloseHandle(hProcess);
    }

    return null;
}

// P/Invoke 和 结构体定义（放在同一文件任意位置）
[StructLayout(LayoutKind.Sequential)]
private struct SID_AND_ATTRIBUTES
{
    public IntPtr Sid;
    public int Attributes;
}

[StructLayout(LayoutKind.Sequential)]
private struct TOKEN_USER
{
    public SID_AND_ATTRIBUTES User;
}

[DllImport("kernel32.dll", SetLastError = true)]
private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

[DllImport("advapi32.dll", SetLastError = true)]
private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

[DllImport("advapi32.dll", SetLastError = true)]
private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
private static extern bool LookupAccountSid(string lpSystemName, IntPtr Sid, StringBuilder Name, ref uint cchName, StringBuilder ReferencedDomainName, ref uint cchReferencedDomainName, out int peUse);

[DllImport("kernel32.dll", SetLastError = true)]
private static extern bool CloseHandle(IntPtr hObject);

    private ImageSource? GetWindowIcon(IntPtr hWnd)
    {
        try
        {
            const int ICON_BIG = 1;    // WM_GETICON large icon constant
            const int ICON_SMALL = 0;   // WM_GETICON small icon constant
            const int GCL_HICON = -14;  // GetClassLong index for icon

            // 尝试获取窗口大图标
            var iconHandle = User32.SendMessage(hWnd, User32.WindowMessage.WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);

            if (iconHandle == IntPtr.Zero)
            {
                // 尝试获取窗口小图标
                iconHandle = User32.SendMessage(hWnd, User32.WindowMessage.WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
            }

            if (iconHandle == IntPtr.Zero)
            {
                // 尝试获取窗口类图标
                iconHandle = User32.GetClassLong(hWnd, GCL_HICON);
            }

            if (iconHandle != IntPtr.Zero)
            {
                return Imaging.CreateBitmapSourceFromHIcon(
                    iconHandle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取窗口图标失败: {ex.Message}");
        }

        // 如果获取失败，返回一个默认图标或null
        return null;
    }

    private static bool IsGenshinWindow(CapturableWindow window)
    {
        return window is
        { Name: "原神", ProcessName: "YuanShen" } or
        { Name: "云·原神", ProcessName: "Genshin Impact Cloud Game" } or
        { Name: "Genshin Impact", ProcessName: "GenshinImpact" } or
        { Name: "Genshin Impact · Cloud", ProcessName: "Genshin Impact Cloud" };
    }

    private static bool AskIsThisGenshinImpact(CapturableWindow window)
    {
        var res = ThemedMessageBox.Question(
            $"""
            这看起来不像是原神，确定要选择这个窗口吗？

            当前选择的窗口：{window.Name} ({window.ProcessName})
            """,
            "确认选择",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxResult.No
        );
        return res == System.Windows.MessageBoxResult.Yes;
    }

    private void WindowsOnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WindowList.SelectedItem is not CapturableWindow selectedWindow)
            return;

        // 如果不是原神窗口，询问用户是否确认
        if (!_captureTest && !IsGenshinWindow(selectedWindow))
        {
            if (!AskIsThisGenshinImpact(selectedWindow))
            {
                return;
            }
        }
        _isSelected = true;
        Close();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.TryApplySystemBackdrop(this);
        FindWindows();
    }

    private void FluentWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _isSelected = false;
            Close();
        }
    }

    private void cancelButton_Click(object sender, RoutedEventArgs e)
    {
        _isSelected = false;
        Close();
    }
}