using System.Collections.Generic;

namespace iRacingReplayDirector.AI.Models
{
	public class TelemetrySnapshot
	{
		public int Frame { get; set; }

		public double SessionTime { get; set; }

		public List<DriverSnapshot> DriverStates { get; set; } = new List<DriverSnapshot>();
	}
}
