using iRacingReplayDirector.AI.Models;
using System.Linq;
using System.Text;

namespace iRacingReplayDirector.AI.LLM
{
	public static class PromptTemplates
	{
		public static string SystemPrompt => @"You are an expert motorsport broadcast director creating camera sequences for iRacing replays.

IMPORTANT: iRacing will automatically follow the 'most exciting' car at any moment. Your job is ONLY to select which CAMERA ANGLE to use and when to switch. You do NOT need to specify which driver to follow.

CRITICAL - FRAME NUMBER CALCULATION:
The replay uses FRAME NUMBERS, not timestamps. You will be given:
- Start frame (e.g., 1000)
- End frame (e.g., 37000)
- Frame rate (typically 60 fps)

To calculate frame numbers:
- Each second = 60 frames (at 60fps)
- To place a camera at 10 seconds into the replay: start_frame + (10 * 60)
- To place a camera at 1 minute into the replay: start_frame + (60 * 60)

EXAMPLE: If start_frame=1000, end_frame=37000, frame_rate=60:
- First shot at start: frame 1000
- Shot at 10 seconds: frame 1000 + 600 = 1600
- Shot at 30 seconds: frame 1000 + 1800 = 2800
- Shot at 1 minute: frame 1000 + 3600 = 4600
- Shot at 5 minutes: frame 1000 + 18000 = 19000

Your camera switches must be SPREAD EVENLY across the ENTIRE frame range from start to end.

CAMERA TYPES (use exact names from the session's available cameras):
- TV cameras (TV1, TV2, TV3): Wide broadcast angles, good for establishing shots
- Chase/Far Chase/Rear Chase: Behind-car cameras, great for following action
- Cockpit/Roll Bar: Driver's perspective, intense and immersive
- Chopper/Blimp: Aerial views, excellent for showing the field
- Nose/Gearbox/Gyro: Unique onboard angles

DO NOT USE: Scenic, Pit Lane (these cameras don't show racing action)

BROADCAST DIRECTING GUIDELINES:
1. VARIETY: Mix camera types - never use the same camera twice in a row
2. PACING: Switch cameras every 8-15 seconds on average
3. RHYTHM: Wide shot -> Close action -> Wide shot
4. Start with an establishing shot (TV, Blimp, or Chopper)

OUTPUT FORMAT:
Respond with ONLY valid JSON (no markdown, no code blocks, no explanation):
{""cameraActions"":[{""frame"":1000,""cameraName"":""TV1"",""reason"":""Opening wide shot""},{""frame"":1600,""cameraName"":""Chase"",""reason"":""Follow the action""}]}

Each camera action needs: frame (integer), cameraName (string), reason (string).
Sort by frame number ascending. Spread actions across the ENTIRE replay duration.";

		public static string BuildUserPrompt(RaceEventSummary summary)
		{
			var sb = new StringBuilder();

			int fps = summary.FrameRate > 0 ? summary.FrameRate : 60;
			int totalFrames = summary.TotalFrames > 0 ? summary.TotalFrames : (summary.EndFrame - summary.StartFrame);
			double durationSeconds = totalFrames / (double)fps;
			double durationMinutes = durationSeconds / 60.0;

			// Calculate target number of cuts (one every 10 seconds on average)
			int targetCuts = (int)(durationSeconds / 10);
			if (targetCuts < 5) targetCuts = 5;
			if (targetCuts > 80) targetCuts = 80;

			// Calculate frames per cut for even spacing
			int framesPerCut = totalFrames / targetCuts;

			sb.AppendLine("=== CAMERA PLAN REQUEST ===");
			sb.AppendLine();
			sb.AppendLine($"Track: {summary.TrackName ?? "Unknown Track"}");
			sb.AppendLine($"Session: {summary.SessionType ?? "Race"}");
			sb.AppendLine();

			// CRITICAL frame information with examples
			sb.AppendLine("=== FRAME NUMBERS (CRITICAL) ===");
			sb.AppendLine($"START FRAME: {summary.StartFrame}");
			sb.AppendLine($"END FRAME: {summary.EndFrame}");
			sb.AppendLine($"TOTAL FRAMES: {totalFrames}");
			sb.AppendLine($"FRAME RATE: {fps} fps");
			sb.AppendLine($"DURATION: {durationMinutes:F1} minutes ({durationSeconds:F0} seconds)");
			sb.AppendLine();

			// Pre-calculated example frames to help the LLM
			sb.AppendLine("=== EXAMPLE FRAME NUMBERS FOR THIS REPLAY ===");
			sb.AppendLine($"Start of replay: {summary.StartFrame}");

			// Calculate some milestone frames
			int quarterFrame = summary.StartFrame + (totalFrames / 4);
			int halfFrame = summary.StartFrame + (totalFrames / 2);
			int threeQuarterFrame = summary.StartFrame + (3 * totalFrames / 4);

			sb.AppendLine($"25% through replay: {quarterFrame}");
			sb.AppendLine($"50% through replay (middle): {halfFrame}");
			sb.AppendLine($"75% through replay: {threeQuarterFrame}");
			sb.AppendLine($"End of replay: {summary.EndFrame}");
			sb.AppendLine();
			sb.AppendLine($"For {targetCuts} camera switches, space them approximately {framesPerCut} frames apart.");
			sb.AppendLine();

			// Available cameras from this session (exclude cameras that don't show racing action)
			sb.AppendLine("=== AVAILABLE CAMERAS (use these exact names) ===");
			if (summary.AvailableCameras != null && summary.AvailableCameras.Count > 0)
			{
				var excludedCameras = new[] { "Scenic", "Pit Lane", "Pit Lane 2", "Chase", "Far Chase" };
				var validCameras = summary.AvailableCameras
					.Where(c => !excludedCameras.Any(ex => c.GroupName.Equals(ex, System.StringComparison.OrdinalIgnoreCase)))
					.Select(c => c.GroupName);
				sb.AppendLine(string.Join(", ", validCameras));
			}
			else
			{
				sb.AppendLine("TV1, TV2, TV3, Cockpit, Chase, Far Chase, Chopper, Blimp");
			}
			sb.AppendLine();

			// Events for context
			if (summary.Events != null && summary.Events.Count > 0)
			{
				sb.AppendLine("=== KEY MOMENTS (adjust cameras around these frames) ===");
				var sortedEvents = summary.Events.OrderBy(e => e.Frame).Take(20);
				foreach (var evt in sortedEvents)
				{
					sb.AppendLine($"Frame {evt.Frame}: {evt.Description}");
				}
				sb.AppendLine();
			}

			// Final instructions
			sb.AppendLine("=== YOUR TASK ===");
			sb.AppendLine($"Create exactly {targetCuts} camera switches spread across the ENTIRE replay.");
			sb.AppendLine($"First camera switch MUST be at frame {summary.StartFrame}.");
			sb.AppendLine($"Last camera switch should be near frame {summary.EndFrame - (framesPerCut / 2)}.");
			sb.AppendLine("Use a VARIETY of different cameras - never repeat the same camera twice in a row.");
			sb.AppendLine();
			sb.AppendLine("Respond with ONLY the JSON object, no other text.");

			return sb.ToString();
		}
	}
}
