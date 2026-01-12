using System;

namespace iRacingReplayDirector.AI.Models
{
	public enum RaceEventType
	{
		Incident,       // Off-track, spin, contact
		Overtake,       // Position change
		Battle,         // Close racing (gap < threshold)
		PitStop,        // Pit entry/exit
		RaceStart,      // Green flag
		RaceFinish      // Checkered flag
	}

	public class RaceEvent
	{
		public int Frame { get; set; }

		public double SessionTime { get; set; }

		public RaceEventType EventType { get; set; }

		public int PrimaryDriverNumber { get; set; }

		public string PrimaryDriverName { get; set; }

		public int? SecondaryDriverNumber { get; set; }

		public string SecondaryDriverName { get; set; }

		public int? Position { get; set; }

		public float? LapDistancePct { get; set; }

		public string Description { get; set; }

		public int ImportanceScore { get; set; }

		public int DurationFrames { get; set; }

		public override string ToString()
		{
			return $"[{EventType}] Frame {Frame}: {Description}";
		}
	}
}
