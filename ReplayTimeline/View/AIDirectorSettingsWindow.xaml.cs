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
				bool success = await provider.TestConnectionAsync();

				if (success)
				{
					ConnectionStatusText.Text = "Connection successful!";
					ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Green;
				}
				else
				{
					ConnectionStatusText.Text = "Connection failed. Check your settings.";
					ConnectionStatusText.Foreground = System.Windows.Media.Brushes.Red;
				}
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
	}
}
