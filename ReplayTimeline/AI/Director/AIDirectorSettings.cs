using System.ComponentModel;

namespace iRacingReplayDirector.AI.Director
{
	public class AIDirectorSettings : INotifyPropertyChanged
	{
		// LLM Provider Settings
		private string _selectedProvider = "OpenAI";
		public string SelectedProvider
		{
			get { return _selectedProvider; }
			set { _selectedProvider = value; OnPropertyChanged("SelectedProvider"); }
		}

		private string _openAIApiKey = "";
		public string OpenAIApiKey
		{
			get { return _openAIApiKey; }
			set { _openAIApiKey = value; OnPropertyChanged("OpenAIApiKey"); }
		}

		private string _openAIModel = "gpt-3.5-turbo";
		public string OpenAIModel
		{
			get { return _openAIModel; }
			set { _openAIModel = value; OnPropertyChanged("OpenAIModel"); }
		}

		private string _localModelEndpoint = "http://localhost:11434/v1/chat/completions";
		public string LocalModelEndpoint
		{
			get { return _localModelEndpoint; }
			set { _localModelEndpoint = value; OnPropertyChanged("LocalModelEndpoint"); }
		}

		private string _localModelName = "llama3";
		public string LocalModelName
		{
			get { return _localModelName; }
			set { _localModelName = value; OnPropertyChanged("LocalModelName"); }
		}

		// Event Detection Settings
		private bool _detectIncidents = true;
		public bool DetectIncidents
		{
			get { return _detectIncidents; }
			set { _detectIncidents = value; OnPropertyChanged("DetectIncidents"); }
		}

		private bool _detectOvertakes = true;
		public bool DetectOvertakes
		{
			get { return _detectOvertakes; }
			set { _detectOvertakes = value; OnPropertyChanged("DetectOvertakes"); }
		}

		private bool _detectBattles = true;
		public bool DetectBattles
		{
			get { return _detectBattles; }
			set { _detectBattles = value; OnPropertyChanged("DetectBattles"); }
		}

		private float _battleGapThreshold = 0.02f;
		public float BattleGapThreshold
		{
			get { return _battleGapThreshold; }
			set { _battleGapThreshold = value; OnPropertyChanged("BattleGapThreshold"); }
		}

		// Scan Settings
		private int _scanIntervalFrames = 60;
		public int ScanIntervalFrames
		{
			get { return _scanIntervalFrames; }
			set { _scanIntervalFrames = value; OnPropertyChanged("ScanIntervalFrames"); }
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
