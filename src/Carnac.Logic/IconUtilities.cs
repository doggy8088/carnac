using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Carnac.Logic
{
    internal static class IconUtilities
    {
        const int CacheCapacity = 64;

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        // Icons are cached per executable path. An executable without a usable icon is cached as null, so nothing is extracted
        // (and no exception is thrown) again on every key press. The cache is thread-safe and bounded.
        private static readonly BoundedCache<string, ImageSource> icons = new BoundedCache<string, ImageSource>(CacheCapacity, LoadIcon);

        public static ImageSource GetProcessIconAsImageSource(string processFileName)
        {
            return icons.Get(processFileName);
        }

        private static ImageSource LoadIcon(string processFileName)
        {
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(processFileName))
                {
                    return icon == null ? null : IconToImageSource(icon);
                }
            }
            catch (ArgumentException)
            {
                // no such file, or nothing to extract: an application without an icon is normal, not an error
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (ExternalException)
            {
                // GDI+ and Win32 failures
                return null;
            }
        }

        private static ImageSource IconToImageSource(Icon icon)
        {
            using (Bitmap bitmap = icon.ToBitmap())
            {
                IntPtr hBitmap = bitmap.GetHbitmap();
                try
                {
                    BitmapSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    // the icon is created on the keyboard hook's thread and shown on the UI thread
                    wpfBitmap.Freeze();
                    return wpfBitmap;
                }
                finally
                {
                    // the WPF bitmap has its own copy; if this fails there is nothing more to do than leaking one GDI handle
                    DeleteObject(hBitmap);
                }
            }
        }
    }
}
