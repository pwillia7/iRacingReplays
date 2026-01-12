using iRacingReplayDirector.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iRacingReplayDirector.AI.EventDetection
{
	public class IncidentDetector : IEventDetector
	{
		public RaceEventType EventType => RaceEventType.Incident;

		public bool IsEnabled { get; set; } = true;

		private const int MinFramesBetweenIncidents = 300; // ~5 seconds at 60fps

		public List<RaceEvent> DetectEvents(List<TelemetrySnapshot> snapshots)
		{
			var events = new List<RaceEvent>();

			try
			{
				if (snapshots == null || snapshots.Count < 2)
					return events;

				var lastIncidentFrameByDriver = new Dictionary<int, int>();

				for (int i = 1; i < snapshots.Count; i++)
				{
					var previousSnapshot = snapshots[i - 1];
					var currentSnapshot = snapshots[i];

					if (previousSnapshot?.DriverStates == null || currentSnapshot?.DriverStates == null)
						continue;

					foreach (var currentDriver in currentSnapshot.DriverStates)
					{
						if (currentDriver == null)
							continue;

						var previousDriver = previousSnapshot.DriverStates
							.FirstOrDefault(d => d != null && d.NumberRaw == currentDriver.NumberRaw);

						if (previousDriver == null)
							continue;

						// Detect off-track incidents
						if (previousDriver.TrackSurface == iRacingReplayDirector.TrackSurfaces.OnTrack &&
							currentDriver.TrackSurface == iRacingReplayDirector.TrackSurfaces.OffTrack)
						{
							// Check if enough time has passed since last incident for this driver
							if (lastIncidentFrameByDriver.TryGetValue(currentDriver.NumberRaw, out int lastFrame) &&
								(currentSnapshot.Frame - lastFrame) < MinFramesBetweenIncidents)
							{
								continue;
							}

							lastIncidentFrameByDriver[currentDriver.NumberRaw] = currentSnapshot.Frame;

							var raceEvent = new RaceEvent
							{
								Frame = currentSnapshot.Frame,
								SessionTime = currentSnapshot.SessionTime,
								EventType = RaceEventType.Incident,
								PrimaryDriverNumber = currentDriver.NumberRaw,
								PrimaryDriverName = currentDriver.TeamName ?? "Unknown",
								Position = currentDriver.Position,
								LapDistancePct = currentDriver.LapDistance,
								Description = $"#{currentDriver.NumberRaw} {currentDriver.TeamName ?? "Unknown"} went off track",
								ImportanceScore = CalculateImportance(currentDriver.Position),
								DurationFrames = 180 // ~3 seconds
							};

							events.Add(raceEvent);
						}

						// Detect potential spins (rapid change in lap distance percentage going backwards)
						if (previousDriver.TrackSurface == iRacingReplayDirector.TrackSurfaces.OnTrack &&
							currentDriver.TrackSurface == iRacingReplayDirector.TrackSurfaces.OnTrack)
						{
							float lapProgressChange = currentDriver.LapDistance - previousDriver.LapDistance;

							// Normal forward progress would be positive and small
							// A spin might show negative progress (car facing wrong way) or very little progress
							// This is a heuristic - real spin detection would need more data
							if (lapProgressChange < -0.01f && lapProgressChange > -0.5f) // Going backwards but not a new lap
							{
								if (lastIncidentFrameByDriver.TryGetValue(currentDriver.NumberRaw, out int lastFrame) &&
									(currentSnapshot.Frame - lastFrame) < MinFramesBetweenIncidents)
								{
									continue;
								}

								lastIncidentFrameByDriver[currentDriver.NumberRaw] = currentSnapshot.Frame;

								var raceEvent = new RaceEvent
								{
									Frame = currentSnapshot.Frame,
									SessionTime = currentSnapshot.SessionTime,
									EventType = RaceEventType.Incident,
									PrimaryDriverNumber = currentDriver.NumberRaw,
									PrimaryDriverName = currentDriver.TeamName ?? "Unknown",
									Position = currentDriver.Position,
									LapDistancePct = currentDriver.LapDistance,
									Description = $"#{currentDriver.NumberRaw} {currentDriver.TeamName ?? "Unknown"} possible spin",
									ImportanceScore = CalculateImportance(currentDriver.Position),
									DurationFrames = 240 // ~4 seconds
								};

								events.Add(raceEvent);
							}
						}
					}
				}
			}
			catch (Exception)
			{
				// Swallow exceptions in detector to avoid crashing the whole scan
			}

			return events;
		}

		private int CalculateImportance(int position)
		{
			// Leaders and front-runners are more important
			if (position <= 3) return 10;
			if (position <= 5) return 8;
			if (position <= 10) return 6;
			return 4;
		}
	}
}
