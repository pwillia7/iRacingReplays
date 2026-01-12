using iRacingReplayDirector.AI.EventDetection;
using iRacingReplayDirector.AI.LLM;
using iRacingReplayDirector.AI.Models;
using iRacingSimulator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace iRacingReplayDirector.AI.Director
{
	public enum AIDirectorState
	{
		Idle,
		Scanning,
		GeneratingPlan,
		ApplyingPlan,
		Error
	}

	public class AIDirector : INotifyPropertyChanged
	{
		private readonly ReplayDirectorVM _viewModel;
		private readonly List<IEventDetector> _eventDetectors;

		public AIDirectorSettings Settings { get; private set; }

		private AIDirectorState _state = AIDirectorState.Idle;
		public AIDirectorState State
		{
			get { return _state; }
			private set { _state = value; OnPropertyChanged("State"); OnPropertyChanged("IsBusy"); }
		}

		public bool IsBusy => State != AIDirectorState.Idle && State != AIDirectorState.Error;

		private int _scanProgress;
		public int ScanProgress
		{
			get { return _scanProgress; }
			private set { _scanProgress = value; OnPropertyChanged("ScanProgress"); }
		}

		private string _statusMessage = "Ready";
		public string StatusMessage
		{
			get { return _statusMessage; }
			private set { _statusMessage = value; OnPropertyChanged("StatusMessage"); }
		}

		private ReplayScanResult _lastScanResult;
		public ReplayScanResult LastScanResult
		{
			get { return _lastScanResult; }
			private set { _lastScanResult = value; OnPropertyChanged("LastScanResult"); OnPropertyChanged("HasScanResult"); }
		}

		public bool HasScanResult => LastScanResult != null && LastScanResult.Events.Count > 0;

		private CameraPlan _generatedPlan;
		public CameraPlan GeneratedPlan
		{
			get { return _generatedPlan; }
			private set { _generatedPlan = value; OnPropertyChanged("GeneratedPlan"); OnPropertyChanged("HasGeneratedPlan"); }
		}

		public bool HasGeneratedPlan => GeneratedPlan != null && GeneratedPlan.CameraActions.Count > 0;

		public AIDirector(ReplayDirectorVM viewModel)
		{
			_viewModel = viewModel;
			Settings = new AIDirectorSettings();

			_eventDetectors = new List<IEventDetector>
			{
				new IncidentDetector(),
				new OvertakeDetector(),
				new BattleDetector()
			};
		}

		public ILLMProvider GetCurrentProvider()
		{
			if (Settings.SelectedProvider == "Local")
			{
				return new LocalModelProvider
				{
					EndpointUrl = Settings.LocalModelEndpoint,
					Model = Settings.LocalModelName
				};
			}
			else
			{
				return new OpenAIProvider
				{
					ApiKey = Settings.OpenAIApiKey,
					Model = Settings.OpenAIModel
				};
			}
		}

		public async Task<ReplayScanResult> ScanReplayAsync(int startFrame, int endFrame, IProgress<int> progress = null, CancellationToken cancellationToken = default)
		{
			State = AIDirectorState.Scanning;
			StatusMessage = "Scanning replay...";
			ScanProgress = 0;

			var result = new ReplayScanResult
			{
				StartFrame = startFrame,
				EndFrame = endFrame
			};

			var snapshots = new List<TelemetrySnapshot>();

			try
			{
				// Validate we have a connection to iRacing
				if (Sim.Instance == null || Sim.Instance.Sdk == null)
				{
					throw new InvalidOperationException("iRacing is not connected");
				}

				if (Sim.Instance.SessionInfo == null)
				{
					throw new InvalidOperationException("No session info available");
				}

				// Get track and session info
				try
				{
					var weekendInfo = Sim.Instance.SessionInfo["WeekendInfo"];
					result.TrackName = weekendInfo?["TrackName"]?.GetValue("Unknown Track") ?? "Unknown Track";

					var sessionNum = Sim.Instance.Telemetry?.SessionNum?.Value ?? 0;
					var sessionInfo = Sim.Instance.SessionInfo["SessionInfo"]?["Sessions"]?["SessionNum", sessionNum];
					result.SessionType = sessionInfo?["SessionType"]?.GetValue("Race") ?? "Race";
				}
				catch
				{
					result.TrackName = "Unknown Track";
					result.SessionType = "Race";
				}

				int totalFrames = endFrame - startFrame;
				if (totalFrames <= 0)
				{
					throw new InvalidOperationException("Invalid frame range");
				}

				int frameStep = Settings.ScanIntervalFrames;
				int framesProcessed = 0;

				// Store original position
				int originalFrame = _viewModel.CurrentFrame;

				// Verify we can access the dispatcher
				if (Application.Current?.Dispatcher == null)
				{
					throw new InvalidOperationException("Cannot access UI dispatcher");
				}

				// Scan through the replay
				for (int frame = startFrame; frame <= endFrame; frame += frameStep)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						StatusMessage = "Scan cancelled";
						State = AIDirectorState.Idle;
						return null;
					}

					// Jump to frame on UI thread
					try
					{
						await Application.Current.Dispatcher.InvokeAsync(() =>
						{
							try
							{
								if (Sim.Instance?.Sdk?.Replay != null)
								{
									Sim.Instance.Sdk.Replay.SetPosition(frame);
								}
							}
							catch { }
						});
					}
					catch (Exception)
					{
						// Dispatcher might be unavailable
						continue;
					}

					// Wait for telemetry to update
					try
					{
						await Task.Delay(100).ConfigureAwait(false);
					}
					catch (TaskCanceledException)
					{
						break;
					}

					// Capture snapshot on UI thread
					TelemetrySnapshot snapshot = null;
					try
					{
						await Application.Current.Dispatcher.InvokeAsync(() =>
						{
							snapshot = CaptureSnapshot(frame);
						});
					}
					catch (Exception)
					{
						// Skip this frame if dispatcher fails
						continue;
					}

					if (snapshot != null)
					{
						snapshots.Add(snapshot);
					}

					framesProcessed += frameStep;
					int progressPct = (int)((framesProcessed / (float)totalFrames) * 100);
					ScanProgress = Math.Min(progressPct, 100);
					progress?.Report(ScanProgress);

					StatusMessage = $"Scanning: {ScanProgress}% ({frame}/{endFrame})";
				}

				result.Snapshots = snapshots;
				result.DurationSeconds = totalFrames / 60.0; // Assuming 60fps

				// Run event detectors
				StatusMessage = "Analyzing events...";

				foreach (var detector in _eventDetectors)
				{
					if (!ShouldRunDetector(detector))
						continue;

					var events = detector.DetectEvents(snapshots);
					if (events != null)
					{
						result.Events.AddRange(events);
					}
				}

				// Sort events by frame
				result.Events = result.Events.OrderBy(e => e.Frame).ToList();

				// Return to original position on UI thread
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					try
					{
						Sim.Instance.Sdk.Replay.SetPosition(originalFrame);
					}
					catch { }
				});

				LastScanResult = result;
				StatusMessage = $"Scan complete: {result.Events.Count} events detected";
				State = AIDirectorState.Idle;

				return result;
			}
			catch (Exception ex)
			{
				StatusMessage = $"Scan error: {ex.Message}";
				State = AIDirectorState.Error;
				throw;
			}
		}

		private bool ShouldRunDetector(IEventDetector detector)
		{
			switch (detector.EventType)
			{
				case RaceEventType.Incident:
					return Settings.DetectIncidents;
				case RaceEventType.Overtake:
					return Settings.DetectOvertakes;
				case RaceEventType.Battle:
					return Settings.DetectBattles;
				default:
					return true;
			}
		}

		private TelemetrySnapshot CaptureSnapshot(int frame)
		{
			try
			{
				if (_viewModel?.Drivers == null || _viewModel.Drivers.Count == 0)
					return null;

				var snapshot = new TelemetrySnapshot
				{
					Frame = frame,
					SessionTime = _viewModel.SessionTime,
					DriverStates = new List<DriverSnapshot>()
				};

				foreach (var driver in _viewModel.Drivers)
				{
					if (driver == null)
						continue;

					snapshot.DriverStates.Add(new DriverSnapshot
					{
						Id = driver.Id,
						NumberRaw = driver.NumberRaw,
						TeamName = driver.TeamName ?? string.Empty,
						Position = driver.Position,
						Lap = driver.Lap,
						LapDistance = driver.LapDistance,
						TrackSurface = driver.TrackSurface
					});
				}

				return snapshot;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public RaceEventSummary BuildEventSummary()
		{
			if (LastScanResult == null)
				return null;

			var summary = new RaceEventSummary
			{
				TrackName = LastScanResult.TrackName,
				SessionType = LastScanResult.SessionType,
				StartFrame = LastScanResult.StartFrame,
				EndFrame = LastScanResult.EndFrame,
				DurationMinutes = LastScanResult.DurationSeconds / 60.0,
				Events = LastScanResult.Events
			};

			// Add driver summaries
			if (_viewModel.Drivers != null)
			{
				foreach (var driver in _viewModel.Drivers.Where(d => d.TrackSurface != TrackSurfaces.NotInWorld))
				{
					summary.Drivers.Add(new DriverSummary
					{
						NumberRaw = driver.NumberRaw,
						TeamName = driver.TeamName,
						StartPosition = 0, // Would need start snapshot
						EndPosition = driver.Position
					});
				}
			}

			// Add camera summaries
			if (_viewModel.Cameras != null)
			{
				foreach (var camera in _viewModel.Cameras)
				{
					summary.AvailableCameras.Add(new CameraSummary
					{
						GroupNum = camera.GroupNum,
						GroupName = camera.GroupName
					});
				}
			}

			return summary;
		}

		public async Task<CameraPlan> GenerateCameraPlanAsync(CancellationToken cancellationToken = default)
		{
			if (LastScanResult == null)
				throw new InvalidOperationException("Must scan replay before generating plan");

			State = AIDirectorState.GeneratingPlan;
			StatusMessage = "Generating camera plan...";

			try
			{
				var provider = GetCurrentProvider();

				if (!provider.IsConfigured)
				{
					throw new InvalidOperationException($"{provider.Name} is not configured. Please check settings.");
				}

				var summary = BuildEventSummary();
				GeneratedPlan = await provider.GenerateCameraPlanAsync(summary, cancellationToken);

				StatusMessage = $"Plan generated: {GeneratedPlan.CameraActions.Count} camera actions";
				State = AIDirectorState.Idle;

				return GeneratedPlan;
			}
			catch (Exception ex)
			{
				StatusMessage = $"Plan generation error: {ex.Message}";
				State = AIDirectorState.Error;
				throw;
			}
		}

		public int ApplyPlanToNodeCollection(bool clearExisting = true)
		{
			if (GeneratedPlan == null || GeneratedPlan.CameraActions.Count == 0)
				return 0;

			State = AIDirectorState.ApplyingPlan;
			StatusMessage = "Applying camera plan...";

			try
			{
				if (clearExisting)
				{
					_viewModel.NodeCollection.RemoveAllNodes();
				}

				int nodesCreated = 0;

				foreach (var action in GeneratedPlan.CameraActions.OrderBy(a => a.Frame))
				{
					// Find driver by number
					var driver = _viewModel.Drivers.FirstOrDefault(d => d.NumberRaw == action.DriverNumber);
					if (driver == null)
					{
						// Try to find any driver if specified one not found
						driver = _viewModel.Drivers.FirstOrDefault();
						if (driver == null) continue;
					}

					// Find camera by name
					var camera = _viewModel.Cameras.FirstOrDefault(c =>
						c.GroupName.Equals(action.CameraName, StringComparison.OrdinalIgnoreCase));
					if (camera == null)
					{
						// Try partial match
						camera = _viewModel.Cameras.FirstOrDefault(c =>
							c.GroupName.IndexOf(action.CameraName, StringComparison.OrdinalIgnoreCase) >= 0);
					}
					if (camera == null)
					{
						// Use first available camera as fallback
						camera = _viewModel.Cameras.FirstOrDefault();
						if (camera == null) continue;
					}

					// Create and add node
					var node = new CamChangeNode(true, action.Frame, driver, camera);
					_viewModel.NodeCollection.AddNode(node);
					nodesCreated++;
				}

				StatusMessage = $"Applied {nodesCreated} camera nodes";
				State = AIDirectorState.Idle;

				return nodesCreated;
			}
			catch (Exception ex)
			{
				StatusMessage = $"Apply error: {ex.Message}";
				State = AIDirectorState.Error;
				throw;
			}
		}

		public void ClearResults()
		{
			LastScanResult = null;
			GeneratedPlan = null;
			StatusMessage = "Ready";
			State = AIDirectorState.Idle;
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
