using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Carnac.Logic;
using Forms = System.Windows.Forms;

namespace Carnac.UI
{
    public partial class PreferencesView
    {
        public PreferencesView(PreferencesViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            // MahApps restores the saved placement (SaveWindowPosition="True") from its own SourceInitialized
            // handler, which is raised by the base implementation, so the bounds can only be validated afterwards.
            base.OnSourceInitialized(e);
            EnsureWindowIsReachable();
        }

        // The title bar is hidden, so the window is dragged by any part of its background.
        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Controls that use the mouse themselves usually mark the event as handled; the checks below cover
            // the parts that do not (slider margins, text box borders, drop-down popups, thumbs that hold the capture).
            if (Mouse.LeftButton != MouseButtonState.Pressed || Mouse.Captured != null)
                return;

            if (IsInsideInteractiveControl(e.OriginalSource as DependencyObject))
                return;

            DragMove();
        }

        static bool IsInsideInteractiveControl(DependencyObject element)
        {
            while (element != null)
            {
                if (element is ButtonBase || element is TextBoxBase || element is RangeBase ||
                    element is ComboBox || element is TabItem)
                    return true;

                element = GetParent(element);
            }

            return false;
        }

        static DependencyObject GetParent(DependencyObject element)
        {
            // Content elements such as Run are not part of the visual tree and VisualTreeHelper throws for them.
            return element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        // Restoring the last placement blindly leaves the window off-screen or unusably small after a monitor was
        // unplugged or the resolution changed, and without a title bar there is nothing to grab to fix that.
        void EnsureWindowIsReachable()
        {
            if (WindowState != WindowState.Normal)
                return;

            // The source is looked up by handle: PresentationSource.FromVisual(this) can still return null while the window is being set up.
            var handle = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(handle);
            NativeRect nativeBounds;
            if (source == null || source.CompositionTarget == null || !GetWindowRect(handle, out nativeBounds))
                return;

            // Screen and window coordinates are device pixels, Left/Top/Width/Height are device independent pixels.
            var toDips = source.CompositionTarget.TransformFromDevice;
            var current = ToDips(nativeBounds.Left, nativeBounds.Top, nativeBounds.Right, nativeBounds.Bottom, toDips);
            var workAreas = Forms.Screen.AllScreens.Select(s => ToDips(s.WorkingArea, toDips)).ToList();
            var primary = ToDips(Forms.Screen.PrimaryScreen.WorkingArea, toDips);

            var clamped = WindowBoundsClamper.Clamp(current, new Size(MinWidth, MinHeight), workAreas, primary);
            if (clamped == current)
                return;

            Left = clamped.Left;
            Top = clamped.Top;
            Width = clamped.Width;
            Height = clamped.Height;
        }

        static Rect ToDips(System.Drawing.Rectangle pixels, Matrix toDips)
        {
            return ToDips(pixels.Left, pixels.Top, pixels.Right, pixels.Bottom, toDips);
        }

        static Rect ToDips(int left, int top, int right, int bottom, Matrix toDips)
        {
            var rect = new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
            rect.Transform(toDips);
            return rect;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
