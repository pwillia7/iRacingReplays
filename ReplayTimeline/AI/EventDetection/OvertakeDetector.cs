using iRacingReplayDirector.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iRacingReplayDirector.AI.EventDetection
{
	public class OvertakeDetector : IEventDetector
	{
		public RaceEventType EventType => RaceEventType.Overtake;

		public bool IsEnabled { get; set; } = true;

		private const int MinFramesBetweenOvertakes = 120; // ~2 seconds at 60fps

		public List<RaceEvent> DetectEvents(List<TelemetrySnapshot> snapshots)
		{
			var events = new List<RaceEvent>();

			try
			{
				if (snapshots == null || snapshots.Count < 2)
					return events;

				var lastOvertakeFrameByDriver = new Dictionary<int, int>();

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

						// Skip drivers not on track
						if (currentDriver.TrackSurface != iRacingReplayDirector.TrackSurfaces.OnTrack)
							continue;

						// Detect position gain (lower position number = better)
						if (currentDriver.Position < previousDriver.Position && previousDriver.Position > 0)
						{
							// Check if enough time has passed since last overtake for this driver
							if (lastOvertakeFrameByDriver.TryGetValue(currentDriver.NumberRaw, out int lastFrame) &&
								(currentSnapshot.Frame - lastFrame) < MinFramesBetweenOvertakes)
							{
								continue;
							}

							lastOvertakeFrameByDriver[currentDriver.NumberRaw] = currentSnapshot.Frame;

							// Find who was passed
							var passedDriver = currentSnapshot.DriverStates
								.FirstOrDefault(d => d != null && d.Position == previousDriver.Position && d.NumberRaw != currentDriver.NumberRaw);

							var raceEvent = new RaceEvent
							{
								Frame = currentSnapshot.Frame,
								SessionTime = currentSnapshot.SessionTime,
								EventType = RaceEventType.Overtake,
								PrimaryDriverNumber = currentDriver.NumberRaw,
								PrimaryDriverName = currentDriver.TeamName ?? "Unknown",
								SecondaryDriverNumber = passedDriver?.NumberRaw,
								SecondaryDriverName = passedDriver?.TeamName,
								Position = currentDriver.Position,
								LapDistancePct = currentDriver.LapDistance,
								Description = passedDriver != null
									? $"#{currentDriver.NumberRaw} {currentDriver.TeamName ?? "Unknown"} passes #{passedDriver.NumberRaw} for P{currentDriver.Position}"
									: $"#{currentDriver.NumberRaw} {currentDriver.TeamName ?? "Unknown"} moves to P{currentDriver.Position}",
								ImportanceScore = CalculateImportance(currentDriver.Position, previousDriver.Position - currentDriver.Position),
								DurationFrames = 300 // ~5 seconds
							};

							events.Add(raceEvent);
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

		private int CalculateImportance(int newPosition, int positionsGained)
		{
			int baseScore = 5;

			// Lead changes are most important
			if (newPosition == 1) baseScore = 10;
			else if (newPosition <= 3) baseScore = 8;
			else if (newPosition <= 5) baseScore = 7;
			else if (newPosition <= 10) baseScore = 6;

			// Multiple positions gained adds importance
			if (positionsGained > 1) baseScore += positionsGained - 1;

			return System.Math.Min(baseScore, 10);
		}
	}
}
