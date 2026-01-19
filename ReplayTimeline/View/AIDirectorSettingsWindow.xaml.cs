using iRacingReplayDirector.AI.Director;
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
			IncidentWeightSlider.Value = 50;
			OvertakeWeightSlider.Value = 40;
			BattleWeightSlider.Value = 35;
			MomentumWeightSlider.Value = 25;
			PackWeightSlider.Value = 15;
			FreshActionWeightSlider.Value = 15;
			PositionWeightSlider.Value = 15;
			VarietyPenaltySlider.Value = 60;
			VarietyDampeningSlider.Value = 40;
			MinCutsSlider.Value = 4;
		}
	}
}
