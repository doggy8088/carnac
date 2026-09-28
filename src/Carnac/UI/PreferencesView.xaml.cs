using System;

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
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // The sample popups are on the overlay for as long as this window is.
            viewModel.StartPreview();
        }

        protected override void OnClosed(EventArgs e)
        {
            viewModel.StopPreview();

            base.OnClosed(e);
        }
    }
}