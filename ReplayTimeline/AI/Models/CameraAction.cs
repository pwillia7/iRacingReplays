using Newtonsoft.Json;

namespace iRacingReplayDirector.AI.Models
{
	public class CameraAction
	{
		[JsonProperty("frame")]
		public int Frame { get; set; }

		// DriverNumber is optional - defaults to -1 which means "most exciting" in iRacing
		// The LLM no longer specifies drivers; iRacing's automatic selection handles it
		[JsonProperty("driverNumber", NullValueHandling = NullValueHandling.Ignore)]
		public int DriverNumber { get; set; } = -1;

		[JsonProperty("cameraName")]
		public string CameraName { get; set; }

		[JsonProperty("duration")]
		public int DurationSeconds { get; set; }

		[JsonProperty("reason")]
		public string Reason { get; set; }
	}
}
