using System.Collections.Generic;

namespace iRacingReplayDirector.AI.Models
{
	public class RaceEventSummary
	{
		public string TrackName { get; set; }

		public string SessionType { get; set; }

		public int StartFrame { get; set; }

		public int EndFrame { get; set; }

		public int TotalFrames => EndFrame - StartFrame;

		public double DurationMinutes { get; set; }

		public int FrameRate { get; set; } = 60;

		public List<DriverSummary> Drivers { get; set; } = new List<DriverSummary>();

		public List<CameraSummary> AvailableCameras { get; set; } = new List<CameraSummary>();

		public List<RaceEvent> Events { get; set; } = new List<RaceEvent>();

		public int RecommendedCuts => (int)(DurationMinutes * 4); // ~4 cuts per minute
	}

	public class DriverSummary
	{
		public int NumberRaw { get; set; }

		public string TeamName { get; set; }

		public int StartPosition { get; set; }

		public int EndPosition { get; set; }

		public int PositionsGained => StartPosition - EndPosition;
	}

	public class CameraSummary
	{
		public int GroupNum { get; set; }

		public string GroupName { get; set; }
	}
}
