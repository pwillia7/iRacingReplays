using Newtonsoft.Json;
using System.Collections.Generic;

namespace iRacingReplayDirector.AI.Models
{
	public class CameraAction
	{
		[JsonProperty("frame")]
		public int Frame { get; set; }

		// Primary driver to focus on at the start of this camera action
		[JsonProperty("driverNumber", NullValueHandling = NullValueHandling.Ignore)]
		public int DriverNumber { get; set; }

		[JsonProperty("cameraName")]
		public string CameraName { get; set; }

		[JsonProperty("duration")]
		public int DurationSeconds { get; set; }

		[JsonProperty("reason")]
		public string Reason { get; set; }

		// Focus type helps understand why this driver/camera was chosen
		// Values: "battle", "overtake", "incident", "leader", "field", "pit"
		[JsonProperty("focusType", NullValueHandling = NullValueHandling.Ignore)]
		public string FocusType { get; set; }

		// Secondary driver involved in the action (e.g., the other driver in a battle)
		[JsonProperty("secondaryDriver", NullValueHandling = NullValueHandling.Ignore)]
		public int? SecondaryDriverNumber { get; set; }

		// For dynamic scenes: switch to this driver midway through the camera action
		// Frame offset from the start of this action (e.g., 300 = switch 5 seconds in)
		[JsonProperty("switchToSecondaryAtFrame", NullValueHandling = NullValueHandling.Ignore)]
		public int? SwitchToSecondaryAtFrame { get; set; }

		// Optional: List of driver numbers to cycle through during this action
		// Useful for showing multiple cars in a battle or pack racing
		[JsonProperty("driverSequence", NullValueHandling = NullValueHandling.Ignore)]
		public List<DriverFocus> DriverSequence { get; set; }
	}

	// Represents a driver focus within a camera action
	public class DriverFocus
	{
		[JsonProperty("driverNumber")]
		public int DriverNumber { get; set; }

		// Frame at which to switch to this driver (absolute frame number)
		[JsonProperty("atFrame")]
		public int AtFrame { get; set; }

		// Optional reason for focusing on this driver
		[JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
		public string Reason { get; set; }
	}
}
