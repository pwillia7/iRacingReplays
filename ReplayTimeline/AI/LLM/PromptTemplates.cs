using iRacingReplayDirector.AI.Models;
using System.Linq;
using System.Text;

namespace iRacingReplayDirector.AI.LLM
{
	public static class PromptTemplates
	{
		public static string SystemPrompt => @"You are an expert motorsport broadcast director creating camera sequences for iRacing replays.

IMPORTANT: iRacing will automatically follow the 'most exciting' car at any moment. Your job is ONLY to select which CAMERA ANGLE to use and when to switch. You do NOT need to specify which driver to follow.

AVAILABLE STANDARD CAMERAS (use exact names):
- Nose: Front-facing camera on car nose, dramatic low angle
- Gearbox: Rear-facing camera from gearbox area
- Roll Bar: Over-the-shoulder view from roll bar
- LF Susp / LR Susp / RF Susp / RR Susp: Suspension-mounted cameras, unique angles
- Gyro: Stabilized onboard camera
- Cockpit: Driver's eye view from inside the car
- Scenic: Track-side beauty shots, good for replays
- TV1 / TV2 / TV3: Traditional broadcast TV cameras at various track positions
- Chopper: Helicopter camera for aerial views
- Blimp: High overhead blimp camera
- Chase: Behind-car chase camera
- Far Chase: Wider chase camera further back
- Rear Chase: Chase camera from behind

BROADCAST DIRECTING GUIDELINES:
1. VARIETY IS KEY: Mix camera types throughout - don't overuse any single camera
2. Establish-Detail-Establish rhythm: Wide shot (TV/Blimp) -> Close action (Chase/Cockpit) -> Wide shot
3. Use TV cameras (TV1, TV2, TV3) for:
   - Race starts and restarts
   - Showing track position and gaps
   - Corner entries with multiple cars
4. Use Chase/Far Chase for:
   - Close racing and battles
   - Following action through corners
   - Most of the general racing coverage
5. Use Cockpit/Roll Bar for:
   - Intense battles
   - Demonstrating driver skill
   - Weather conditions (rain, fog)
6. Use Chopper/Blimp for:
   - Opening establishing shots
   - Showing the full field
   - Transitions between track sections
7. Use Scenic for:
   - Brief artistic shots during calm moments
   - Track beauty shots
8. Camera timing:
   - Wide establishing shots: 3-8 seconds
   - TV cameras: 5-15 seconds
   - Chase cameras: 8-20 seconds (longer for battles)
   - Cockpit/onboard: 5-12 seconds
   - Chopper/Blimp: 5-10 seconds

SHOT SEQUENCING TIPS:
- Never use the same camera twice in a row
- After an intense close-up, cut to a wider shot
- Build tension by moving from wide to progressively tighter shots
- Use TV cameras to 're-establish' location on the track

OUTPUT FORMAT:
Respond with ONLY valid JSON (no markdown, no explanation):
{
  ""cameraActions"": [
    {
      ""frame"": <integer - frame number to switch camera>,
      ""cameraName"": <string - exact camera name from the list>,
      ""duration"": <integer - duration in seconds>,
      ""reason"": <string - brief explanation>
    }
  ]
}

Sort cameraActions by frame number ascending.";

		public static string BuildUserPrompt(RaceEventSummary summary)
		{
			var sb = new StringBuilder();

			sb.AppendLine("Create a comprehensive camera plan for this race replay.");
			sb.AppendLine("Remember: iRacing automatically follows the most exciting action - you only choose WHICH CAMERA to use.");
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

			// Available cameras from this session
			sb.AppendLine("CAMERAS AVAILABLE IN THIS SESSION:");
			if (summary.AvailableCameras != null && summary.AvailableCameras.Count > 0)
			{
				// Categorize cameras
				var tvCameras = summary.AvailableCameras.Where(c => c.GroupName.StartsWith("TV")).ToList();
				var chaseCameras = summary.AvailableCameras.Where(c => c.GroupName.ToLower().Contains("chase")).ToList();
				var onboardCameras = summary.AvailableCameras.Where(c =>
					c.GroupName == "Cockpit" || c.GroupName == "Roll Bar" || c.GroupName == "Gyro" ||
					c.GroupName == "Nose" || c.GroupName == "Gearbox" || c.GroupName.Contains("Susp")).ToList();
				var aerialCameras = summary.AvailableCameras.Where(c =>
					c.GroupName == "Chopper" || c.GroupName == "Blimp").ToList();
				var otherCameras = summary.AvailableCameras.Where(c =>
					!tvCameras.Contains(c) && !chaseCameras.Contains(c) &&
					!onboardCameras.Contains(c) && !aerialCameras.Contains(c)).ToList();

				if (tvCameras.Any())
				{
					sb.AppendLine("TV/Broadcast cameras: " + string.Join(", ", tvCameras.Select(c => c.GroupName)));
				}
				if (chaseCameras.Any())
				{
					sb.AppendLine("Chase cameras: " + string.Join(", ", chaseCameras.Select(c => c.GroupName)));
				}
				if (onboardCameras.Any())
				{
					sb.AppendLine("Onboard cameras: " + string.Join(", ", onboardCameras.Select(c => c.GroupName)));
				}
				if (aerialCameras.Any())
				{
					sb.AppendLine("Aerial cameras: " + string.Join(", ", aerialCameras.Select(c => c.GroupName)));
				}
				if (otherCameras.Any())
				{
					sb.AppendLine("Other cameras: " + string.Join(", ", otherCameras.Select(c => c.GroupName)));
				}
			}
			else
			{
				sb.AppendLine("TV1, TV2, TV3, Cockpit, Chase, Far Chase, Chopper, Blimp");
			}
			sb.AppendLine();

			// Events for context (helps LLM know when action happens)
			sb.AppendLine("KEY MOMENTS (for camera selection context):");
			if (summary.Events != null && summary.Events.Count > 0)
			{
				var sortedEvents = summary.Events
					.OrderBy(e => e.Frame)
					.Take(30);

				foreach (var evt in sortedEvents)
				{
					string cameraHint = "";
					switch (evt.EventType)
					{
						case RaceEventType.Incident:
							cameraHint = " -> consider TV or Chopper to show aftermath";
							break;
						case RaceEventType.Overtake:
							cameraHint = " -> consider Chase or TV for the pass";
							break;
						case RaceEventType.Battle:
							cameraHint = " -> consider Chase or Cockpit for intensity";
							break;
					}
					sb.AppendLine($"- Frame {evt.Frame}: {evt.Description}{cameraHint}");
				}
			}
			else
			{
				sb.AppendLine("- No specific events detected. Create varied general race coverage.");
			}
			sb.AppendLine();

			// Request
			int targetCuts = (int)(summary.DurationMinutes * 6); // About 6 cuts per minute for dynamic coverage
			if (targetCuts < 10) targetCuts = 10;
			if (targetCuts > 100) targetCuts = 100;

			sb.AppendLine($"Create approximately {targetCuts} camera switches for professional broadcast-style coverage.");
			sb.AppendLine("IMPORTANT: Use a good MIX of different camera types throughout the replay.");
			sb.AppendLine("Start with an establishing shot (TV, Blimp, or Chopper) at the start frame.");
			sb.AppendLine("Use the EXACT camera names from the list above.");

			return sb.ToString();
		}
	}
}
