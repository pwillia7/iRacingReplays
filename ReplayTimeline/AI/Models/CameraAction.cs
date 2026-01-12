using Newtonsoft.Json;

namespace iRacingReplayDirector.AI.Models
{
	public class CameraAction
	{
		[JsonProperty("frame")]
		public int Frame { get; set; }

		[JsonProperty("driverNumber")]
		public int DriverNumber { get; set; }

		[JsonProperty("cameraName")]
		public string CameraName { get; set; }

		[JsonProperty("duration")]
		public int DurationSeconds { get; set; }

		[JsonProperty("reason")]
		public string Reason { get; set; }
	}
}
