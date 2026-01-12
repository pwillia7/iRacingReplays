using iRacingReplayDirector.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace iRacingReplayDirector.AI.EventDetection
{
	public class BattleDetector : IEventDetector
	{
		public RaceEventType EventType => RaceEventType.Battle;

		public bool IsEnabled { get; set; } = true;

		public float GapThresholdPct { get; set; } = 0.02f; // 2% of lap = close battle

		private const int MinBattleDurationFrames = 300; // ~5 seconds minimum battle

		private const int MinFramesBetweenBattleEvents = 600; // ~10 seconds between battle events

		public List<RaceEvent> DetectEvents(List<TelemetrySnapshot> snapshots)
		{
			var events = new List<RaceEvent>();

			try
			{
				if (snapshots == null || snapshots.Count < 2)
					return events;

				// Track ongoing battles: key = sorted pair of driver numbers
				var ongoingBattles = new Dictionary<string, BattleTracker>();
				var lastBattleEventFrame = new Dictionary<string, int>();

				foreach (var snapshot in snapshots)
				{
					if (snapshot?.DriverStates == null)
						continue;

					var onTrackDrivers = snapshot.DriverStates
						.Where(d => d != null && d.TrackSurface == iRacingReplayDirector.TrackSurfaces.OnTrack && d.Position > 0)
						.OrderBy(d => d.Position)
						.ToList();

					// Check pairs of consecutive positions
					for (int i = 0; i < onTrackDrivers.Count - 1; i++)
					{
						var driver1 = onTrackDrivers[i];
						var driver2 = onTrackDrivers[i + 1];

						// Calculate gap based on lap distance
						float gap = CalculateGap(driver1, driver2);
						string battleKey = GetBattleKey(driver1.NumberRaw, driver2.NumberRaw);

						if (gap <= GapThresholdPct)
						{
							// Cars are close - track or continue battle
							if (!ongoingBattles.ContainsKey(battleKey))
							{
								ongoingBattles[battleKey] = new BattleTracker
								{
									StartFrame = snapshot.Frame,
									Driver1Number = driver1.NumberRaw,
									Driver1Name = driver1.TeamName ?? "Unknown",
									Driver2Number = driver2.NumberRaw,
									Driver2Name = driver2.TeamName ?? "Unknown",
									Position = driver1.Position
								};
							}

							ongoingBattles[battleKey].LastFrame = snapshot.Frame;
							ongoingBattles[battleKey].ClosestGap = Math.Min(ongoingBattles[battleKey].ClosestGap, gap);
						}
						else
						{
							// Gap increased - check if battle ended
							if (ongoingBattles.TryGetValue(battleKey, out var battle))
							{
								int battleDuration = battle.LastFrame - battle.StartFrame;

								if (battleDuration >= MinBattleDurationFrames)
								{
									// Check if enough time since last battle event for this pair
									if (!lastBattleEventFrame.TryGetValue(battleKey, out int lastFrame) ||
										(snapshot.Frame - lastFrame) >= MinFramesBetweenBattleEvents)
									{
										lastBattleEventFrame[battleKey] = battle.StartFrame;

										var raceEvent = new RaceEvent
										{
											Frame = battle.StartFrame,
											SessionTime = 0,
											EventType = RaceEventType.Battle,
											PrimaryDriverNumber = battle.Driver1Number,
											PrimaryDriverName = battle.Driver1Name,
											SecondaryDriverNumber = battle.Driver2Number,
											SecondaryDriverName = battle.Driver2Name,
											Position = battle.Position,
											Description = $"Battle for P{battle.Position}: #{battle.Driver1Number} vs #{battle.Driver2Number}",
											ImportanceScore = CalculateImportance(battle.Position, battleDuration),
											DurationFrames = battleDuration
										};

										events.Add(raceEvent);
									}
								}

								ongoingBattles.Remove(battleKey);
							}
						}
					}
				}

				// Process any remaining ongoing battles at the end
				foreach (var kvp in ongoingBattles)
				{
					var battle = kvp.Value;
					int battleDuration = battle.LastFrame - battle.StartFrame;

					if (battleDuration >= MinBattleDurationFrames)
					{
						if (!lastBattleEventFrame.TryGetValue(kvp.Key, out int lastFrame) ||
							(battle.StartFrame - lastFrame) >= MinFramesBetweenBattleEvents)
						{
							var raceEvent = new RaceEvent
							{
								Frame = battle.StartFrame,
								SessionTime = 0,
								EventType = RaceEventType.Battle,
								PrimaryDriverNumber = battle.Driver1Number,
								PrimaryDriverName = battle.Driver1Name,
								SecondaryDriverNumber = battle.Driver2Number,
								SecondaryDriverName = battle.Driver2Name,
								Position = battle.Position,
								Description = $"Battle for P{battle.Position}: #{battle.Driver1Number} vs #{battle.Driver2Number}",
								ImportanceScore = CalculateImportance(battle.Position, battleDuration),
								DurationFrames = battleDuration
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

		private float CalculateGap(DriverSnapshot leader, DriverSnapshot follower)
		{
			// Simple gap calculation based on lap distance percentage
			// Both cars on same lap: direct subtraction
			// Different laps: account for full lap difference
			float gap;

			if (leader.Lap == follower.Lap)
			{
				gap = leader.LapDistance - follower.LapDistance;
			}
			else if (leader.Lap > follower.Lap)
			{
				// Leader is ahead by at least one lap
				gap = (1.0f - follower.LapDistance) + leader.LapDistance + (leader.Lap - follower.Lap - 1);
			}
			else
			{
				// Follower somehow ahead? Use absolute difference
				gap = Math.Abs(leader.LapDistance - follower.LapDistance);
			}

			return Math.Abs(gap);
		}

		private string GetBattleKey(int driver1, int driver2)
		{
			int min = Math.Min(driver1, driver2);
			int max = Math.Max(driver1, driver2);
			return $"{min}-{max}";
		}

		private int CalculateImportance(int position, int durationFrames)
		{
			int baseScore = 5;

			// Battles for lead are most important
			if (position == 1) baseScore = 10;
			else if (position <= 3) baseScore = 8;
			else if (position <= 5) baseScore = 7;
			else if (position <= 10) baseScore = 6;

			// Longer battles are more interesting
			if (durationFrames > 600) baseScore += 1; // > 10 seconds
			if (durationFrames > 1200) baseScore += 1; // > 20 seconds

			return Math.Min(baseScore, 10);
		}

		private class BattleTracker
		{
			public int StartFrame { get; set; }
			public int LastFrame { get; set; }
			public int Driver1Number { get; set; }
			public string Driver1Name { get; set; }
			public int Driver2Number { get; set; }
			public string Driver2Name { get; set; }
			public int Position { get; set; }
			public float ClosestGap { get; set; } = float.MaxValue;
		}
	}
}
