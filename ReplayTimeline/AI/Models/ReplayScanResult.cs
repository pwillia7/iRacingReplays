using System.Collections.Generic;

namespace iRacingReplayDirector.AI.Models
{
	public class ReplayScanResult
	{
		public int StartFrame { get; set; }

		public int EndFrame { get; set; }

		public int TotalFrames => EndFrame - StartFrame;

		public double DurationSeconds { get; set; }

		public string TrackName { get; set; }

		public string SessionType { get; set; }

		public List<TelemetrySnapshot> Snapshots { get; set; } = new List<TelemetrySnapshot>();

		public List<RaceEvent> Events { get; set; } = new List<RaceEvent>();
	}
}
