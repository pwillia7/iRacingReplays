using iRacingReplayDirector.AI.Director;
using System.ComponentModel;
using System.Windows;

namespace iRacingReplayDirector
{
	public partial class BehindDriverOverlayWindow : Window
	{
		private readonly ReplayDirectorVM _viewModel;
		private readonly AIDirectorSettings _settings;

		public BehindDriverOverlayWindow(ReplayDirectorVM viewModel, AIDirectorSettings settings)
		{
			InitializeComponent();
			_viewModel = viewModel;
			_settings = settings;

			// Subscribe to property changes to update the driver name
			_viewModel.PropertyChanged += ViewModel_PropertyChanged;

			// Set initial position
			PositionWindow();

			// Set initial driver name
			UpdateDriverName();

			// Apply font size from settings
			DriverNameText.FontSize = _settings.OverlayFontSize > 0 ? _settings.OverlayFontSize - 8 : 24;
		}

		private void PositionWindow()
		{
			var screenWidth = SystemParameters.PrimaryScreenWidth;
			var screenHeight = SystemParameters.PrimaryScreenHeight;

			// Position to the right side of center
			Left = (screenWidth / 2) + 50;

			if (_settings.OverlayPosition == "Top")
			{
				Top = _settings.OverlayOffset;
			}
			else
			{
				Top = screenHeight - Height - _settings.OverlayOffset;
			}
		}

		private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "BehindDriver")
			{
				UpdateDriverName();
			}
		}

		private void UpdateDriverName()
		{
			if (_viewModel.BehindDriver != null)
			{
				string displayText = $"#{_viewModel.BehindDriver.Number} {_viewModel.BehindDriver.TeamName}";
				DriverNameText.Text = displayText;
			}
			else
			{
				DriverNameText.Text = "";
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			_viewModel.PropertyChanged -= ViewModel_PropertyChanged;
			base.OnClosing(e);
		}
	}
}
