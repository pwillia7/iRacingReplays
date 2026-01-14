using System.ComponentModel;
using System.Windows;

namespace iRacingReplayDirector
{
	public partial class DriverOverlayWindow : Window
	{
		private readonly ReplayDirectorVM _viewModel;

		public DriverOverlayWindow(ReplayDirectorVM viewModel)
		{
			InitializeComponent();
			_viewModel = viewModel;

			// Subscribe to property changes to update the driver name
			_viewModel.PropertyChanged += ViewModel_PropertyChanged;

			// Set initial position (bottom center of primary screen)
			PositionWindow();

			// Set initial driver name
			UpdateDriverName();
		}

		private void PositionWindow()
		{
			// Position at bottom center of the primary screen
			var screenWidth = SystemParameters.PrimaryScreenWidth;
			var screenHeight = SystemParameters.PrimaryScreenHeight;

			Left = (screenWidth - Width) / 2;
			Top = screenHeight - Height - 100; // 100 pixels from bottom
		}

		private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// Update when OverlayDriver changes (based on our applied camera nodes)
			if (e.PropertyName == "OverlayDriver")
			{
				UpdateDriverName();
			}
		}

		private void UpdateDriverName()
		{
			// Use OverlayDriver which is set when our CamChangeNodes are applied
			if (_viewModel.OverlayDriver != null)
			{
				// Show driver name with car number
				string displayText = $"#{_viewModel.OverlayDriver.Number} {_viewModel.OverlayDriver.TeamName}";
				DriverNameText.Text = displayText;
			}
			else
			{
				DriverNameText.Text = "";
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			// Unsubscribe from events
			_viewModel.PropertyChanged -= ViewModel_PropertyChanged;
			base.OnClosing(e);
		}
	}
}
