namespace iRacingReplayDirector.AI.Models
{
	public class DriverSnapshot
	{
		public int Id { get; set; }

		public int NumberRaw { get; set; }

		public string TeamName { get; set; } = string.Empty;

		public int Position { get; set; }

		public int Lap { get; set; }

		public float LapDistance { get; set; }

		public iRacingReplayDirector.TrackSurfaces TrackSurface { get; set; }
	}
}
