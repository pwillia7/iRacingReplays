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
			// Filter out excluded cameras based on settings
			try
			{
				if (_viewModel?.Cameras != null)
				{
					var camerasCopy = _viewModel.Cameras.ToList();
					foreach (var camera in camerasCopy)
					{
						if (camera == null) continue;
						// Skip excluded cameras
						if (Settings.IsCameraExcluded(camera.GroupName ?? string.Empty))
							continue;
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

		// Maximum segment duration in frames (10 minutes at 60fps = 36000 frames)
		private const int MaxSegmentFrames = 36000;

		public async Task<CameraPlan> GenerateCameraPlanAsync(CancellationToken cancellationToken = default)
		{
			if (LastScanResult == null)
				throw new InvalidOperationException("Must scan replay before generating plan");

			State = AIDirectorState.GeneratingPlan;
			StatusMessage = "Generating camera plan...";

			try
			{
				// Check if AI is disabled - use event-driven local generation
				if (!Settings.UseAIForCameraPlan)
				{
					GeneratedPlan = GenerateEventDrivenCameraPlan();
					StatusMessage = $"Event-driven plan generated: {GeneratedPlan.CameraActions.Count} camera actions";
					State = AIDirectorState.Idle;
					return GeneratedPlan;
				}

				var provider = GetCurrentProvider();

				if (!provider.IsConfigured)
				{
					throw new InvalidOperationException($"{provider.Name} is not configured. Please check settings.");
				}

				var fullSummary = BuildEventSummary();
				int totalFrames = fullSummary.EndFrame - fullSummary.StartFrame;

				// Check if we need to chunk the replay
				if (totalFrames > MaxSegmentFrames)
				{
					GeneratedPlan = await GenerateChunkedPlanAsync(provider, fullSummary, cancellationToken);
				}
				else
				{
					// Short replay - generate in one call
					GeneratedPlan = await provider.GenerateCameraPlanAsync(fullSummary, cancellationToken);
				}

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

		/// <summary>
		/// Generate an event-driven camera plan that switches cameras before events happen.
		/// </summary>
		private CameraPlan GenerateEventDrivenCameraPlan()
		{
			var plan = new CameraPlan
			{
				GeneratedBy = "Event-Driven",
				GeneratedAt = DateTime.Now,
				TotalDurationFrames = LastScanResult.EndFrame - LastScanResult.StartFrame
			};

			// Get available cameras (not excluded)
			var availableCameras = _viewModel.Cameras
				.Where(c => c != null && !Settings.IsCameraExcluded(c.GroupName))
				.Select(c => c.GroupName)
				.ToList();

			if (!availableCameras.Any())
			{
				availableCameras = _viewModel.Cameras
					.Where(c => c != null)
					.Select(c => c.GroupName)
					.ToList();
			}

			if (!availableCameras.Any())
				return plan;

			const int fps = 60;
			int anticipationFrames = Settings.EventAnticipationSeconds * fps;
			int minFramesBetweenCuts = Settings.MinSecondsBetweenCuts * fps;
			int maxFramesBetweenCuts = Settings.MaxSecondsBetweenCuts * fps;

			// Sort events by frame and filter to significant events
			var significantEvents = LastScanResult.Events
				.Where(e => e.EventType == RaceEventType.Incident ||
				           e.EventType == RaceEventType.Overtake ||
				           (e.EventType == RaceEventType.Battle && e.ImportanceScore >= 6))
				.OrderBy(e => e.Frame)
				.ToList();

			// Track scheduled cut frames to avoid duplicates and respect minimum spacing
			var scheduledCuts = new List<ScheduledCut>();
			int lastCutFrame = LastScanResult.StartFrame - minFramesBetweenCuts;
			string lastCamera = null;

			// Add opening cut at start
			string openingCamera = SelectCameraForContext("opening", availableCameras, null);
			scheduledCuts.Add(new ScheduledCut
			{
				Frame = LastScanResult.StartFrame,
				CameraName = openingCamera,
				Reason = "Opening shot",
				EventType = null,
				PrimaryDriverNumber = 0
			});
			lastCutFrame = LastScanResult.StartFrame;
			lastCamera = openingCamera;

			// Schedule cuts for each significant event
			foreach (var evt in significantEvents)
			{
				// Calculate the frame to switch TO this event (before it happens)
				int cutFrame = evt.Frame - anticipationFrames;

				// Ensure cut frame is within bounds
				if (cutFrame < LastScanResult.StartFrame)
					cutFrame = LastScanResult.StartFrame;
				if (cutFrame >= LastScanResult.EndFrame)
					continue;

				// Check minimum spacing from last cut
				if (cutFrame - lastCutFrame < minFramesBetweenCuts)
				{
					// Event is too close to last cut - skip or merge
					// But if this is a high-importance event (incident), try to fit it in
					if (evt.EventType == RaceEventType.Incident && evt.ImportanceScore >= 8)
					{
						cutFrame = lastCutFrame + minFramesBetweenCuts;
						if (cutFrame >= evt.Frame + evt.DurationFrames)
							continue; // Too late, skip this event
					}
					else
					{
						continue;
					}
				}

				// Select appropriate camera for this event type
				string camera = SelectCameraForEvent(evt, availableCameras, lastCamera);

				scheduledCuts.Add(new ScheduledCut
				{
					Frame = cutFrame,
					CameraName = camera,
					Reason = evt.Description,
					EventType = evt.EventType,
					PrimaryDriverNumber = evt.PrimaryDriverNumber,
					SecondaryDriverNumber = evt.SecondaryDriverNumber
				});

				lastCutFrame = cutFrame;
				lastCamera = camera;
			}

			// Fill gaps where there are no events
			FillGapsWithCuts(scheduledCuts, LastScanResult.StartFrame, LastScanResult.EndFrame,
				maxFramesBetweenCuts, minFramesBetweenCuts, availableCameras);

			// Sort by frame and convert to camera actions
			// NOTE: We intentionally do NOT set DriverNumber here.
			// Driver selection is handled by FindMostExcitingDriver() in ApplyPlanToNodeCollection,
			// which considers variety penalties, event proximity scoring, momentum, etc.
			// The event proximity scoring will naturally boost drivers involved in events at each frame.
			foreach (var cut in scheduledCuts.OrderBy(c => c.Frame))
			{
				plan.CameraActions.Add(new CameraAction
				{
					Frame = cut.Frame,
					CameraName = cut.CameraName,
					Reason = cut.Reason
				});
			}

			return plan;
		}

		private class ScheduledCut
		{
			public int Frame { get; set; }
			public string CameraName { get; set; }
			public string Reason { get; set; }
			public RaceEventType? EventType { get; set; }
			public int PrimaryDriverNumber { get; set; }
			public int? SecondaryDriverNumber { get; set; }
		}

		/// <summary>
		/// Select an appropriate camera based on event type.
		/// </summary>
		private string SelectCameraForEvent(RaceEvent evt, List<string> availableCameras, string lastCamera)
		{
			// Camera preferences by event type
			string[] preferredCameras;

			switch (evt.EventType)
			{
				case RaceEventType.Incident:
					// For incidents, prefer wide angles that show the whole scene
					preferredCameras = new[] { "TV1", "TV2", "TV3", "Chopper", "Blimp", "Chase", "Far Chase" };
					break;

				case RaceEventType.Overtake:
					// For overtakes, prefer chase cameras to follow the action
					preferredCameras = new[] { "Chase", "Far Chase", "TV1", "TV2", "Rear Chase", "Cockpit" };
					break;

				case RaceEventType.Battle:
					// For battles, mix of chase and TV angles
					preferredCameras = new[] { "Chase", "TV1", "TV2", "Far Chase", "Nose", "Cockpit" };
					break;

				default:
					preferredCameras = new[] { "TV1", "Chase", "TV2", "Cockpit" };
					break;
			}

			// Find first preferred camera that's available and not the last one used
			foreach (var pref in preferredCameras)
			{
				var match = availableCameras.FirstOrDefault(c =>
					c.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0 && c != lastCamera);
				if (match != null)
					return match;
			}

			// Fallback: any camera different from last
			var different = availableCameras.Where(c => c != lastCamera).ToList();
			if (different.Any())
				return different[_random.Next(different.Count)];

			return availableCameras[_random.Next(availableCameras.Count)];
		}

		/// <summary>
		/// Select camera for non-event contexts (opening, gap filler).
		/// </summary>
		private string SelectCameraForContext(string context, List<string> availableCameras, string lastCamera)
		{
			string[] preferredCameras;

			if (context == "opening")
			{
				// Wide establishing shots for opening
				preferredCameras = new[] { "Chopper", "Blimp", "TV1", "TV2", "TV3" };
			}
			else
			{
				// Gap fillers - variety of angles
				preferredCameras = new[] { "TV1", "TV2", "Chase", "Far Chase", "Cockpit", "Chopper" };
			}

			foreach (var pref in preferredCameras)
			{
				var match = availableCameras.FirstOrDefault(c =>
					c.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0 && c != lastCamera);
				if (match != null)
					return match;
			}

			var different = availableCameras.Where(c => c != lastCamera).ToList();
			if (different.Any())
				return different[_random.Next(different.Count)];

			return availableCameras[_random.Next(availableCameras.Count)];
		}

		/// <summary>
		/// Fill long gaps between events with regular interval cuts.
		/// </summary>
		private void FillGapsWithCuts(List<ScheduledCut> cuts, int startFrame, int endFrame,
			int maxGapFrames, int minGapFrames, List<string> availableCameras)
		{
			// Sort existing cuts
			var sortedCuts = cuts.OrderBy(c => c.Frame).ToList();
			var newCuts = new List<ScheduledCut>();

			int previousFrame = startFrame;
			string lastCamera = sortedCuts.FirstOrDefault()?.CameraName;

			foreach (var cut in sortedCuts)
			{
				int gap = cut.Frame - previousFrame;

				// If gap is too long, add filler cuts
				while (gap > maxGapFrames)
				{
					int fillerFrame = previousFrame + maxGapFrames;
					if (fillerFrame >= cut.Frame - minGapFrames)
						break; // Don't add filler too close to next event cut

					string fillerCamera = SelectCameraForContext("filler", availableCameras, lastCamera);
					newCuts.Add(new ScheduledCut
					{
						Frame = fillerFrame,
						CameraName = fillerCamera,
						Reason = "Field coverage",
						EventType = null,
						PrimaryDriverNumber = 0
					});

					lastCamera = fillerCamera;
					previousFrame = fillerFrame;
					gap = cut.Frame - previousFrame;
				}

				previousFrame = cut.Frame;
				lastCamera = cut.CameraName;
			}

			// Fill gap at the end if needed
			int lastEventFrame = sortedCuts.LastOrDefault()?.Frame ?? startFrame;
			while (endFrame - lastEventFrame > maxGapFrames)
			{
				int fillerFrame = lastEventFrame + maxGapFrames;
				if (fillerFrame >= endFrame)
					break;

				string fillerCamera = SelectCameraForContext("filler", availableCameras, lastCamera);
				newCuts.Add(new ScheduledCut
				{
					Frame = fillerFrame,
					CameraName = fillerCamera,
					Reason = "Field coverage",
					EventType = null,
					PrimaryDriverNumber = 0
				});

				lastCamera = fillerCamera;
				lastEventFrame = fillerFrame;
			}

			cuts.AddRange(newCuts);
		}

		private async Task<CameraPlan> GenerateChunkedPlanAsync(ILLMProvider provider, RaceEventSummary fullSummary, CancellationToken cancellationToken)
		{
			var combinedPlan = new CameraPlan
			{
				GeneratedBy = $"{provider.Name} ({provider.ModelName})",
				GeneratedAt = DateTime.Now,
				TotalDurationFrames = fullSummary.TotalFrames
			};

			int totalFrames = fullSummary.EndFrame - fullSummary.StartFrame;
			int numSegments = (int)Math.Ceiling(totalFrames / (double)MaxSegmentFrames);

			StatusMessage = $"Generating camera plan in {numSegments} segments...";

			for (int i = 0; i < numSegments; i++)
			{
				if (cancellationToken.IsCancellationRequested)
					break;

				int segmentStart = fullSummary.StartFrame + (i * MaxSegmentFrames);
				int segmentEnd = Math.Min(segmentStart + MaxSegmentFrames, fullSummary.EndFrame);

				StatusMessage = $"Generating segment {i + 1} of {numSegments}...";

				// Create a summary for just this segment
				var segmentSummary = CreateSegmentSummary(fullSummary, segmentStart, segmentEnd);

				try
				{
					var segmentPlan = await provider.GenerateCameraPlanAsync(segmentSummary, cancellationToken);

					if (segmentPlan?.CameraActions != null)
					{
						// Add all actions from this segment to the combined plan
						foreach (var action in segmentPlan.CameraActions)
						{
							// Ensure the action frame is within the segment bounds
							if (action.Frame >= segmentStart && action.Frame <= segmentEnd)
							{
								combinedPlan.CameraActions.Add(action);
							}
						}
					}
				}
				catch (Exception ex)
				{
					// Log but continue with other segments
					System.Diagnostics.Debug.WriteLine($"Segment {i + 1} generation failed: {ex.Message}");

					// If we have no actions yet and first segment failed, rethrow
					if (combinedPlan.CameraActions.Count == 0 && i == 0)
						throw;
				}

				// Small delay between segments to avoid rate limiting
				if (i < numSegments - 1)
				{
					await Task.Delay(500, cancellationToken);
				}
			}

			// Sort all actions by frame
			combinedPlan.CameraActions = combinedPlan.CameraActions.OrderBy(a => a.Frame).ToList();

			return combinedPlan;
		}

		private RaceEventSummary CreateSegmentSummary(RaceEventSummary fullSummary, int segmentStart, int segmentEnd)
		{
			int segmentFrames = segmentEnd - segmentStart;
			double segmentDurationMinutes = (segmentFrames / 60.0) / 60.0; // frames / fps / 60

			var segmentSummary = new RaceEventSummary
			{
				TrackName = fullSummary.TrackName,
				SessionType = fullSummary.SessionType,
				StartFrame = segmentStart,
				EndFrame = segmentEnd,
				DurationMinutes = segmentDurationMinutes,
				FrameRate = fullSummary.FrameRate,
				Drivers = fullSummary.Drivers,
				AvailableCameras = fullSummary.AvailableCameras
			};

			// Filter events to only those within this segment (with a small buffer for context)
			int bufferFrames = 600; // 10 seconds buffer
			segmentSummary.Events = fullSummary.Events
				.Where(e => e.Frame >= (segmentStart - bufferFrames) && e.Frame <= (segmentEnd + bufferFrames))
				.ToList();

			return segmentSummary;
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
					// This considers variety penalties, event proximity, momentum, pack racing, etc.
					Driver driver = FindMostExcitingDriver(action.Frame);

					if (driver == null)
					{
						// Fallback to first available driver (exclude pace car)
						driver = _viewModel.Drivers.FirstOrDefault(d => d.TrackSurface != TrackSurfaces.NotInWorld && d.NumberRaw != 0);
						if (driver == null)
						{
							driver = _viewModel.Drivers.FirstOrDefault(d => d.NumberRaw != 0);
						}
						if (driver == null) continue;
					}

					// Find camera by name (excluding cameras based on settings)
					var camera = _viewModel.Cameras.FirstOrDefault(c =>
						c.GroupName.Equals(action.CameraName, StringComparison.OrdinalIgnoreCase) &&
						!Settings.IsCameraExcluded(c.GroupName));
					if (camera == null)
					{
						// Try partial match (excluding restricted cameras)
						camera = _viewModel.Cameras.FirstOrDefault(c =>
							c.GroupName.IndexOf(action.CameraName, StringComparison.OrdinalIgnoreCase) >= 0 &&
							!Settings.IsCameraExcluded(c.GroupName));
					}
					if (camera == null)
					{
						// Use first available non-excluded camera as fallback
						camera = _viewModel.Cameras.FirstOrDefault(c =>
							!Settings.IsCameraExcluded(c.GroupName));
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

		// Track position history for momentum detection (driver number -> list of (frame, position))
		private Dictionary<int, List<PositionRecord>> _positionHistory = new Dictionary<int, List<PositionRecord>>();
		private const int MomentumWindowFrames = 3600; // 60 seconds at 60fps

		// Random for tie-breaking when scores are close
		private readonly Random _random = new Random();

		private class PositionRecord
		{
			public int Frame { get; set; }
			public int Position { get; set; }
		}

		/// <summary>
		/// Resets driver selection tracking. Call this before applying a new plan.
		/// </summary>
		public void ResetDriverSelectionTracking()
		{
			_recentlySelectedDrivers.Clear();
			_driverSelectionCounts.Clear();
			_totalSelectionsInPlan = 0;
			BuildPositionHistoryFromScan();
		}

		/// <summary>
		/// Build position history from scan results for momentum tracking.
		/// </summary>
		private void BuildPositionHistoryFromScan()
		{
			_positionHistory.Clear();

			if (LastScanResult?.Snapshots == null)
				return;

			foreach (var snapshot in LastScanResult.Snapshots)
			{
				if (snapshot?.DriverStates == null)
					continue;

				foreach (var driver in snapshot.DriverStates)
				{
					if (driver == null || driver.Position <= 0)
						continue;

					if (!_positionHistory.ContainsKey(driver.NumberRaw))
						_positionHistory[driver.NumberRaw] = new List<PositionRecord>();

					_positionHistory[driver.NumberRaw].Add(new PositionRecord
					{
						Frame = snapshot.Frame,
						Position = driver.Position
					});
				}
			}
		}

		/// <summary>
		/// Find the most exciting driver at a given frame using a comprehensive scoring system.
		///
		/// NEW ALGORITHM DESIGN:
		/// The key insight is that VARIETY SHOULD YIELD TO ACTION, not override it.
		///
		/// TIERED APPROACH:
		/// ┌─────────────────────────────────────────────────────────────────┐
		/// │  TIER 1: CRITICAL ACTION (variety penalty reduced to 20%)       │
		/// │  - Active incident happening NOW (within 2 seconds)             │
		/// │  - Overtake completing NOW                                      │
		/// ├─────────────────────────────────────────────────────────────────┤
		/// │  TIER 2: HIGH INTEREST (variety penalty reduced to 50%)         │
		/// │  - Driver currently in active battle                            │
		/// │  - Driver on a charge (3+ positions gained recently)            │
		/// │  - Driver in a tight pack (3+ cars nearby)                      │
		/// ├─────────────────────────────────────────────────────────────────┤
		/// │  TIER 3: STANDARD INTEREST (full variety penalty)               │
		/// │  - Position-based scoring                                       │
		/// │  - Field diversity                                              │
		/// └─────────────────────────────────────────────────────────────────┘
		///
		/// SCORING COMPONENTS:
		/// 1. Event Proximity Score (0-70) - Incidents, overtakes, battles near this frame
		/// 2. Momentum Bonus (0-40) - Drivers gaining positions over recent frames
		/// 3. Active Battle Bonus (0-35) - Drivers currently in detected battles
		/// 4. Pack Proximity Bonus (0-25) - Drivers running in groups
		/// 5. Fresh Action Bonus (0-20) - Recent position changes
		/// 6. Position Score (0-15) - Baseline interest from running position
		///
		/// VARIETY DAMPENING:
		/// ActionLevel = (EventScore + MomentumBonus + BattleBonus + PackBonus + FreshBonus) / 100
		/// VarietyDampener = 1.0 - (ActionLevel * 0.8)  // At max action: only 20% penalty
		///
		/// FINAL FORMULA:
		/// FinalScore = ActionScore + (PositionScore * (1 - ActionLevel * 0.5))
		///            - (VarietyPenalty * VarietyDampener) - OverexposurePenalty + DiversityBonus
		/// </summary>
		private Driver FindMostExcitingDriver(int frame)
		{
			var activeDrivers = _viewModel.Drivers
				.Where(d => d != null && d.TrackSurface != TrackSurfaces.NotInWorld && d.NumberRaw != 0)
				.ToList();

			if (!activeDrivers.Any())
				return _viewModel.Drivers.FirstOrDefault();

			// Build driver snapshots for pack proximity calculation
			var driverSnapshots = GetDriverSnapshotsAtFrame(frame, activeDrivers);

			// Calculate scores for all active drivers
			var driverScores = new Dictionary<int, DriverScore>();

			foreach (var driver in activeDrivers)
			{
				driverScores[driver.NumberRaw] = new DriverScore
				{
					DriverNumber = driver.NumberRaw,
					Driver = driver,
					Breakdown = new List<string>()
				};
			}

			// === PHASE 1: Calculate all action-based scores ===

			// 1. Event proximity scoring (incidents, overtakes near this frame)
			ScoreDriversFromEvents(frame, driverScores);

			// 2. Active battle bonus (drivers currently in ongoing battles)
			ApplyActiveBattleBonus(frame, driverScores);

			// 3. Momentum bonus (drivers gaining positions)
			ApplyMomentumBonus(frame, driverScores);

			// 4. Pack proximity bonus (drivers in groups)
			ApplyPackProximityBonus(driverSnapshots, driverScores);

			// 5. Fresh action bonus (recent position changes)
			ApplyFreshActionBonus(frame, driverScores);

			// === PHASE 2: Calculate action level for variety dampening ===

			foreach (var score in driverScores.Values)
			{
				// Sum all action-related scores
				score.ActionScore = score.EventScore + score.MomentumBonus +
				                    score.BattleBonus + score.PackBonus + score.FreshActionBonus;

				// Calculate action level (0.0 to 1.0)
				// Use higher threshold (150) so variety remains important
				// Only truly exceptional action (incident + momentum + pack) approaches 1.0
				score.ActionLevel = Math.Min(1.0f, score.ActionScore / 150.0f);
			}

			// === PHASE 3: Apply position scoring (reduced during high action) ===

			foreach (var driver in activeDrivers)
			{
				var score = driverScores[driver.NumberRaw];
				int positionScore = CalculatePositionScore(driver.Position);

				// Reduce position weight when driver has high action (action takes precedence)
				float positionMultiplier = 1.0f - (score.ActionLevel * 0.5f);
				score.PositionScore = (int)(positionScore * positionMultiplier);
				score.BaseScore = score.ActionScore + score.PositionScore;

				if (score.PositionScore > 0)
					score.Breakdown.Add($"Position P{driver.Position}: +{score.PositionScore} (x{positionMultiplier:F2})");
			}

			// === PHASE 4: Apply variety penalty with dampening ===

			ApplyVarietyPenaltiesWithDampening(driverScores);

			// === PHASE 5: Apply overexposure penalty ===

			ApplyOverexposurePenalty(driverScores, activeDrivers.Count);

			// === PHASE 6: Apply field diversity bonus ===

			ApplyFieldDiversityBonus(driverScores, activeDrivers);

			// === PHASE 7: Apply focus driver bonus ===

			ApplyFocusDriverBonus(driverScores);

			// === PHASE 8: Select best driver with tie-breaking ===

			var bestDriver = SelectWithTieBreaking(driverScores);

			if (bestDriver?.Driver != null)
			{
				UpdateRecentlySelected(bestDriver.DriverNumber);
				UpdateSelectionCount(bestDriver.DriverNumber);
				return bestDriver.Driver;
			}

			return GetFallbackDriver(activeDrivers);
		}

		/// <summary>
		/// Get driver position/distance data at or near the specified frame from scan snapshots.
		/// </summary>
		private Dictionary<int, DriverSnapshot> GetDriverSnapshotsAtFrame(int frame, List<Driver> activeDrivers)
		{
			var result = new Dictionary<int, DriverSnapshot>();

			if (LastScanResult?.Snapshots == null || !LastScanResult.Snapshots.Any())
			{
				// Fallback: create from current driver state
				foreach (var driver in activeDrivers)
				{
					result[driver.NumberRaw] = new DriverSnapshot
					{
						NumberRaw = driver.NumberRaw,
						Position = driver.Position,
						LapDistance = driver.LapDistance,
						Lap = driver.Lap,
						TrackSurface = driver.TrackSurface
					};
				}
				return result;
			}

			// Find closest snapshot to this frame
			var closestSnapshot = LastScanResult.Snapshots
				.OrderBy(s => Math.Abs(s.Frame - frame))
				.FirstOrDefault();

			if (closestSnapshot?.DriverStates != null)
			{
				foreach (var ds in closestSnapshot.DriverStates)
				{
					if (ds != null)
						result[ds.NumberRaw] = ds;
				}
			}

			return result;
		}

		/// <summary>
		/// Score drivers based on proximity to detected events.
		/// </summary>
		private void ScoreDriversFromEvents(int frame, Dictionary<int, DriverScore> driverScores)
		{
			if (LastScanResult?.Events == null)
				return;

			// Window: 8 seconds each direction (480 frames at 60fps)
			int frameWindow = 480;

			var relevantEvents = LastScanResult.Events
				.Where(e => {
					int eventEndFrame = e.Frame + e.DurationFrames;
					return (frame >= e.Frame - frameWindow && frame <= e.Frame + frameWindow) ||
					       (frame >= e.Frame && frame <= eventEndFrame);
				})
				.ToList();

			foreach (var evt in relevantEvents)
			{
				int frameDistance = Math.Abs(evt.Frame - frame);
				bool isDuringEvent = frame >= evt.Frame && frame <= evt.Frame + evt.DurationFrames;

				// Calculate proximity multiplier with urgency boost for "right now" events
				float proximityMultiplier;
				if (isDuringEvent || frameDistance <= 60) // Within 1 second = critical
				{
					proximityMultiplier = 1.2f; // Boost for happening RIGHT NOW
				}
				else if (frameDistance <= 120) // Within 2 seconds
				{
					proximityMultiplier = 1.0f;
				}
				else if (frameDistance <= 300) // Within 5 seconds
				{
					proximityMultiplier = 0.7f;
				}
				else
				{
					proximityMultiplier = Math.Max(0.1f, (float)Math.Exp(-frameDistance / 300.0));
				}

				int eventTypeScore = GetEventTypeScore(evt.EventType);
				int eventScore = (int)(eventTypeScore * proximityMultiplier);
				eventScore += (int)(evt.ImportanceScore * proximityMultiplier);

				// Apply to primary driver
				if (driverScores.ContainsKey(evt.PrimaryDriverNumber))
				{
					driverScores[evt.PrimaryDriverNumber].EventScore += eventScore;
					driverScores[evt.PrimaryDriverNumber].Breakdown.Add(
						$"{evt.EventType} at frame {evt.Frame}: +{eventScore}");
				}

				// Apply to secondary driver (80% for battles/overtakes)
				if (evt.SecondaryDriverNumber.HasValue && driverScores.ContainsKey(evt.SecondaryDriverNumber.Value))
				{
					int secondaryScore = (int)(eventScore * 0.8f);
					driverScores[evt.SecondaryDriverNumber.Value].EventScore += secondaryScore;
					driverScores[evt.SecondaryDriverNumber.Value].Breakdown.Add(
						$"{evt.EventType} (secondary): +{secondaryScore}");
				}
			}
		}

		/// <summary>
		/// Apply bonus to drivers currently in active battles (during battle duration).
		/// Uses Settings.BattleWeight to scale the bonus.
		/// </summary>
		private void ApplyActiveBattleBonus(int frame, Dictionary<int, DriverScore> driverScores)
		{
			if (LastScanResult?.Events == null)
				return;

			// Scale based on battle weight setting
			float scale = Settings.BattleWeight / 35.0f;

			var activeBattles = LastScanResult.Events
				.Where(e => e.EventType == RaceEventType.Battle &&
				           frame >= e.Frame && frame <= e.Frame + e.DurationFrames)
				.ToList();

			foreach (var battle in activeBattles)
			{
				// Calculate how far into the battle we are (0.0 to 1.0)
				float battleProgress = (float)(frame - battle.Frame) / Math.Max(1, battle.DurationFrames);

				// Base bonus - moderate so variety still applies
				int baseBonus;
				if (battleProgress > 0.7f) // Last 30% of battle - climax
					baseBonus = 22;
				else if (battleProgress > 0.4f) // Middle of battle
					baseBonus = 18;
				else // Early battle
					baseBonus = 15;

				// Small extra for top battles
				if (battle.Position <= 3)
					baseBonus += 5;
				else if (battle.Position <= 5)
					baseBonus += 3;

				int bonus = (int)(baseBonus * scale);

				// Apply to both drivers in the battle
				if (driverScores.ContainsKey(battle.PrimaryDriverNumber))
				{
					driverScores[battle.PrimaryDriverNumber].BattleBonus += bonus;
					driverScores[battle.PrimaryDriverNumber].Breakdown.Add(
						$"Battle P{battle.Position}: +{bonus}");
				}

				if (battle.SecondaryDriverNumber.HasValue && driverScores.ContainsKey(battle.SecondaryDriverNumber.Value))
				{
					driverScores[battle.SecondaryDriverNumber.Value].BattleBonus += bonus;
					driverScores[battle.SecondaryDriverNumber.Value].Breakdown.Add(
						$"Battle P{battle.Position}: +{bonus}");
				}
			}
		}

		/// <summary>
		/// Apply momentum bonus for drivers gaining multiple positions over recent frames.
		/// Uses Settings.MomentumWeight to scale the bonus.
		/// </summary>
		private void ApplyMomentumBonus(int frame, Dictionary<int, DriverScore> driverScores)
		{
			// Scale based on MomentumWeight setting (default 25, max ~40)
			float scale = Settings.MomentumWeight / 25.0f;

			foreach (var kvp in driverScores)
			{
				int driverNum = kvp.Key;
				var score = kvp.Value;

				if (!_positionHistory.ContainsKey(driverNum))
					continue;

				var history = _positionHistory[driverNum];
				if (history.Count < 2)
					continue;

				// Find position at start of momentum window
				int windowStart = frame - MomentumWindowFrames;
				var startRecord = history
					.Where(r => r.Frame <= windowStart)
					.OrderByDescending(r => r.Frame)
					.FirstOrDefault();

				var currentRecord = history
					.Where(r => r.Frame <= frame)
					.OrderByDescending(r => r.Frame)
					.FirstOrDefault();

				if (startRecord == null || currentRecord == null)
					continue;

				int positionsGained = startRecord.Position - currentRecord.Position;

				if (positionsGained >= 2)
				{
					// Base bonus scales with positions gained
					int baseBonus;
					if (positionsGained >= 5)
						baseBonus = 30;
					else if (positionsGained >= 4)
						baseBonus = 25;
					else if (positionsGained >= 3)
						baseBonus = 18;
					else
						baseBonus = 12;

					// Extra for gaining into top positions
					if (currentRecord.Position <= 3 && startRecord.Position > 3)
						baseBonus += 8;
					else if (currentRecord.Position <= 5 && startRecord.Position > 5)
						baseBonus += 4;

					int bonus = (int)(baseBonus * scale);
					score.MomentumBonus = bonus;
					score.Breakdown.Add($"Momentum (+{positionsGained}): +{bonus}");
				}
			}
		}

		/// <summary>
		/// Apply bonus for drivers running in packs (multiple cars nearby).
		/// Uses Settings.PackWeight to scale the bonus.
		/// </summary>
		private void ApplyPackProximityBonus(Dictionary<int, DriverSnapshot> snapshots, Dictionary<int, DriverScore> driverScores)
		{
			const float PackThreshold = 0.03f; // 3% of lap distance = in pack
			float scale = Settings.PackWeight / 15.0f;

			foreach (var kvp in driverScores)
			{
				int driverNum = kvp.Key;
				var score = kvp.Value;

				if (!snapshots.ContainsKey(driverNum))
					continue;

				var thisDriver = snapshots[driverNum];
				if (thisDriver.TrackSurface != TrackSurfaces.OnTrack)
					continue;

				// Count cars within pack threshold
				int carsNearby = 0;
				foreach (var other in snapshots.Values)
				{
					if (other.NumberRaw == driverNum)
						continue;
					if (other.TrackSurface != TrackSurfaces.OnTrack)
						continue;

					float gap = CalculateLapDistanceGap(thisDriver, other);
					if (gap <= PackThreshold)
						carsNearby++;
				}

				if (carsNearby >= 1)
				{
					// Base bonus scales with pack size
					int baseBonus;
					if (carsNearby >= 4)
						baseBonus = 20;
					else if (carsNearby >= 3)
						baseBonus = 15;
					else if (carsNearby >= 2)
						baseBonus = 10;
					else
						baseBonus = 6;

					int bonus = (int)(baseBonus * scale);
					score.PackBonus = bonus;
					score.Breakdown.Add($"Pack ({carsNearby + 1} cars): +{bonus}");
				}
			}
		}

		private float CalculateLapDistanceGap(DriverSnapshot d1, DriverSnapshot d2)
		{
			if (d1.Lap == d2.Lap)
			{
				return Math.Abs(d1.LapDistance - d2.LapDistance);
			}
			// Different laps - they're not really in a pack
			return 1.0f;
		}

		/// <summary>
		/// Apply bonus for recent position changes.
		/// Uses Settings.FreshActionWeight to scale the bonus.
		/// </summary>
		private void ApplyFreshActionBonus(int frame, Dictionary<int, DriverScore> driverScores)
		{
			if (LastScanResult?.Events == null)
				return;

			float scale = Settings.FreshActionWeight / 15.0f;
			int recentWindow = 1200; // 20 seconds

			var recentOvertakes = LastScanResult.Events
				.Where(e => e.EventType == RaceEventType.Overtake &&
				           frame >= e.Frame && frame <= e.Frame + recentWindow)
				.ToList();

			foreach (var overtake in recentOvertakes)
			{
				int frameSince = frame - overtake.Frame;

				// Base bonus decays over time
				int baseBonus;
				if (frameSince <= 300) // Last 5 seconds
					baseBonus = 15;
				else if (frameSince <= 600) // Last 10 seconds
					baseBonus = 10;
				else if (frameSince <= 900) // Last 15 seconds
					baseBonus = 6;
				else
					baseBonus = 3;

				int bonus = (int)(baseBonus * scale);

				// Apply to primary driver
				if (driverScores.ContainsKey(overtake.PrimaryDriverNumber))
				{
					var score = driverScores[overtake.PrimaryDriverNumber];
					if (bonus > score.FreshActionBonus)
					{
						score.FreshActionBonus = bonus;
						score.Breakdown.Add($"Fresh pass ({frameSince / 60}s): +{bonus}");
					}
				}

				// Apply to secondary driver (got passed)
				if (overtake.SecondaryDriverNumber.HasValue &&
				    driverScores.ContainsKey(overtake.SecondaryDriverNumber.Value))
				{
					var score = driverScores[overtake.SecondaryDriverNumber.Value];
					int secondaryBonus = (int)(bonus * 0.6f);
					if (secondaryBonus > score.FreshActionBonus)
					{
						score.FreshActionBonus = secondaryBonus;
						score.Breakdown.Add($"Got passed ({frameSince / 60}s): +{secondaryBonus}");
					}
				}
			}
		}

		private int GetEventTypeScore(RaceEventType eventType)
		{
			// Use configurable weights from settings
			switch (eventType)
			{
				case RaceEventType.Incident:
					return Settings.IncidentWeight;
				case RaceEventType.Battle:
					return Settings.BattleWeight;
				case RaceEventType.Overtake:
					return Settings.OvertakeWeight;
				case RaceEventType.RaceStart:
				case RaceEventType.RaceFinish:
					return Settings.OvertakeWeight; // Use overtake weight for milestones
				case RaceEventType.PitStop:
					return 15;
				default:
					return 5;
			}
		}

		private int CalculatePositionScore(int position)
		{
			// Scale based on position weight setting (default 15)
			float scale = Settings.PositionWeight / 15.0f;

			int baseScore;
			if (position == 1) baseScore = 15;
			else if (position == 2) baseScore = 12;
			else if (position == 3) baseScore = 10;
			else if (position <= 5) baseScore = 8;
			else if (position <= 10) baseScore = 5;
			else if (position <= 15) baseScore = 2;
			else baseScore = 1;

			return (int)(baseScore * scale);
		}

		/// <summary>
		/// Apply variety penalties with configurable dampening.
		/// Uses Settings.VarietyPenalty for base strength and Settings.VarietyDampening for action override.
		/// </summary>
		private void ApplyVarietyPenaltiesWithDampening(Dictionary<int, DriverScore> driverScores)
		{
			// Scale penalties based on VarietyPenalty setting (default 60)
			// Higher = more variety enforcement
			float penaltyScale = Settings.VarietyPenalty / 60.0f;
			int[] basePenalties = {
				(int)(60 * penaltyScale),  // Most recent
				(int)(40 * penaltyScale),
				(int)(25 * penaltyScale),
				(int)(15 * penaltyScale),
				(int)(10 * penaltyScale),
				(int)(8 * penaltyScale),
				(int)(6 * penaltyScale),
				(int)(4 * penaltyScale)
			};

			// Dampening percentage from settings (0-100, where 100 = action fully overrides variety)
			float maxDampening = Settings.VarietyDampening / 100.0f;

			for (int i = 0; i < _recentlySelectedDrivers.Count && i < basePenalties.Length; i++)
			{
				int driverNum = _recentlySelectedDrivers[i];
				if (!driverScores.ContainsKey(driverNum))
					continue;

				var score = driverScores[driverNum];
				int basePenalty = basePenalties[i];

				// Dampen penalty based on action level, but always keep at least 40% penalty
				// This ensures variety still matters even during high action
				float dampener = 1.0f - (score.ActionLevel * maxDampening);
				dampener = Math.Max(dampener, 0.4f); // Floor: always at least 40% of penalty applies

				int adjustedPenalty = (int)(basePenalty * dampener);

				score.VarietyPenalty = adjustedPenalty;
				score.BaseScore -= adjustedPenalty;
				score.Breakdown.Add($"Variety (#{i + 1}): -{adjustedPenalty}");
			}
		}

		private void ApplyOverexposurePenalty(Dictionary<int, DriverScore> driverScores, int activeDriverCount)
		{
			if (_totalSelectionsInPlan < 3 || activeDriverCount == 0)
				return;

			float fairShare = _totalSelectionsInPlan / (float)activeDriverCount;

			foreach (var kvp in _driverSelectionCounts)
			{
				if (driverScores.ContainsKey(kvp.Key))
				{
					int overExposure = (int)(kvp.Value - fairShare);
					if (overExposure > 0)
					{
						int penalty = overExposure * 10;
						penalty = Math.Min(penalty, 40);
						driverScores[kvp.Key].BaseScore -= penalty;
						driverScores[kvp.Key].Breakdown.Add($"Overexposure ({kvp.Value} selections): -{penalty}");
					}
				}
			}
		}

		private void ApplyFieldDiversityBonus(Dictionary<int, DriverScore> driverScores, List<Driver> activeDrivers)
		{
			// Every 5th selection, boost midfield/back drivers
			if (_totalSelectionsInPlan > 0 && _totalSelectionsInPlan % 5 == 0)
			{
				foreach (var driver in activeDrivers)
				{
					if (!driverScores.ContainsKey(driver.NumberRaw))
						continue;

					if (driver.Position > 10)
					{
						int selectCount = _driverSelectionCounts.ContainsKey(driver.NumberRaw)
							? _driverSelectionCounts[driver.NumberRaw] : 0;
						if (selectCount <= 1)
						{
							int bonus = driver.Position > 15 ? 25 : 20;
							driverScores[driver.NumberRaw].BaseScore += bonus;
							driverScores[driver.NumberRaw].Breakdown.Add($"Field diversity: +{bonus}");
						}
					}
				}
			}
		}

		/// <summary>
		/// Apply bonus to the focus driver (specified by car number in settings).
		/// </summary>
		private void ApplyFocusDriverBonus(Dictionary<int, DriverScore> driverScores)
		{
			// Check if a focus driver is configured (0 means no focus driver)
			int focusDriverNumber = Settings.FocusDriverNumber;
			if (focusDriverNumber <= 0)
				return;

			int focusBonus = Settings.FocusDriverBonus;
			if (focusBonus <= 0)
				return;

			// Apply bonus to the focus driver if they're in the scoring list
			if (driverScores.ContainsKey(focusDriverNumber))
			{
				driverScores[focusDriverNumber].BaseScore += focusBonus;
				driverScores[focusDriverNumber].Breakdown.Add($"Focus driver (#{focusDriverNumber}): +{focusBonus}");
			}
		}

		private DriverScore SelectWithTieBreaking(Dictionary<int, DriverScore> driverScores)
		{
			if (!driverScores.Any())
				return null;

			var sortedScores = driverScores.Values
				.OrderByDescending(s => s.BaseScore)
				.ToList();

			var topScore = sortedScores.First();

			// Find drivers within 5% or 5 points of top score
			int threshold = Math.Max(5, (int)(Math.Abs(topScore.BaseScore) * 0.05f));
			var tiedDrivers = sortedScores
				.Where(s => topScore.BaseScore - s.BaseScore <= threshold)
				.ToList();

			if (tiedDrivers.Count > 1)
			{
				return tiedDrivers[_random.Next(tiedDrivers.Count)];
			}

			return topScore;
		}

		private void UpdateRecentlySelected(int driverNumber)
		{
			_recentlySelectedDrivers.Remove(driverNumber);
			_recentlySelectedDrivers.Insert(0, driverNumber);

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
			var orderedDrivers = activeDrivers
				.Where(d => d.Position > 0 && d.NumberRaw != 0)
				.OrderBy(d => d.Position)
				.ToList();

			var notRecent = orderedDrivers
				.FirstOrDefault(d => !_recentlySelectedDrivers.Contains(d.NumberRaw));

			if (notRecent != null)
			{
				UpdateRecentlySelected(notRecent.NumberRaw);
				UpdateSelectionCount(notRecent.NumberRaw);
				return notRecent;
			}

			var backRunner = orderedDrivers.LastOrDefault();
			if (backRunner != null)
			{
				UpdateRecentlySelected(backRunner.NumberRaw);
				UpdateSelectionCount(backRunner.NumberRaw);
				return backRunner;
			}

			return activeDrivers.FirstOrDefault(d => d.NumberRaw != 0);
		}

		/// <summary>
		/// Enhanced driver score tracking with all new scoring components.
		/// </summary>
		private class DriverScore
		{
			public int DriverNumber { get; set; }
			public Driver Driver { get; set; }

			// Action-based scores (contribute to ActionLevel)
			public int EventScore { get; set; }
			public int MomentumBonus { get; set; }
			public int BattleBonus { get; set; }
			public int PackBonus { get; set; }
			public int FreshActionBonus { get; set; }

			// Calculated from action scores
			public int ActionScore { get; set; }
			public float ActionLevel { get; set; } // 0.0 to 1.0

			// Standard scores
			public int PositionScore { get; set; }
			public int VarietyPenalty { get; set; }

			// Final combined score
			public int BaseScore { get; set; }

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
