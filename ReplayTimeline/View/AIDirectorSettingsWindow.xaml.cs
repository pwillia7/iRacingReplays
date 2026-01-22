using iRacingReplayDirector.AI.Director;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace iRacingReplayDirector
{
	public partial class AIDirectorSettingsWindow : Window
	{
		private readonly AIDirector _aiDirector;

		public AIDirectorSettingsWindow(AIDirector aiDirector)
		{
			InitializeComponent();
			_aiDirector = aiDirector;
			LoadSettings();
		}

		private void LoadSettings()
		{
			// Load provider selection
			if (_aiDirector.Settings.SelectedProvider == "Local")
			{
				ProviderComboBox.SelectedIndex = 1;
				OpenAIPanel.Visibility = Visibility.Collapsed;
				LocalModelPanel.Visibility = Visibility.Visible;
			}
			else
			{
				ProviderComboBox.SelectedIndex = 0;
				OpenAIPanel.Visibility = Visibility.Visible;
				LocalModelPanel.Visibility = Visibility.Collapsed;
			}

			// Load OpenAI settings
			ApiKeyBox.Password = _aiDirector.Settings.OpenAIApiKey;
			foreach (ComboBoxItem item in OpenAIModelComboBox.Items)
			{
				if (item.Content.ToString() == _aiDirector.Settings.OpenAIModel)
				{
					OpenAIModelComboBox.SelectedItem = item;
					break;
				}
			}

			// Load local model settings
			LocalEndpointBox.Text = _aiDirector.Settings.LocalModelEndpoint;
			LocalModelNameBox.Text = _aiDirector.Settings.LocalModelName;

			// Load event detection settings
			DetectIncidentsCheckBox.IsChecked = _aiDirector.Settings.DetectIncidents;
			DetectOvertakesCheckBox.IsChecked = _aiDirector.Settings.DetectOvertakes;
			DetectBattlesCheckBox.IsChecked = _aiDirector.Settings.DetectBattles;
			ScanIntervalBox.Text = _aiDirector.Settings.ScanIntervalFrames.ToString();

			// Load driver selection weights
			IncidentWeightSlider.Value = _aiDirector.Settings.IncidentWeight;
			OvertakeWeightSlider.Value = _aiDirector.Settings.OvertakeWeight;
			BattleWeightSlider.Value = _aiDirector.Settings.BattleWeight;
			MomentumWeightSlider.Value = _aiDirector.Settings.MomentumWeight;
			PackWeightSlider.Value = _aiDirector.Settings.PackWeight;
			FreshActionWeightSlider.Value = _aiDirector.Settings.FreshActionWeight;
			PositionWeightSlider.Value = _aiDirector.Settings.PositionWeight;
			VarietyPenaltySlider.Value = _aiDirector.Settings.VarietyPenalty;
			VarietyDampeningSlider.Value = _aiDirector.Settings.VarietyDampening;
			MinCutsSlider.Value = _aiDirector.Settings.MinCutsPerMinute;

			// Load focus driver settings
			FocusDriverBox.Text = _aiDirector.Settings.FocusDriverNumber.ToString();
			FocusDriverBonusSlider.Value = _aiDirector.Settings.FocusDriverBonus;

			// Load camera plan generation settings
			UseAICheckBox.IsChecked = _aiDirector.Settings.UseAIForCameraPlan;
			AnticipationSlider.Value = _aiDirector.Settings.EventAnticipationSeconds;
			MinSecondsBetweenCutsSlider.Value = _aiDirector.Settings.MinSecondsBetweenCuts;
			MaxSecondsBetweenCutsSlider.Value = _aiDirector.Settings.MaxSecondsBetweenCuts;
			UpdateAIModeVisibility();

			// Load camera exclusion settings
			LoadCameraExclusions();

			// Load overlay settings
			LoadOverlaySettings();
		}

		private void LoadOverlaySettings()
		{
			ShowCurrentDriverOverlayCheckBox.IsChecked = _aiDirector.Settings.ShowCurrentDriverOverlay;
			ShowAheadDriverOverlayCheckBox.IsChecked = _aiDirector.Settings.ShowAheadDriverOverlay;
			ShowBehindDriverOverlayCheckBox.IsChecked = _aiDirector.Settings.ShowBehindDriverOverlay;
			ShowLeaderboardOverlayCheckBox.IsChecked = _aiDirector.Settings.ShowLeaderboardOverlay;

			// Set position combo box
			foreach (ComboBoxItem item in OverlayPositionComboBox.Items)
			{
				if (item.Tag?.ToString() == _aiDirector.Settings.OverlayPosition)
				{
					OverlayPositionComboBox.SelectedItem = item;
					break;
				}
			}

			OverlayOffsetSlider.Value = _aiDirector.Settings.OverlayOffset;
			OverlayFontSizeSlider.Value = _aiDirector.Settings.OverlayFontSize;
		}

		private void LoadCameraExclusions()
		{
			var excluded = _aiDirector.Settings.GetExcludedCameraList();

			// TV Cameras
			ExcludeTV1.IsChecked = excluded.Any(e => e.Equals("TV1", System.StringComparison.OrdinalIgnoreCase));
			ExcludeTV2.IsChecked = excluded.Any(e => e.Equals("TV2", System.StringComparison.OrdinalIgnoreCase));
			ExcludeTV3.IsChecked = excluded.Any(e => e.Equals("TV3", System.StringComparison.OrdinalIgnoreCase));

			// Chase Cameras
			ExcludeChase.IsChecked = excluded.Any(e => e.Equals("Chase", System.StringComparison.OrdinalIgnoreCase));
			ExcludeFarChase.IsChecked = excluded.Any(e => e.Equals("Far Chase", System.StringComparison.OrdinalIgnoreCase));
			ExcludeRearChase.IsChecked = excluded.Any(e => e.Equals("Rear Chase", System.StringComparison.OrdinalIgnoreCase));

			// Onboard Cameras
			ExcludeCockpit.IsChecked = excluded.Any(e => e.Equals("Cockpit", System.StringComparison.OrdinalIgnoreCase));
			ExcludeRollBar.IsChecked = excluded.Any(e => e.Equals("Roll Bar", System.StringComparison.OrdinalIgnoreCase));
			ExcludeGyro.IsChecked = excluded.Any(e => e.Equals("Gyro", System.StringComparison.OrdinalIgnoreCase));
			ExcludeNose.IsChecked = excluded.Any(e => e.Equals("Nose", System.StringComparison.OrdinalIgnoreCase));
			ExcludeGearbox.IsChecked = excluded.Any(e => e.Equals("Gearbox", System.StringComparison.OrdinalIgnoreCase));

			// Suspension Cameras
			ExcludeLFSusp.IsChecked = excluded.Any(e => e.Equals("LF Susp", System.StringComparison.OrdinalIgnoreCase));
			ExcludeRFSusp.IsChecked = excluded.Any(e => e.Equals("RF Susp", System.StringComparison.OrdinalIgnoreCase));
			ExcludeLRSusp.IsChecked = excluded.Any(e => e.Equals("LR Susp", System.StringComparison.OrdinalIgnoreCase));
			ExcludeRRSusp.IsChecked = excluded.Any(e => e.Equals("RR Susp", System.StringComparison.OrdinalIgnoreCase));

			// Aerial Cameras
			ExcludeChopper.IsChecked = excluded.Any(e => e.Equals("Chopper", System.StringComparison.OrdinalIgnoreCase));
			ExcludeBlimp.IsChecked = excluded.Any(e => e.Equals("Blimp", System.StringComparison.OrdinalIgnoreCase));

			// Other Cameras
			ExcludeScenic.IsChecked = excluded.Any(e => e.Equals("Scenic", System.StringComparison.OrdinalIgnoreCase));
			ExcludePitLane.IsChecked = excluded.Any(e => e.Equals("Pit Lane", System.StringComparison.OrdinalIgnoreCase));
			ExcludePitLane2.IsChecked = excluded.Any(e => e.Equals("Pit Lane 2", System.StringComparison.OrdinalIgnoreCase));
		}

		private void SaveSettings()
		{
			// Save provider selection
			var selectedItem = ProviderComboBox.SelectedItem as ComboBoxItem;
			_aiDirector.Settings.SelectedProvider = selectedItem?.Tag?.ToString() ?? "OpenAI";

			// Save OpenAI settings
			_aiDirector.Settings.OpenAIApiKey = ApiKeyBox.Password;
			var selectedModel = OpenAIModelComboBox.SelectedItem as ComboBoxItem;
			_aiDirector.Settings.OpenAIModel = selectedModel?.Content?.ToString() ?? "gpt-4o";

			// Save local model settings
			_aiDirector.Settings.LocalModelEndpoint = LocalEndpointBox.Text;
			_aiDirector.Settings.LocalModelName = LocalModelNameBox.Text;

			// Save event detection settings
			_aiDirector.Settings.DetectIncidents = DetectIncidentsCheckBox.IsChecked ?? true;
			_aiDirector.Settings.DetectOvertakes = DetectOvertakesCheckBox.IsChecked ?? true;
			_aiDirector.Settings.DetectBattles = DetectBattlesCheckBox.IsChecked ?? true;

			if (int.TryParse(ScanIntervalBox.Text, out int scanInterval))
			{
				_aiDirector.Settings.ScanIntervalFrames = scanInterval;
			}

			// Save driver selection weights
			_aiDirector.Settings.IncidentWeight = (int)IncidentWeightSlider.Value;
			_aiDirector.Settings.OvertakeWeight = (int)OvertakeWeightSlider.Value;
			_aiDirector.Settings.BattleWeight = (int)BattleWeightSlider.Value;
			_aiDirector.Settings.MomentumWeight = (int)MomentumWeightSlider.Value;
			_aiDirector.Settings.PackWeight = (int)PackWeightSlider.Value;
			_aiDirector.Settings.FreshActionWeight = (int)FreshActionWeightSlider.Value;
			_aiDirector.Settings.PositionWeight = (int)PositionWeightSlider.Value;
			_aiDirector.Settings.VarietyPenalty = (int)VarietyPenaltySlider.Value;
			_aiDirector.Settings.VarietyDampening = (int)VarietyDampeningSlider.Value;
			_aiDirector.Settings.MinCutsPerMinute = (int)MinCutsSlider.Value;

			// Save focus driver settings
			if (int.TryParse(FocusDriverBox.Text, out int focusDriverNumber))
			{
				_aiDirector.Settings.FocusDriverNumber = focusDriverNumber;
			}
			_aiDirector.Settings.FocusDriverBonus = (int)FocusDriverBonusSlider.Value;

			// Save camera plan generation settings
			_aiDirector.Settings.UseAIForCameraPlan = UseAICheckBox.IsChecked ?? false;
			_aiDirector.Settings.EventAnticipationSeconds = (int)AnticipationSlider.Value;
			_aiDirector.Settings.MinSecondsBetweenCuts = (int)MinSecondsBetweenCutsSlider.Value;
			_aiDirector.Settings.MaxSecondsBetweenCuts = (int)MaxSecondsBetweenCutsSlider.Value;

			// Save camera exclusion settings
			SaveCameraExclusions();

			// Save overlay settings
			SaveOverlaySettings();
		}

		private void SaveOverlaySettings()
		{
			_aiDirector.Settings.ShowCurrentDriverOverlay = ShowCurrentDriverOverlayCheckBox.IsChecked ?? true;
			_aiDirector.Settings.ShowAheadDriverOverlay = ShowAheadDriverOverlayCheckBox.IsChecked ?? false;
			_aiDirector.Settings.ShowBehindDriverOverlay = ShowBehindDriverOverlayCheckBox.IsChecked ?? false;
			_aiDirector.Settings.ShowLeaderboardOverlay = ShowLeaderboardOverlayCheckBox.IsChecked ?? false;

			var selectedPosition = OverlayPositionComboBox.SelectedItem as ComboBoxItem;
			_aiDirector.Settings.OverlayPosition = selectedPosition?.Tag?.ToString() ?? "Bottom";

			_aiDirector.Settings.OverlayOffset = (int)OverlayOffsetSlider.Value;
			_aiDirector.Settings.OverlayFontSize = (int)OverlayFontSizeSlider.Value;
		}

		private void SaveCameraExclusions()
		{
			var excludedCameras = new System.Collections.Generic.List<string>();

			// TV Cameras
			if (ExcludeTV1.IsChecked == true) excludedCameras.Add("TV1");
			if (ExcludeTV2.IsChecked == true) excludedCameras.Add("TV2");
			if (ExcludeTV3.IsChecked == true) excludedCameras.Add("TV3");

			// Chase Cameras
			if (ExcludeChase.IsChecked == true) excludedCameras.Add("Chase");
			if (ExcludeFarChase.IsChecked == true) excludedCameras.Add("Far Chase");
			if (ExcludeRearChase.IsChecked == true) excludedCameras.Add("Rear Chase");

			// Onboard Cameras
			if (ExcludeCockpit.IsChecked == true) excludedCameras.Add("Cockpit");
			if (ExcludeRollBar.IsChecked == true) excludedCameras.Add("Roll Bar");
			if (ExcludeGyro.IsChecked == true) excludedCameras.Add("Gyro");
			if (ExcludeNose.IsChecked == true) excludedCameras.Add("Nose");
			if (ExcludeGearbox.IsChecked == true) excludedCameras.Add("Gearbox");

			// Suspension Cameras
			if (ExcludeLFSusp.IsChecked == true) excludedCameras.Add("LF Susp");
			if (ExcludeRFSusp.IsChecked == true) excludedCameras.Add("RF Susp");
			if (ExcludeLRSusp.IsChecked == true) excludedCameras.Add("LR Susp");
			if (ExcludeRRSusp.IsChecked == true) excludedCameras.Add("RR Susp");

			// Aerial Cameras
			if (ExcludeChopper.IsChecked == true) excludedCameras.Add("Chopper");
			if (ExcludeBlimp.IsChecked == true) excludedCameras.Add("Blimp");

			// Other Cameras
			if (ExcludeScenic.IsChecked == true) excludedCameras.Add("Scenic");
			if (ExcludePitLane.IsChecked == true) excludedCameras.Add("Pit Lane");
			if (ExcludePitLane2.IsChecked == true) excludedCameras.Add("Pit Lane 2");

			_aiDirector.Settings.ExcludedCameras = string.Join(",", excludedCameras);
		}

		private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (OpenAIPanel == null || LocalModelPanel == null)
				return;

			var selectedItem = ProviderComboBox.SelectedItem as ComboBoxItem;
			if (selectedItem?.Tag?.ToString() == "Local")
			{
				OpenAIPanel.Visibility = Visibility.Collapsed;
				LocalModelPanel.Visibility = Visibility.Visible;
			}
			else
			{
				OpenAIPanel.Visibility = Visibility.Visible;
				LocalModelPanel.Visibility = Visibility.Collapsed;
			}
		}

		private void UseAICheckBox_Changed(object sender, RoutedEventArgs e)
		{
			UpdateAIModeVisibility();
		}

		private void UpdateAIModeVisibility()
		{
			if (AIProviderPanel == null || EventDrivenModePanel == null)
				return;

			bool useAI = UseAICheckBox.IsChecked ?? true;
			AIProviderPanel.Visibility = useAI ? Visibility.Visible : Visibility.Collapsed;
			EventDrivenModePanel.Visibility = useAI ? Visibility.Collapsed : Visibility.Visible;
		}

		private async void TestConnection_Click(object sender, RoutedEventArgs e)
		{
			ConnectionStatusText.Text = "Testing connection...";
			ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Gray;

			// Temporarily apply settings for testing
			SaveSettings();

			try
			{
				var provider = _aiDirector.GetCurrentProvider();

				if (!provider.IsConfigured)
				{
					ConnectionStatusText.Text = provider.RequiresApiKey
						? "Please enter an API key first."
						: "Please configure the endpoint.";
					ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Orange;
					return;
				}

				ConnectionStatusText.Text = $"Testing {provider.Name} ({provider.ModelName})...";

				bool success = await provider.TestConnectionAsync();

				if (success)
				{
					ConnectionStatusText.Text = "Connection successful!";
					ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Green;
				}
				else
				{
					string errorMessage = provider.LastError ?? "Connection failed. Check API key and model name.";
					ConnectionStatusText.Text = errorMessage;
					ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Red;
				}
			}
			catch (System.Net.Http.HttpRequestException httpEx)
			{
				ConnectionStatusText.Text = $"Network error: {httpEx.Message}";
				ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Red;
			}
			catch (System.Threading.Tasks.TaskCanceledException)
			{
				ConnectionStatusText.Text = "Connection timed out. Check your network.";
				ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Red;
			}
			catch (System.Exception ex)
			{
				ConnectionStatusText.Text = $"Error: {ex.Message}";
				ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Red;
			}
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			SaveSettings();
			DialogResult = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void ResetWeights_Click(object sender, RoutedEventArgs e)
		{
			// Reset to default values
			IncidentWeightSlider.Value = 70;
			OvertakeWeightSlider.Value = 50;
			BattleWeightSlider.Value = 10;
			MomentumWeightSlider.Value = 20;
			PackWeightSlider.Value = 15;
			FreshActionWeightSlider.Value = 25;
			PositionWeightSlider.Value = 20;
			VarietyPenaltySlider.Value = 70;
			VarietyDampeningSlider.Value = 30;
			MinCutsSlider.Value = 4;

			// Reset focus driver
			FocusDriverBox.Text = "0";
			FocusDriverBonusSlider.Value = 30;
		}

		private void SelectAllCameras_Click(object sender, RoutedEventArgs e)
		{
			// Check all cameras (exclude all)
			SetAllCameraCheckboxes(true);
		}

		private void DeselectAllCameras_Click(object sender, RoutedEventArgs e)
		{
			// Uncheck all cameras (include all)
			SetAllCameraCheckboxes(false);
		}

		private void ResetCameras_Click(object sender, RoutedEventArgs e)
		{
			// Reset to defaults: Scenic, Pit Lane, Pit Lane 2, Chase, Far Chase excluded
			SetAllCameraCheckboxes(false);
			ExcludeScenic.IsChecked = true;
			ExcludePitLane.IsChecked = true;
			ExcludePitLane2.IsChecked = true;
			ExcludeChase.IsChecked = true;
			ExcludeFarChase.IsChecked = true;
		}

		private void SetAllCameraCheckboxes(bool isChecked)
		{
			// TV Cameras
			ExcludeTV1.IsChecked = isChecked;
			ExcludeTV2.IsChecked = isChecked;
			ExcludeTV3.IsChecked = isChecked;

			// Chase Cameras
			ExcludeChase.IsChecked = isChecked;
			ExcludeFarChase.IsChecked = isChecked;
			ExcludeRearChase.IsChecked = isChecked;

			// Onboard Cameras
			ExcludeCockpit.IsChecked = isChecked;
			ExcludeRollBar.IsChecked = isChecked;
			ExcludeGyro.IsChecked = isChecked;
			ExcludeNose.IsChecked = isChecked;
			ExcludeGearbox.IsChecked = isChecked;

			// Suspension Cameras
			ExcludeLFSusp.IsChecked = isChecked;
			ExcludeRFSusp.IsChecked = isChecked;
			ExcludeLRSusp.IsChecked = isChecked;
			ExcludeRRSusp.IsChecked = isChecked;

			// Aerial Cameras
			ExcludeChopper.IsChecked = isChecked;
			ExcludeBlimp.IsChecked = isChecked;

			// Other Cameras
			ExcludeScenic.IsChecked = isChecked;
			ExcludePitLane.IsChecked = isChecked;
			ExcludePitLane2.IsChecked = isChecked;
		}

		private void ResetOverlays_Click(object sender, RoutedEventArgs e)
		{
			ShowCurrentDriverOverlayCheckBox.IsChecked = true;
			ShowAheadDriverOverlayCheckBox.IsChecked = false;
			ShowBehindDriverOverlayCheckBox.IsChecked = false;
			ShowLeaderboardOverlayCheckBox.IsChecked = false;
			OverlayPositionComboBox.SelectedIndex = 1; // Bottom
			OverlayOffsetSlider.Value = 100;
			OverlayFontSizeSlider.Value = 32;
		}
	}
}
