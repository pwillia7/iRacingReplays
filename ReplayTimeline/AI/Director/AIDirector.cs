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
			private set
			{
				_state = value;
				SafeRaisePropertyChanged("State");
				SafeRaisePropertyChanged("IsBusy");
			}
		}

		public bool IsBusy => State != AIDirectorState.Idle && State != AIDirectorState.Error;

		private int _scanProgress;
		public int ScanProgress
		{
			get { return _scanProgress; }
			private set { _scanProgress = value; SafeRaisePropertyChanged("ScanProgress"); }
		}

		private string _statusMessage = "Ready";
		public string StatusMessage
		{
			get { return _statusMessage; }
			set { _statusMessage = value; SafeRaisePropertyChanged("StatusMessage"); }
		}

		private ReplayScanResult _lastScanResult;
		public ReplayScanResult LastScanResult
		{
			get { return _lastScanResult; }
			private set
			{
				_lastScanResult = value;
				SafeRaisePropertyChanged("LastScanResult");
				SafeRaisePropertyChanged("HasScanResult");
			}
		}

		public bool HasScanResult => LastScanResult != null && LastScanResult.Events.Count > 0;

		private CameraPlan _generatedPlan;
		public CameraPlan GeneratedPlan
		{
			get { return _generatedPlan; }
			private set
			{
				_generatedPlan = value;
				SafeRaisePropertyChanged("GeneratedPlan");
				SafeRaisePropertyChanged("HasGeneratedPlan");
			}
		}

		public bool HasGeneratedPlan => GeneratedPlan != null && GeneratedPlan.CameraActions.Count > 0;

		private void SafeRaisePropertyChanged(string propertyName)
		{
			if (Application.Current?.Dispatcher == null)
			{
				OnPropertyChanged(propertyName);
				return;
			}

			if (Application.Current.Dispatcher.CheckAccess())
			{
				OnPropertyChanged(propertyName);
			}
			else
			{
				Application.Current.Dispatcher.BeginInvoke(new Action(() => OnPropertyChanged(propertyName)));
			}
		}

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
			int originalFrame = startFrame;

			try
			{
				// Verify we can access the dispatcher first
				if (Application.Current?.Dispatcher == null)
				{
					StatusMessage = "Cannot access UI dispatcher";
					State = AIDirectorState.Error;
					return null;
				}

				// Validate connection on UI thread
				bool isConnected = false;
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					isConnected = Sim.Instance != null && Sim.Instance.Sdk != null && Sim.Instance.SessionInfo != null;
					if (isConnected)
					{
						originalFrame = _viewModel.CurrentFrame;
					}
				});

				if (!isConnected)
				{
					StatusMessage = "iRacing is not connected";
					State = AIDirectorState.Error;
					return null;
				}

				// Get track and session info on UI thread
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
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
				});

				int totalFrames = endFrame - startFrame;
				if (totalFrames <= 0)
				{
					StatusMessage = "Invalid frame range";
					State = AIDirectorState.Error;
					return null;
				}

				int frameStep = Settings.ScanIntervalFrames;
				int framesProcessed = 0;

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
					try
					{
						if (!ShouldRunDetector(detector))
							continue;

						var events = detector.DetectEvents(snapshots);
						if (events != null)
						{
							result.Events.AddRange(events);
						}
					}
					catch
					{
						// Skip detector if it fails
						continue;
					}
				}

				// Sort events by frame
				try
				{
					result.Events = result.Events.OrderBy(e => e.Frame).ToList();
				}
				catch
				{
					// Keep unsorted if sorting fails
				}

				// Return to original position on UI thread
				try
				{
					if (Application.Current?.Dispatcher != null)
					{
						await Application.Current.Dispatcher.InvokeAsync(() =>
						{
							try
							{
								if (Sim.Instance?.Sdk?.Replay != null)
								{
									Sim.Instance.Sdk.Replay.SetPosition(originalFrame);
								}
							}
							catch { }
						});
					}
				}
				catch { }

				LastScanResult = result;
				StatusMessage = $"Scan complete: {result.Events.Count} events detected";
				State = AIDirectorState.Idle;

				return result;
			}
			catch (Exception ex)
			{
				StatusMessage = $"Scan error: {ex.Message}";
				State = AIDirectorState.Error;
				return null;
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

				// Create a safe copy of the drivers list to avoid collection modified exceptions
				var driversCopy = _viewModel.Drivers.ToList();

				var snapshot = new TelemetrySnapshot
				{
					Frame = frame,
					SessionTime = _viewModel.SessionTime,
					DriverStates = new List<DriverSnapshot>()
				};

				foreach (var driver in driversCopy)
				{
					if (driver == null)
						continue;

					try
					{
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
					catch
					{
						// Skip this driver if any property access fails
						continue;
					}
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

			// Add driver summaries - use ToList() to avoid collection modified exception
			try
			{
				if (_viewModel?.Drivers != null)
				{
					var driversCopy = _viewModel.Drivers.ToList();
					foreach (var driver in driversCopy.Where(d => d != null && d.TrackSurface != TrackSurfaces.NotInWorld))
					{
						summary.Drivers.Add(new DriverSummary
						{
							NumberRaw = driver.NumberRaw,
							TeamName = driver.TeamName ?? string.Empty,
							StartPosition = 0,
							EndPosition = driver.Position
						});
					}
				}
			}
			catch { }

			// Add camera summaries - use ToList() to avoid collection modified exception
			try
			{
				if (_viewModel?.Cameras != null)
				{
					var camerasCopy = _viewModel.Cameras.ToList();
					foreach (var camera in camerasCopy)
					{
						if (camera == null) continue;
						summary.AvailableCameras.Add(new CameraSummary
						{
							GroupNum = camera.GroupNum,
							GroupName = camera.GroupName ?? string.Empty
						});
					}
				}
			}
			catch { }

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

				// Create a special "Most Exciting" driver with NumberRaw = -1
				// iRacing SDK uses -1 to automatically follow the most exciting action
				var mostExcitingDriver = new Driver
				{
					Id = -1,
					NumberRaw = -1,
					Number = "-1",
					Name = "Most Exciting",
					TeamName = "Most Exciting"
				};

				int nodesCreated = 0;

				foreach (var action in GeneratedPlan.CameraActions.OrderBy(a => a.Frame))
				{
					// Use "Most Exciting" driver for automatic driver selection
					// If action specifies a driver (legacy support), try to find it; otherwise use most exciting
					Driver driver;
					if (action.DriverNumber > 0)
					{
						driver = _viewModel.Drivers.FirstOrDefault(d => d.NumberRaw == action.DriverNumber);
						if (driver == null)
						{
							driver = mostExcitingDriver;
						}
					}
					else
					{
						// Use most exciting driver (default behavior)
						driver = mostExcitingDriver;
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

		// Helper methods for synchronous scanning from command
		public void SetScanning()
		{
			State = AIDirectorState.Scanning;
			StatusMessage = "Scanning replay...";
			ScanProgress = 0;
		}

		public void UpdateProgress(int progress, string message)
		{
			ScanProgress = progress;
			StatusMessage = message;
		}

		public void RunDetectors(ReplayScanResult result, List<TelemetrySnapshot> snapshots)
		{
			try
			{
				foreach (var detector in _eventDetectors)
				{
					try
					{
						if (!ShouldRunDetector(detector))
							continue;

						var events = detector.DetectEvents(snapshots);
						if (events != null)
						{
							result.Events.AddRange(events);
						}
					}
					catch { }
				}

				// Sort events by frame
				try
				{
					result.Events = result.Events.OrderBy(e => e.Frame).ToList();
				}
				catch { }
			}
			catch { }
		}

		public void SetScanComplete(ReplayScanResult result)
		{
			LastScanResult = result;
			StatusMessage = $"Scan complete: {result.Events.Count} events detected";
			State = AIDirectorState.Idle;
		}

		public void SetError(string message)
		{
			StatusMessage = message;
			State = AIDirectorState.Error;
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
