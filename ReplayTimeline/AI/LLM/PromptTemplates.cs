using iRacingReplayDirector.AI.Models;
using System.Linq;
using System.Text;

namespace iRacingReplayDirector.AI.LLM
{
	public static class PromptTemplates
	{
		public static string SystemPrompt => @"You are an expert motorsport broadcast director creating camera sequences for iRacing replays. Your task is to create engaging coverage that captures the most exciting moments of the race.

GUIDELINES:
- Focus on battles, overtakes, and position changes - these are the most engaging moments
- Show incidents (crashes, spins, off-tracks) but don't dwell too long on them
- Cut to the race leader periodically to maintain context
- Use varied camera angles: TV cameras for wide establishing shots, chase/cockpit for close racing
- Typical shot duration: 5-15 seconds. Longer (up to 30s) for developing battles
- Start with an establishing shot (TV or helicopter camera) at the race start
- Ensure good coverage of drivers who gained multiple positions
- Avoid staying on one car for more than 30 seconds unless action is continuous

OUTPUT FORMAT:
Respond with ONLY valid JSON matching this exact schema (no markdown, no explanation):
{
  ""cameraActions"": [
    {
      ""frame"": <integer - the frame number to switch camera>,
      ""driverNumber"": <integer - the car number to focus on>,
      ""cameraName"": <string - must match one of the available camera names exactly>,
      ""duration"": <integer - suggested duration in seconds>,
      ""reason"": <string - brief explanation of why this shot>
    }
  ]
}

The cameraActions array must be sorted by frame number in ascending order.";

		public static string BuildUserPrompt(RaceEventSummary summary)
		{
			var sb = new StringBuilder();

			sb.AppendLine("Create a camera plan for this race replay.");
			sb.AppendLine();

			// Race info
			sb.AppendLine("RACE INFO:");
			sb.AppendLine($"- Track: {summary.TrackName ?? "Unknown Track"}");
			sb.AppendLine($"- Session: {summary.SessionType ?? "Race"}");
			sb.AppendLine($"- Duration: {summary.TotalFrames} frames ({summary.DurationMinutes:F1} minutes)");
			sb.AppendLine($"- Frame rate: {summary.FrameRate} fps");
			sb.AppendLine($"- Start frame: {summary.StartFrame}");
			sb.AppendLine($"- End frame: {summary.EndFrame}");
			sb.AppendLine();

			// Available cameras
			sb.AppendLine("AVAILABLE CAMERAS:");
			if (summary.AvailableCameras != null && summary.AvailableCameras.Count > 0)
			{
				foreach (var camera in summary.AvailableCameras)
				{
					sb.AppendLine($"- {camera.GroupName}");
				}
			}
			else
			{
				sb.AppendLine("- TV1");
				sb.AppendLine("- TV2");
				sb.AppendLine("- Cockpit");
				sb.AppendLine("- Chase");
			}
			sb.AppendLine();

			// Drivers
			sb.AppendLine("DRIVERS:");
			if (summary.Drivers != null && summary.Drivers.Count > 0)
			{
				var topDrivers = summary.Drivers.OrderBy(d => d.EndPosition).Take(20);
				foreach (var driver in topDrivers)
				{
					string posChange = "";
					if (driver.PositionsGained > 0)
						posChange = $" (+{driver.PositionsGained} positions)";
					else if (driver.PositionsGained < 0)
						posChange = $" ({driver.PositionsGained} positions)";

					sb.AppendLine($"- #{driver.NumberRaw} {driver.TeamName} - P{driver.EndPosition}{posChange}");
				}
			}
			sb.AppendLine();

			// Events
			sb.AppendLine("DETECTED EVENTS (sorted by importance):");
			if (summary.Events != null && summary.Events.Count > 0)
			{
				var sortedEvents = summary.Events
					.OrderByDescending(e => e.ImportanceScore)
					.ThenBy(e => e.Frame)
					.Take(50); // Limit to top 50 events

				foreach (var evt in sortedEvents)
				{
					sb.AppendLine($"- Frame {evt.Frame} [{evt.EventType}] (Importance: {evt.ImportanceScore}/10): {evt.Description}");
				}
			}
			else
			{
				sb.AppendLine("- No specific events detected. Create general race coverage.");
			}
			sb.AppendLine();

			// Request
			sb.AppendLine($"Generate a camera plan with approximately {summary.RecommendedCuts} camera cuts covering the race highlights.");
			sb.AppendLine("Ensure the first camera action starts at or near the start frame.");
			sb.AppendLine("Use the exact camera names from the AVAILABLE CAMERAS list.");

			return sb.ToString();
		}
	}
}
