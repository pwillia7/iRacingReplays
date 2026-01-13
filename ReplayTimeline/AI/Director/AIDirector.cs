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
				// Reset driver selection tracking for fresh variety in the new plan
				ResetDriverSelectionTracking();

				if (clearExisting)
				{
					_viewModel.NodeCollection.RemoveAllNodes();
				}

				int nodesCreated = 0;

				foreach (var action in GeneratedPlan.CameraActions.OrderBy(a => a.Frame))
				{
					// Find the most interesting driver at this frame based on events
					Driver driver = FindMostExcitingDriver(action.Frame);

					if (driver == null)
					{
						// Fallback to first available driver
						driver = _viewModel.Drivers.FirstOrDefault(d => d.TrackSurface != TrackSurfaces.NotInWorld);
						if (driver == null)
						{
							driver = _viewModel.Drivers.FirstOrDefault();
						}
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

		// Track recently selected drivers to ensure variety
		private List<int> _recentlySelectedDrivers = new List<int>();
		private const int MaxRecentDrivers = 8;

		// Track total selections per driver during plan application for balanced coverage
		private Dictionary<int, int> _driverSelectionCounts = new Dictionary<int, int>();
		private int _totalSelectionsInPlan = 0;

		// Random for tie-breaking when scores are close
		private readonly Random _random = new Random();

		/// <summary>
		/// Resets driver selection tracking. Call this before applying a new plan.
		/// </summary>
		public void ResetDriverSelectionTracking()
		{
			_recentlySelectedDrivers.Clear();
			_driverSelectionCounts.Clear();
			_totalSelectionsInPlan = 0;
		}

		/// <summary>
		/// Find the most exciting driver at a given frame using a comprehensive scoring system.
		///
		/// SCORING FACTORS (in order of evaluation):
		///
		/// 1. EVENT PROXIMITY SCORE (0-60 points)
		///    - Events happening AT or NEAR this frame give the highest scores
		///    - Score decays with distance from the event
		///    - Uses a tighter 8-second window (480 frames) for more targeted selection
		///
		/// 2. EVENT TYPE WEIGHTS:
		///    - Incidents: 60 points (most dramatic, must show)
		///    - Battles: 45 points (exciting close racing)
		///    - Overtakes: 40 points (position changes are key)
		///    - Race Start/Finish: 35 points (milestone moments)
		///    - Pit Stops: 15 points (strategic interest)
		///
		/// 3. EVENT IMPORTANCE SCORE (0-10 points)
		///    - Added from event detector's analysis (position importance, duration)
		///
		/// 4. POSITION IMPORTANCE (0-15 points)
		///    - Baseline interest based on running position
		///    - Only applied when no strong event is happening
		///    - Leader: 15, P2: 12, P3: 10, Top 5: 8, Top 10: 5, Midfield: 2
		///
		/// 5. VARIETY PENALTY (-80 to -10 points)
		///    - Exponential decay penalty for recently shown drivers
		///    - Most recent: -80, then -50, -30, -20, -15, -12, -10, -8
		///    - Strong enough to override event scores in most cases
		///
		/// 6. OVEREXPOSURE PENALTY (0 to -40 points)
		///    - Penalizes drivers shown more than their fair share
		///    - Fair share = total selections / number of active drivers
		///    - Each selection over fair share: -10 points
		///
		/// 7. FIELD DIVERSITY BONUS (0-25 points)
		///    - Every 5th selection, boost midfield/back drivers
		///    - Ensures broadcast shows different parts of the field
		///
		/// 8. TIE-BREAKER (random selection among top scorers within 5%)
		///    - When multiple drivers score similarly, randomly pick one
		///    - Adds natural variety to the broadcast
		/// </summary>
		private Driver FindMostExcitingDriver(int frame)
		{
			var activeDrivers = _viewModel.Drivers
				.Where(d => d != null && d.TrackSurface != TrackSurfaces.NotInWorld)
				.ToList();

			if (!activeDrivers.Any())
				return _viewModel.Drivers.FirstOrDefault();

			// Calculate scores for all active drivers
			var driverScores = new Dictionary<int, DriverScore>();

			foreach (var driver in activeDrivers)
			{
				driverScores[driver.NumberRaw] = new DriverScore
				{
					DriverNumber = driver.NumberRaw,
					Driver = driver,
					BaseScore = 0,
					EventScore = 0,
					PositionScore = 0,
					VarietyPenalty = 0,
					Breakdown = new List<string>()
				};
			}

			// 1. Score based on events (highest priority)
			bool hasStrongEvent = false;
			if (LastScanResult?.Events != null)
			{
				hasStrongEvent = ScoreDriversFromEvents(frame, driverScores);
			}

			// 2. Add position-based scoring (reduced weight when events are happening)
			foreach (var driver in activeDrivers)
			{
				var score = driverScores[driver.NumberRaw];
				int positionScore = CalculatePositionScore(driver.Position);

				// Reduce position score weight when a strong event is happening
				// This ensures event participants get priority over position-holders
				if (hasStrongEvent && score.EventScore < 20)
				{
					positionScore = positionScore / 3; // Heavily reduce for non-event drivers
				}

				score.PositionScore = positionScore;
				score.BaseScore += positionScore;
				if (positionScore > 0)
					score.Breakdown.Add($"Position P{driver.Position}: +{positionScore}");
			}

			// 3. Apply variety penalty for recently shown drivers (strong penalty)
			ApplyVarietyPenalties(driverScores);

			// 4. Apply overexposure penalty for drivers shown too often
			ApplyOverexposurePenalty(driverScores, activeDrivers.Count);

			// 5. Apply field diversity bonus (every 5th selection, boost non-front-runners)
			ApplyFieldDiversityBonus(driverScores, activeDrivers);

			// 6. Select from top scorers with tie-breaking
			var bestDriver = SelectWithTieBreaking(driverScores);

			if (bestDriver?.Driver != null)
			{
				// Update tracking
				UpdateRecentlySelected(bestDriver.DriverNumber);
				UpdateSelectionCount(bestDriver.DriverNumber);
				return bestDriver.Driver;
			}

			// Ultimate fallback - rotate through positions
			return GetFallbackDriver(activeDrivers);
		}

		private bool ScoreDriversFromEvents(int frame, Dictionary<int, DriverScore> driverScores)
		{
			// Tighter window: 8 seconds each direction (480 frames at 60fps)
			int frameWindow = 480;
			bool hasStrongEvent = false;

			var relevantEvents = LastScanResult.Events
				.Where(e => {
					// Event is near this frame, OR this frame is during the event duration
					int eventEndFrame = e.Frame + e.DurationFrames;
					return (frame >= e.Frame - frameWindow && frame <= e.Frame + frameWindow) ||
					       (frame >= e.Frame && frame <= eventEndFrame);
				})
				.ToList();

			foreach (var evt in relevantEvents)
			{
				// Calculate proximity score with exponential decay
				int frameDistance = Math.Abs(evt.Frame - frame);
				bool isDuringEvent = frame >= evt.Frame && frame <= evt.Frame + evt.DurationFrames;

				float proximityMultiplier;
				if (isDuringEvent)
				{
					proximityMultiplier = 1.0f; // Full score during event
					hasStrongEvent = true;
				}
				else if (frameDistance <= 120) // Within 2 seconds
				{
					proximityMultiplier = 0.9f;
					hasStrongEvent = true;
				}
				else if (frameDistance <= 300) // Within 5 seconds
				{
					proximityMultiplier = 0.7f;
				}
				else
				{
					// Exponential decay for further distances
					proximityMultiplier = Math.Max(0.1f, (float)Math.Exp(-frameDistance / 300.0));
				}

				// Base score by event type
				int eventTypeScore = GetEventTypeScore(evt.EventType);

				// Calculate final event score
				int eventScore = (int)(eventTypeScore * proximityMultiplier);

				// Add event's importance score (scaled by proximity)
				eventScore += (int)(evt.ImportanceScore * proximityMultiplier);

				// Apply to primary driver
				if (driverScores.ContainsKey(evt.PrimaryDriverNumber))
				{
					driverScores[evt.PrimaryDriverNumber].BaseScore += eventScore;
					driverScores[evt.PrimaryDriverNumber].EventScore += eventScore;
					driverScores[evt.PrimaryDriverNumber].Breakdown.Add(
						$"{evt.EventType} at frame {evt.Frame}: +{eventScore}");
				}

				// Apply to secondary driver (70% for battles/overtakes where both drivers matter)
				if (evt.SecondaryDriverNumber.HasValue && driverScores.ContainsKey(evt.SecondaryDriverNumber.Value))
				{
					int secondaryScore = (int)(eventScore * 0.7f);
					driverScores[evt.SecondaryDriverNumber.Value].BaseScore += secondaryScore;
					driverScores[evt.SecondaryDriverNumber.Value].EventScore += secondaryScore;
					driverScores[evt.SecondaryDriverNumber.Value].Breakdown.Add(
						$"{evt.EventType} (secondary) at frame {evt.Frame}: +{secondaryScore}");
				}
			}

			return hasStrongEvent;
		}

		private int GetEventTypeScore(RaceEventType eventType)
		{
			switch (eventType)
			{
				case RaceEventType.Incident:
					return 60; // Incidents are most dramatic - must show
				case RaceEventType.Battle:
					return 45; // Close racing is exciting
				case RaceEventType.Overtake:
					return 40; // Position changes are important
				case RaceEventType.RaceStart:
				case RaceEventType.RaceFinish:
					return 35; // Race start/finish are key moments
				case RaceEventType.PitStop:
					return 15; // Pit stops can be strategic
				default:
					return 5;
			}
		}

		private int CalculatePositionScore(int position)
		{
			// Reduced baseline scores - events should dominate when they happen
			if (position == 1) return 15;      // Leader
			if (position == 2) return 12;      // Fight for lead
			if (position == 3) return 10;      // Podium battle
			if (position <= 5) return 8;       // Top 5
			if (position <= 10) return 5;      // Top 10
			if (position <= 15) return 2;      // Midfield
			return 1;                          // Back of field
		}

		private void ApplyVarietyPenalties(Dictionary<int, DriverScore> driverScores)
		{
			// Exponential penalty - very strong for most recent, decays for older selections
			int[] penalties = { 80, 50, 30, 20, 15, 12, 10, 8 };

			for (int i = 0; i < _recentlySelectedDrivers.Count && i < penalties.Length; i++)
			{
				int driverNum = _recentlySelectedDrivers[i];
				if (driverScores.ContainsKey(driverNum))
				{
					int penalty = penalties[i];
					driverScores[driverNum].BaseScore -= penalty;
					driverScores[driverNum].VarietyPenalty = penalty;
					driverScores[driverNum].Breakdown.Add($"Recently shown (#{i + 1}): -{penalty}");
				}
			}
		}

		private void ApplyOverexposurePenalty(Dictionary<int, DriverScore> driverScores, int activeDriverCount)
		{
			if (_totalSelectionsInPlan < 3 || activeDriverCount == 0)
				return;

			// Calculate fair share of screen time
			float fairShare = _totalSelectionsInPlan / (float)activeDriverCount;

			foreach (var kvp in _driverSelectionCounts)
			{
				if (driverScores.ContainsKey(kvp.Key))
				{
					int overExposure = (int)(kvp.Value - fairShare);
					if (overExposure > 0)
					{
						int penalty = overExposure * 10; // 10 points per extra selection
						penalty = Math.Min(penalty, 40); // Cap at 40
						driverScores[kvp.Key].BaseScore -= penalty;
						driverScores[kvp.Key].Breakdown.Add($"Overexposure ({kvp.Value} selections): -{penalty}");
					}
				}
			}
		}

		private void ApplyFieldDiversityBonus(Dictionary<int, DriverScore> driverScores, List<Driver> activeDrivers)
		{
			// Every 5th selection, give a bonus to midfield/back drivers
			// This ensures the broadcast shows different parts of the field
			if (_totalSelectionsInPlan > 0 && _totalSelectionsInPlan % 5 == 0)
			{
				foreach (var driver in activeDrivers)
				{
					if (!driverScores.ContainsKey(driver.NumberRaw))
						continue;

					// Bonus for drivers outside top 10 who haven't been shown much
					if (driver.Position > 10)
					{
						int selectCount = _driverSelectionCounts.ContainsKey(driver.NumberRaw)
						? _driverSelectionCounts[driver.NumberRaw] : 0;
						if (selectCount <= 1)
						{
							int bonus = driver.Position > 15 ? 25 : 20;
							driverScores[driver.NumberRaw].BaseScore += bonus;
							driverScores[driver.NumberRaw].Breakdown.Add($"Field diversity bonus: +{bonus}");
						}
					}
				}
			}
		}

		private DriverScore SelectWithTieBreaking(Dictionary<int, DriverScore> driverScores)
		{
			if (!driverScores.Any())
				return null;

			// Sort by score descending
			var sortedScores = driverScores.Values
				.OrderByDescending(s => s.BaseScore)
				.ToList();

			var topScore = sortedScores.First();

			// Find all drivers within 5% of top score (or within 5 points if scores are low)
			int threshold = Math.Max(5, (int)(topScore.BaseScore * 0.05f));
			var tiedDrivers = sortedScores
				.Where(s => topScore.BaseScore - s.BaseScore <= threshold)
				.ToList();

			// Randomly select from tied drivers
			if (tiedDrivers.Count > 1)
			{
				return tiedDrivers[_random.Next(tiedDrivers.Count)];
			}

			return topScore;
		}

		private void UpdateRecentlySelected(int driverNumber)
		{
			// Remove if already in list
			_recentlySelectedDrivers.Remove(driverNumber);

			// Add to front
			_recentlySelectedDrivers.Insert(0, driverNumber);

			// Trim to max size
			while (_recentlySelectedDrivers.Count > MaxRecentDrivers)
			{
				_recentlySelectedDrivers.RemoveAt(_recentlySelectedDrivers.Count - 1);
			}
		}

		private void UpdateSelectionCount(int driverNumber)
		{
			if (!_driverSelectionCounts.ContainsKey(driverNumber))
				_driverSelectionCounts[driverNumber] = 0;

			_driverSelectionCounts[driverNumber]++;
			_totalSelectionsInPlan++;
		}

		private Driver GetFallbackDriver(List<Driver> activeDrivers)
		{
			// Fallback: cycle through positions, avoiding recently selected
			var orderedDrivers = activeDrivers
				.Where(d => d.Position > 0)
				.OrderBy(d => d.Position)
				.ToList();

			// Find a driver not recently shown
			var notRecent = orderedDrivers
				.FirstOrDefault(d => !_recentlySelectedDrivers.Contains(d.NumberRaw));

			if (notRecent != null)
			{
				UpdateRecentlySelected(notRecent.NumberRaw);
				UpdateSelectionCount(notRecent.NumberRaw);
				return notRecent;
			}

			// If all were recently shown, pick from the back of the field for variety
			var backRunner = orderedDrivers.LastOrDefault();
			if (backRunner != null)
			{
				UpdateRecentlySelected(backRunner.NumberRaw);
				UpdateSelectionCount(backRunner.NumberRaw);
				return backRunner;
			}

			return activeDrivers.FirstOrDefault();
		}

		// Helper class to track driver scoring
		private class DriverScore
		{
			public int DriverNumber { get; set; }
			public Driver Driver { get; set; }
			public int BaseScore { get; set; }
			public int EventScore { get; set; }
			public int PositionScore { get; set; }
			public int VarietyPenalty { get; set; }
			public List<string> Breakdown { get; set; }
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
