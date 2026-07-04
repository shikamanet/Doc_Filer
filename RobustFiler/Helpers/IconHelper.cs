using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;

namespace RobustFiler.Helpers;

public static class IconHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    public static async Task<BitmapImage?> GetIconAsync(string path, bool isDirectory)
    {
        return await Task.Run(async () =>
        {
            var shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;

            IntPtr result = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                uint attributes = isDirectory ? 0x00000010u /* FILE_ATTRIBUTE_DIRECTORY */ : 0x00000080u /* FILE_ATTRIBUTE_NORMAL */;
                SHGetFileInfo(path, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            }

            if (shinfo.hIcon == IntPtr.Zero) return null;

            try
            {
                using var icon = Icon.FromHandle(shinfo.hIcon);
                using var bitmap = icon.ToBitmap();
                using var ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                
                BitmapImage? bitmapImage = null;
                var tcs = new TaskCompletionSource<bool>();
                
                var mainDispatcher = App.Current.MainWindow?.DispatcherQueue;
                if (mainDispatcher != null)
                {
                    mainDispatcher.TryEnqueue(async () =>
                    {
                        try
                        {
                            ms.Position = 0;
                            bitmapImage = new BitmapImage();
                            using var ras = ms.AsRandomAccessStream();
                            await bitmapImage.SetSourceAsync(ras);
                            tcs.SetResult(true);
                        }
                        catch
                        {
                            tcs.SetResult(false);
                        }
                    });
                    await tcs.Task;
                }
                return bitmapImage;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        });
    }
}
