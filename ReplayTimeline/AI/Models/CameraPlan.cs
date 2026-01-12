using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace iRacingReplayDirector.AI.Models
{
	public class CameraPlan
	{
		[JsonProperty("cameraActions")]
		public List<CameraAction> CameraActions { get; set; } = new List<CameraAction>();

		[JsonIgnore]
		public string GeneratedBy { get; set; }

		[JsonIgnore]
		public DateTime GeneratedAt { get; set; }

		[JsonIgnore]
		public int TotalDurationFrames { get; set; }
	}
}
