using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;


namespace iRacingReplayDirector
{
	public partial class MainWindow : Window
	{
		ReplayDirectorVM _vm;
		private DriverOverlayWindow _driverOverlay;

		public MainWindow()
		{
			InitializeComponent();

			_vm = this.DataContext as ReplayDirectorVM;

			// Subscribe to property changes to manage driver overlay
			_vm.PropertyChanged += ViewModel_PropertyChanged;
		}

		private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "PlaybackEnabled")
			{
				if (_vm.PlaybackEnabled)
				{
					ShowDriverOverlay();
				}
				else
				{
					HideDriverOverlay();
				}
			}
		}

		private void ShowDriverOverlay()
		{
			if (_driverOverlay == null)
			{
				_driverOverlay = new DriverOverlayWindow(_vm);
			}
			_driverOverlay.Show();
		}

		private void HideDriverOverlay()
		{
			if (_driverOverlay != null)
			{
				_driverOverlay.Hide();
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			// Clean up driver overlay
			if (_driverOverlay != null)
			{
				_driverOverlay.Close();
				_driverOverlay = null;
			}

			// Unsubscribe from events
			_vm.PropertyChanged -= ViewModel_PropertyChanged;

			_vm.ApplicationClosing(this.RenderSize);
		}

		private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ListBox listBox = sender as ListBox;
			listBox.ScrollIntoView(listBox.SelectedItem);
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			_vm.ShowDriverCameraPanels = e.NewSize.Width > _vm.WidthToDisableSidePanels;

			if (!e.HeightChanged)
				return;

			bool previouslyLowEnoughToDisable = e.PreviousSize.Height < _vm.HeightToDisableControls;
			bool nowHighEnoughToEnable = e.NewSize.Height > _vm.HeightToDisableControls;
			
			// If height was short and is now tall enough
			if (previouslyLowEnoughToDisable && nowHighEnoughToEnable)
			{
				
			}

			// If height was tall enough and is now short enough
			if (!previouslyLowEnoughToDisable && !nowHighEnoughToEnable)
			{
				_vm.ShowVisualTimeline = e.NewSize.Height > _vm.HeightToDisableControls;
				_vm.ShowSessionLapSkipControls = e.NewSize.Height > _vm.HeightToDisableControls;
				_vm.ShowRecordingControls = e.NewSize.Height > _vm.HeightToDisableControls;
			}
		}

		private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}
	}
}
