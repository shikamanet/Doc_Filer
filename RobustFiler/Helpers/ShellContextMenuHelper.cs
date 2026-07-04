using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace RobustFiler.Helpers;

/// <summary>
/// Win32 APIを直接使用してエクスプローラの標準コンテキストメニューを表示するヘルパー。
/// Vanaraのラッパーを使わず、P/Invokeで直接制御することでWinUI 3との互換性を確保する。
/// </summary>
public static class ShellContextMenuHelper
{
    #region COM Interfaces

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    private interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);

        [PreserveSig]
        int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);

        [PreserveSig]
        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        [PreserveSig]
        int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int GetAttributesOf(uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);

        [PreserveSig]
        int GetUIObjectOf(IntPtr hwndOwner, uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
            ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

        [PreserveSig]
        int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);

        [PreserveSig]
        int SetNameOf(IntPtr hwnd, IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    #endregion

    #region Structs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? lpParameters;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? lpTitle;
        public IntPtr lpVerbW;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpParametersW;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpDirectoryW;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpTitleW;
        public POINT ptInvoke;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    #endregion

    #region P/Invoke

    [DllImport("shell32.dll")]
    private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string pszName, IntPtr pbc,
        out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(IntPtr pidl,
        [In] ref Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hmenu, uint fuFlags,
        int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName,
        string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    #endregion

    #region Constants

    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXPLORE = 0x00000004;
    private const uint CMF_CANRENAME = 0x00000010;
    private const uint CMF_EXTENDEDVERBS = 0x00000100;
    private const uint CMIC_MASK_UNICODE = 0x00004000;
    private const uint CMIC_MASK_PTINVOKE = 0x20000000;
    private const int SW_SHOWNORMAL = 1;
    private const uint WM_QUIT = 0x0012;
    private const uint WM_USER = 0x0400;
    private const uint WM_USER_SHOWMENU = WM_USER + 1;

    private static readonly Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IID_IContextMenu = new("000214e4-0000-0000-c000-000000000046");

    #endregion

    // メニュー表示中フラグ
    private static int _isShowing = 0;

    /// <summary>
    /// 専用STAスレッド上でネイティブのシェルコンテキストメニューを表示する。
    /// スレッドの生存期間と COM の初期化/解放、メッセージループを完全に制御する。
    /// </summary>
    public static void ShowContextMenu(string[] paths, int screenX, int screenY, Action? onMenuClosed = null)
    {
        // 多重呼び出し防止
        if (Interlocked.CompareExchange(ref _isShowing, 1, 0) != 0)
            return;

        var thread = new Thread(() =>
        {
            // OLE初期化（COM STA + DragDrop対応）
            OleInitialize(IntPtr.Zero);
            try
            {
                ShowContextMenuInternal(paths, screenX, screenY);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShellContextMenu error: {ex}");
            }
            finally
            {
                OleUninitialize();
                Interlocked.Exchange(ref _isShowing, 0);
                onMenuClosed?.Invoke();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    private static void ShowContextMenuInternal(string[] paths, int screenX, int screenY)
    {
        if (paths == null || paths.Length == 0) return;

        // 隠しウィンドウを作成（メニューのオーナーウィンドウとして必要）
        var hInstance = GetModuleHandle(null);
        var className = "RobustFilerCtxMenu_" + Thread.CurrentThread.ManagedThreadId;

        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate<WndProcDelegate>(WndProc),
            hInstance = hInstance,
            lpszClassName = className,
        };
        // デリゲートをGC回収されないように保持
        _wndProcDelegate = WndProc;
        wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);

        RegisterClassExW(ref wc);

        var hwnd = CreateWindowExW(0, className, "ContextMenuHost", 0,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("Failed to create hidden window for context menu");
            return;
        }

        try
        {
            ShowMenuForPaths(hwnd, paths, screenX, screenY);
        }
        finally
        {
            DestroyWindow(hwnd);
        }
    }

    [ThreadStatic]
    private static WndProcDelegate? _wndProcDelegate;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static void ShowMenuForPaths(IntPtr hwnd, string[] paths, int screenX, int screenY)
    {
        // 単一ファイル/フォルダの場合
        if (paths.Length == 1)
        {
            ShowMenuForSinglePath(hwnd, paths[0], screenX, screenY);
            return;
        }

        // 複数ファイルの場合：同一フォルダ内のファイルをまとめて処理
        ShowMenuForMultiplePaths(hwnd, paths, screenX, screenY);
    }

    private static void ShowMenuForSinglePath(IntPtr hwnd, string path, int screenX, int screenY)
    {
        // パスのPIDLを取得
        int hr = SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _);
        if (hr != 0 || pidl == IntPtr.Zero) return;

        try
        {
            // 親フォルダとPIDLの相対部分を取得
            var riid = IID_IShellFolder;
            hr = SHBindToParent(pidl, ref riid, out var ppv, out var pidlChild);
            if (hr != 0) return;

            var parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(ppv);
            Marshal.Release(ppv);

            try
            {
                // IContextMenuを取得
                var iidContextMenu = IID_IContextMenu;
                var pidls = new[] { pidlChild };
                hr = parentFolder.GetUIObjectOf(hwnd, 1, pidls, ref iidContextMenu, IntPtr.Zero, out var ctxMenuPtr);
                if (hr != 0) return;

                var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(ctxMenuPtr);
                Marshal.Release(ctxMenuPtr);

                try
                {
                    ShowAndInvokeMenu(hwnd, contextMenu, screenX, screenY, path);
                }
                finally
                {
                    Marshal.ReleaseComObject(contextMenu);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(parentFolder);
            }
        }
        finally
        {
            ILFree(pidl);
        }
    }

    private static void ShowMenuForMultiplePaths(IntPtr hwnd, string[] paths, int screenX, int screenY)
    {
        // 複数パス：同一ディレクトリ内と仮定
        var dir = Path.GetDirectoryName(paths[0]);
        if (string.IsNullOrEmpty(dir)) return;

        // 親ディレクトリのPIDLを取得
        int hr = SHParseDisplayName(dir, IntPtr.Zero, out var dirPidl, 0, out _);
        if (hr != 0 || dirPidl == IntPtr.Zero) return;

        IShellFolder? parentFolder = null;
        IntPtr[] childPidls = new IntPtr[paths.Length];
        IntPtr[] fullPidls = new IntPtr[paths.Length];

        try
        {
            // 親フォルダのIShellFolderを取得
            var riid = IID_IShellFolder;
            SHGetDesktopFolder(out var desktop);
            hr = desktop.BindToObject(dirPidl, IntPtr.Zero, ref riid, out var ppv);
            Marshal.ReleaseComObject(desktop);
            if (hr != 0) return;

            parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(ppv);
            Marshal.Release(ppv);

            // 各ファイルの相対PIDLを取得
            for (int i = 0; i < paths.Length; i++)
            {
                hr = SHParseDisplayName(paths[i], IntPtr.Zero, out fullPidls[i], 0, out _);
                if (hr != 0) return;

                var tempRiid = IID_IShellFolder;
                SHBindToParent(fullPidls[i], ref tempRiid, out _, out childPidls[i]);
            }

            // IContextMenuを取得
            var iidContextMenu = IID_IContextMenu;
            hr = parentFolder.GetUIObjectOf(hwnd, (uint)childPidls.Length, childPidls,
                ref iidContextMenu, IntPtr.Zero, out var ctxMenuPtr);
            if (hr != 0) return;

            var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(ctxMenuPtr);
            Marshal.Release(ctxMenuPtr);

            try
            {
                ShowAndInvokeMenu(hwnd, contextMenu, screenX, screenY, dir);
            }
            finally
            {
                Marshal.ReleaseComObject(contextMenu);
            }
        }
        finally
        {
            if (parentFolder != null) Marshal.ReleaseComObject(parentFolder);
            foreach (var p in fullPidls) { if (p != IntPtr.Zero) ILFree(p); }
            ILFree(dirPidl);
        }
    }

    private static void ShowAndInvokeMenu(IntPtr hwnd, IContextMenu contextMenu, int x, int y, string directory)
    {
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            // メニュー項目を追加
            int hr = contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORE | CMF_CANRENAME);
            if (hr < 0) return;

            // メニューを表示してユーザーの選択を待つ（ブロッキング、内部でメッセージループが回る）
            uint cmd = (uint)TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, x, y, hwnd, IntPtr.Zero);

            if (cmd > 0)
            {
                // 選択されたコマンドを実行
                var info = new CMINVOKECOMMANDINFOEX
                {
                    cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                    fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE,
                    hwnd = hwnd,
                    lpVerb = (IntPtr)(cmd - 1),
                    lpVerbW = (IntPtr)(cmd - 1),
                    lpDirectoryW = directory,
                    nShow = SW_SHOWNORMAL,
                    ptInvoke = new POINT { X = x, Y = y },
                };
                contextMenu.InvokeCommand(ref info);
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }
}
