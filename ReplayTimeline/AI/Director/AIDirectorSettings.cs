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

		private string _openAIModel = "gpt-4o";
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

		// ===========================================
		// Driver Selection Algorithm Weights
		// ===========================================

		// Event scoring weights (how much each event type contributes)
		private int _incidentWeight = 50;
		public int IncidentWeight
		{
			get { return _incidentWeight; }
			set { _incidentWeight = value; OnPropertyChanged("IncidentWeight"); }
		}

		private int _overtakeWeight = 40;
		public int OvertakeWeight
		{
			get { return _overtakeWeight; }
			set { _overtakeWeight = value; OnPropertyChanged("OvertakeWeight"); }
		}

		private int _battleWeight = 35;
		public int BattleWeight
		{
			get { return _battleWeight; }
			set { _battleWeight = value; OnPropertyChanged("BattleWeight"); }
		}

		// Bonus weights
		private int _momentumWeight = 25;
		public int MomentumWeight
		{
			get { return _momentumWeight; }
			set { _momentumWeight = value; OnPropertyChanged("MomentumWeight"); }
		}

		private int _packWeight = 15;
		public int PackWeight
		{
			get { return _packWeight; }
			set { _packWeight = value; OnPropertyChanged("PackWeight"); }
		}

		private int _freshActionWeight = 15;
		public int FreshActionWeight
		{
			get { return _freshActionWeight; }
			set { _freshActionWeight = value; OnPropertyChanged("FreshActionWeight"); }
		}

		// Position weight (baseline interest from running position)
		private int _positionWeight = 15;
		public int PositionWeight
		{
			get { return _positionWeight; }
			set { _positionWeight = value; OnPropertyChanged("PositionWeight"); }
		}

		// Variety penalty (how strongly we enforce driver switching)
		private int _varietyPenalty = 60;
		public int VarietyPenalty
		{
			get { return _varietyPenalty; }
			set { _varietyPenalty = value; OnPropertyChanged("VarietyPenalty"); }
		}

		// Variety dampening (0-100: how much action reduces variety penalty)
		// 0 = no dampening (full variety always), 100 = full dampening (action overrides variety)
		private int _varietyDampening = 40;
		public int VarietyDampening
		{
			get { return _varietyDampening; }
			set { _varietyDampening = value; OnPropertyChanged("VarietyDampening"); }
		}

		// Minimum cuts per minute (forces driver switches even during action)
		private int _minCutsPerMinute = 4;
		public int MinCutsPerMinute
		{
			get { return _minCutsPerMinute; }
			set { _minCutsPerMinute = value; OnPropertyChanged("MinCutsPerMinute"); }
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
