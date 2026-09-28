using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Carnac.Logic;
using Forms = System.Windows.Forms;
using Keys = System.Windows.Forms.Keys;

namespace Carnac.UI
{
    public partial class PreferencesView
    {
        readonly PreferencesViewModel viewModel;

        public PreferencesView(PreferencesViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException("viewModel");

            this.viewModel = viewModel;
            DataContext = viewModel;
            // WPF picks the fonts of Chinese text by the language of the element: without this the Traditional and Simplified
            // translations would both be drawn with the fonts for the default language.
            Language = XmlLanguage.GetLanguage((Properties.Resources.Culture ?? CultureInfo.CurrentUICulture).IetfLanguageTag);
            InitializeComponent();
            // ScrollViewer (including the one inside MahApps's tab strip template) marks MouseLeftButtonDown as handled
            // when it takes focus, so listen to handled events too and filter interactive controls explicitly below.
            AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnMouseLeftButtonDown), true);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            // MahApps restores the saved placement (SaveWindowPosition="True") from its own SourceInitialized
            // handler, which is raised by the base implementation, so the bounds can only be validated afterwards.
            base.OnSourceInitialized(e);
            EnsureWindowIsReachable();

            // The sample popups are on the overlay for as long as this window is.
            viewModel.StartPreview();
        }

        protected override void OnClosed(EventArgs e)
        {
            viewModel.StopPreview();

            base.OnClosed(e);
        }

        // The title bar is hidden, so the window is dragged by any part of its background.
        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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
                    element is ComboBox || element is ComboBoxItem || element is Thumb || element is TabItem)
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

        void SilentModeHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CaptureHotkey(e, viewModel.CaptureSilentModeHotkey, viewModel.ClearSilentModeHotkeyCommand);
        }

        void PauseHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CaptureHotkey(e, viewModel.CapturePauseHotkey, viewModel.ClearPauseHotkeyCommand);
        }

        // The hotkey boxes show the key combination that was pressed instead of accepting text.
        static void CaptureHotkey(KeyEventArgs e, Action<Keys, bool, bool, bool, bool> capture, ICommand clear)
        {
            // With Alt held down WPF reports Key.System and puts the real key in SystemKey
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;
            var control = (modifiers & ModifierKeys.Control) != 0;
            var alt = (modifiers & ModifierKeys.Alt) != 0;
            var shift = (modifiers & ModifierKeys.Shift) != 0;
            var windows = (modifiers & ModifierKeys.Windows) != 0;

            // Tab still moves the focus, otherwise keyboard users could not leave the box
            if (key == Key.Tab && !control && !alt)
                return;

            e.Handled = true;

            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LWin:
                case Key.RWin:
                    // only a modifier so far: wait for the key that goes with it
                    return;
            }

            if (modifiers == ModifierKeys.None && (key == Key.Back || key == Key.Delete))
            {
                clear.Execute(null);
                return;
            }

            capture((Keys)KeyInterop.VirtualKeyFromKey(key), control, alt, shift, windows);
        }
    }
}
