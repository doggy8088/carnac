using System;
using System.Windows;

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
            base.OnSourceInitialized(e);

            var workArea = SystemParameters.WorkArea;
            var targetMinHeight = Math.Min(MinHeight, Math.Max(430, workArea.Height - 40));
            if (Height < targetMinHeight)
            {
                Height = Math.Min(760, Math.Max(targetMinHeight, workArea.Height - 60));
            }
            if (Width < MinWidth)
            {
                Width = MinWidth;
            }
            if (Top + Height > workArea.Bottom)
            {
                Top = Math.Max(workArea.Top, workArea.Bottom - Height);
            }
        }
    }
}