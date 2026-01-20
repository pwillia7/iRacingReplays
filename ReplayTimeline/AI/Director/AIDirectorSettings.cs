using System.ComponentModel;
using System.Linq;

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
		private int _incidentWeight = 70;
		public int IncidentWeight
		{
			get { return _incidentWeight; }
			set { _incidentWeight = value; OnPropertyChanged("IncidentWeight"); }
		}

		private int _overtakeWeight = 50;
		public int OvertakeWeight
		{
			get { return _overtakeWeight; }
			set { _overtakeWeight = value; OnPropertyChanged("OvertakeWeight"); }
		}

		private int _battleWeight = 10;
		public int BattleWeight
		{
			get { return _battleWeight; }
			set { _battleWeight = value; OnPropertyChanged("BattleWeight"); }
		}

		// Bonus weights
		private int _momentumWeight = 20;
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

		private int _freshActionWeight = 25;
		public int FreshActionWeight
		{
			get { return _freshActionWeight; }
			set { _freshActionWeight = value; OnPropertyChanged("FreshActionWeight"); }
		}

		// Position weight (baseline interest from running position)
		private int _positionWeight = 20;
		public int PositionWeight
		{
			get { return _positionWeight; }
			set { _positionWeight = value; OnPropertyChanged("PositionWeight"); }
		}

		// Variety penalty (how strongly we enforce driver switching)
		private int _varietyPenalty = 70;
		public int VarietyPenalty
		{
			get { return _varietyPenalty; }
			set { _varietyPenalty = value; OnPropertyChanged("VarietyPenalty"); }
		}

		// Variety dampening (0-100: how much action reduces variety penalty)
		// 0 = no dampening (full variety always), 100 = full dampening (action overrides variety)
		private int _varietyDampening = 30;
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

		// ===========================================
		// Camera Plan Generation Settings
		// ===========================================

		// Whether to use AI/LLM for camera plan generation (false = event-driven local generation)
		private bool _useAIForCameraPlan = false;
		public bool UseAIForCameraPlan
		{
			get { return _useAIForCameraPlan; }
			set { _useAIForCameraPlan = value; OnPropertyChanged("UseAIForCameraPlan"); }
		}

		// Seconds before an event to switch camera (anticipation/rewind)
		private int _eventAnticipationSeconds = 3;
		public int EventAnticipationSeconds
		{
			get { return _eventAnticipationSeconds; }
			set { _eventAnticipationSeconds = value; OnPropertyChanged("EventAnticipationSeconds"); }
		}

		// Maximum seconds between cuts when no events (fills gaps)
		private int _maxSecondsBetweenCuts = 15;
		public int MaxSecondsBetweenCuts
		{
			get { return _maxSecondsBetweenCuts; }
			set { _maxSecondsBetweenCuts = value; OnPropertyChanged("MaxSecondsBetweenCuts"); }
		}

		// Minimum seconds between cuts (prevents rapid switching)
		private int _minSecondsBetweenCuts = 4;
		public int MinSecondsBetweenCuts
		{
			get { return _minSecondsBetweenCuts; }
			set { _minSecondsBetweenCuts = value; OnPropertyChanged("MinSecondsBetweenCuts"); }
		}

		// Target seconds between camera cuts (used for LLM mode)
		private int _secondsBetweenCuts = 10;
		public int SecondsBetweenCuts
		{
			get { return _secondsBetweenCuts; }
			set { _secondsBetweenCuts = value; OnPropertyChanged("SecondsBetweenCuts"); }
		}

		// ===========================================
		// Camera Selection Settings
		// ===========================================

		// Comma-separated list of camera names to exclude
		private string _excludedCameras = "Scenic,Pit Lane,Pit Lane 2";
		public string ExcludedCameras
		{
			get { return _excludedCameras; }
			set { _excludedCameras = value; OnPropertyChanged("ExcludedCameras"); }
		}

		/// <summary>
		/// Get excluded cameras as an array of names.
		/// </summary>
		public string[] GetExcludedCameraList()
		{
			if (string.IsNullOrWhiteSpace(_excludedCameras))
				return new string[0];

			return _excludedCameras
				.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.Where(s => !string.IsNullOrEmpty(s))
				.ToArray();
		}

		/// <summary>
		/// Check if a camera name is excluded.
		/// </summary>
		public bool IsCameraExcluded(string cameraName)
		{
			var excluded = GetExcludedCameraList();
			return excluded.Any(ex => cameraName.Equals(ex, System.StringComparison.OrdinalIgnoreCase));
		}

		// ===========================================
		// Focus Driver Settings
		// ===========================================

		// Car number to focus on (0 = no focus driver)
		private int _focusDriverNumber = 0;
		public int FocusDriverNumber
		{
			get { return _focusDriverNumber; }
			set { _focusDriverNumber = value; OnPropertyChanged("FocusDriverNumber"); }
		}

		// Bonus added to focus driver's score (0-100)
		private int _focusDriverBonus = 30;
		public int FocusDriverBonus
		{
			get { return _focusDriverBonus; }
			set { _focusDriverBonus = value; OnPropertyChanged("FocusDriverBonus"); }
		}

		// ===========================================
		// Overlay Settings
		// ===========================================

		// Show the current driver overlay during playback
		private bool _showCurrentDriverOverlay = true;
		public bool ShowCurrentDriverOverlay
		{
			get { return _showCurrentDriverOverlay; }
			set { _showCurrentDriverOverlay = value; OnPropertyChanged("ShowCurrentDriverOverlay"); }
		}

		// Show the driver ahead overlay during playback
		private bool _showAheadDriverOverlay = false;
		public bool ShowAheadDriverOverlay
		{
			get { return _showAheadDriverOverlay; }
			set { _showAheadDriverOverlay = value; OnPropertyChanged("ShowAheadDriverOverlay"); }
		}

		// Show the driver behind overlay during playback
		private bool _showBehindDriverOverlay = false;
		public bool ShowBehindDriverOverlay
		{
			get { return _showBehindDriverOverlay; }
			set { _showBehindDriverOverlay = value; OnPropertyChanged("ShowBehindDriverOverlay"); }
		}

		// Overlay vertical position: "Top" or "Bottom"
		private string _overlayPosition = "Bottom";
		public string OverlayPosition
		{
			get { return _overlayPosition; }
			set { _overlayPosition = value; OnPropertyChanged("OverlayPosition"); }
		}

		// Overlay offset from edge in pixels
		private int _overlayOffset = 100;
		public int OverlayOffset
		{
			get { return _overlayOffset; }
			set { _overlayOffset = value; OnPropertyChanged("OverlayOffset"); }
		}

		// Overlay font size
		private int _overlayFontSize = 32;
		public int OverlayFontSize
		{
			get { return _overlayFontSize; }
			set { _overlayFontSize = value; OnPropertyChanged("OverlayFontSize"); }
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
