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
		private AheadDriverOverlayWindow _aheadDriverOverlay;
		private BehindDriverOverlayWindow _behindDriverOverlay;
		private LeaderboardOverlayWindow _leaderboardOverlay;

		public MainWindow()
		{
			InitializeComponent();

			_vm = this.DataContext as ReplayDirectorVM;

			// Subscribe to property changes to manage driver overlays
			_vm.PropertyChanged += ViewModel_PropertyChanged;
		}

		private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "PlaybackEnabled")
			{
				if (_vm.PlaybackEnabled)
				{
					ShowDriverOverlays();
				}
				else
				{
					HideDriverOverlays();
				}
			}
			else if (e.PropertyName == "ShowDriverOverlay")
			{
				// Handle setting change while playing
				if (_vm.PlaybackEnabled)
				{
					UpdateCurrentDriverOverlay();
				}
			}
			else if (e.PropertyName == "ShowLeaderboardOverlay")
			{
				// Handle setting change while playing
				if (_vm.PlaybackEnabled)
				{
					UpdateLeaderboardOverlay();
				}
			}
		}

		private void ShowDriverOverlays()
		{
			var settings = _vm.AIDirector?.Settings;

			// Show current driver overlay if enabled (use existing ShowDriverOverlay setting or AI Director setting)
			if (_vm.ShowDriverOverlay || (settings?.ShowCurrentDriverOverlay ?? false))
			{
				if (_driverOverlay == null)
				{
					_driverOverlay = new DriverOverlayWindow(_vm);
				}
				_driverOverlay.Show();
			}

			// Show ahead driver overlay if enabled in AI Director settings
			if (settings?.ShowAheadDriverOverlay ?? false)
			{
				if (_aheadDriverOverlay == null)
				{
					_aheadDriverOverlay = new AheadDriverOverlayWindow(_vm, settings);
				}
				_aheadDriverOverlay.Show();
			}

			// Show behind driver overlay if enabled in AI Director settings
			if (settings?.ShowBehindDriverOverlay ?? false)
			{
				if (_behindDriverOverlay == null)
				{
					_behindDriverOverlay = new BehindDriverOverlayWindow(_vm, settings);
				}
				_behindDriverOverlay.Show();
			}

			// Show leaderboard overlay if enabled
			if (_vm.ShowLeaderboardOverlay)
			{
				if (_leaderboardOverlay == null)
				{
					_leaderboardOverlay = new LeaderboardOverlayWindow(_vm, settings);
				}
				_leaderboardOverlay.Show();
			}
		}

		private void HideDriverOverlays()
		{
			if (_driverOverlay != null)
			{
				_driverOverlay.Hide();
			}
			if (_aheadDriverOverlay != null)
			{
				_aheadDriverOverlay.Hide();
			}
			if (_behindDriverOverlay != null)
			{
				_behindDriverOverlay.Hide();
			}
			if (_leaderboardOverlay != null)
			{
				_leaderboardOverlay.Hide();
			}
		}

		private void UpdateCurrentDriverOverlay()
		{
			if (_vm.ShowDriverOverlay)
			{
				if (_driverOverlay == null)
				{
					_driverOverlay = new DriverOverlayWindow(_vm);
				}
				_driverOverlay.Show();
			}
			else
			{
				if (_driverOverlay != null)
				{
					_driverOverlay.Hide();
				}
			}
		}

		private void UpdateLeaderboardOverlay()
		{
			var settings = _vm.AIDirector?.Settings;
			if (_vm.ShowLeaderboardOverlay)
			{
				if (_leaderboardOverlay == null)
				{
					_leaderboardOverlay = new LeaderboardOverlayWindow(_vm, settings);
				}
				_leaderboardOverlay.Show();
			}
			else
			{
				if (_leaderboardOverlay != null)
				{
					_leaderboardOverlay.Hide();
				}
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			// Clean up driver overlays
			if (_driverOverlay != null)
			{
				_driverOverlay.Close();
				_driverOverlay = null;
			}
			if (_aheadDriverOverlay != null)
			{
				_aheadDriverOverlay.Close();
				_aheadDriverOverlay = null;
			}
			if (_behindDriverOverlay != null)
			{
				_behindDriverOverlay.Close();
				_behindDriverOverlay = null;
			}
			if (_leaderboardOverlay != null)
			{
				_leaderboardOverlay.Close();
				_leaderboardOverlay = null;
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
