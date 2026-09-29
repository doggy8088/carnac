using System;
using System.Windows.Input;
using Keys = System.Windows.Forms.Keys;

namespace Carnac.UI
{
    public partial class PreferencesView
    {
        readonly PreferencesViewModel viewModel;

        public PreferencesView(PreferencesViewModel viewModel)
        {
            this.viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
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