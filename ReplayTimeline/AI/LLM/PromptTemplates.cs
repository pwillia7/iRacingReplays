using iRacingReplayDirector.AI.Models;
using System.Linq;
using System.Text;

namespace iRacingReplayDirector.AI.LLM
{
	public static class PromptTemplates
	{
		public static string SystemPrompt => @"You are an expert motorsport broadcast director creating camera sequences for iRacing replays.

YOUR ROLE: Select camera angles AND specify which drivers to focus on for dynamic, engaging coverage.

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

CAMERA TYPES (use exact names from the session's available cameras):
- TV cameras (TV1, TV2, TV3): Wide broadcast angles, good for establishing shots and showing field spread
- Rear Chase: Behind-car camera, great for following battles and showing the car ahead
- Cockpit/Roll Bar: Driver's perspective, intense and immersive for key moments
- Chopper/Blimp: Aerial views, excellent for showing pack racing and the overall field
- Nose/Gearbox/Gyro: Unique onboard angles for variety

DO NOT USE: Scenic, Pit Lane, Pit Lane 2, Chase, Far Chase

BROADCAST DIRECTING GUIDELINES:
1. VARIETY: Mix camera types - never use the same camera twice in a row
2. PACING: Switch cameras every 8-15 seconds on average
3. RHYTHM: Wide shot -> Close action -> Wide shot
4. REACT TO EVENTS: Use onboards (Cockpit, Roll Bar) for battles and overtakes
5. SHOW BOTH SIDES: In a battle, show both the attacker and defender
6. Start with an establishing shot (TV, Blimp, or Chopper)

DRIVER FOCUS:
For each camera action, specify which driver(s) to follow:
- driverNumber: The primary driver to focus on (use their car number)
- secondaryDriver: For battles/overtakes, the other driver involved
- switchToSecondaryAtFrame: Frame number to switch focus to the secondary driver (for showing both sides of a battle)
- focusType: ""battle"", ""overtake"", ""incident"", ""leader"", ""field"", or ""pack""

DYNAMIC COVERAGE EXAMPLE:
For a battle between #7 and #12, you might do:
1. Frame 1000: TV1, driverNumber 7, focusType ""battle"" - wide shot of the battle
2. Frame 1600: Rear Chase, driverNumber 7, secondaryDriver 12, switchToSecondaryAtFrame 1900, focusType ""battle"" - follow #7, then switch to #12 to show defense
3. Frame 2200: Cockpit, driverNumber 12, focusType ""battle"" - defender's POV

OUTPUT FORMAT:
Respond with ONLY valid JSON (no markdown, no code blocks, no explanation):
{""cameraActions"":[
{""frame"":1000,""cameraName"":""TV1"",""driverNumber"":7,""focusType"":""leader"",""reason"":""Opening shot on leader""},
{""frame"":1600,""cameraName"":""Rear Chase"",""driverNumber"":7,""secondaryDriver"":12,""switchToSecondaryAtFrame"":1900,""focusType"":""battle"",""reason"":""Battle for P3""}
]}

Each camera action needs: frame (integer), cameraName (string), driverNumber (integer), reason (string).
Optional: focusType, secondaryDriver, switchToSecondaryAtFrame.
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
				sb.AppendLine("TV1, TV2, TV3, Cockpit, Rear Chase, Chopper, Blimp");
			}
			sb.AppendLine();

			// Drivers in the session
			if (summary.Drivers != null && summary.Drivers.Count > 0)
			{
				sb.AppendLine("=== DRIVERS (use car numbers for driverNumber field) ===");
				var topDrivers = summary.Drivers.OrderBy(d => d.EndPosition).Take(15);
				foreach (var driver in topDrivers)
				{
					sb.AppendLine($"#{driver.NumberRaw} {driver.TeamName} - P{driver.EndPosition}");
				}
				sb.AppendLine();
			}

			// Detailed events grouped by type
			if (summary.Events != null && summary.Events.Count > 0)
			{
				// Battles - ongoing close racing
				var battles = summary.Events
					.Where(e => e.EventType == RaceEventType.Battle)
					.OrderByDescending(e => e.ImportanceScore)
					.ThenBy(e => e.Frame)
					.Take(15)
					.ToList();

				if (battles.Any())
				{
					sb.AppendLine("=== BATTLES (close racing - show BOTH drivers!) ===");
					foreach (var evt in battles)
					{
						int durationSec = evt.DurationFrames / fps;
						sb.AppendLine($"Frame {evt.Frame} ({durationSec}s): #{evt.PrimaryDriverNumber} vs #{evt.SecondaryDriverNumber} for P{evt.Position} [importance: {evt.ImportanceScore}/10]");
					}
					sb.AppendLine();
				}

				// Overtakes - position changes
				var overtakes = summary.Events
					.Where(e => e.EventType == RaceEventType.Overtake)
					.OrderByDescending(e => e.ImportanceScore)
					.ThenBy(e => e.Frame)
					.Take(15)
					.ToList();

				if (overtakes.Any())
				{
					sb.AppendLine("=== OVERTAKES (show the pass and reaction!) ===");
					foreach (var evt in overtakes)
					{
						string passed = evt.SecondaryDriverNumber.HasValue ? $"passes #{evt.SecondaryDriverNumber}" : "gains position";
						sb.AppendLine($"Frame {evt.Frame}: #{evt.PrimaryDriverNumber} {passed} for P{evt.Position} [importance: {evt.ImportanceScore}/10]");
					}
					sb.AppendLine();
				}

				// Incidents - off-track, spins, contact
				var incidents = summary.Events
					.Where(e => e.EventType == RaceEventType.Incident)
					.OrderByDescending(e => e.ImportanceScore)
					.ThenBy(e => e.Frame)
					.Take(10)
					.ToList();

				if (incidents.Any())
				{
					sb.AppendLine("=== INCIDENTS (dramatic moments!) ===");
					foreach (var evt in incidents)
					{
						sb.AppendLine($"Frame {evt.Frame}: #{evt.PrimaryDriverNumber} - {evt.Description} [importance: {evt.ImportanceScore}/10]");
					}
					sb.AppendLine();
				}
			}

			// Final instructions
			sb.AppendLine("=== YOUR TASK ===");
			sb.AppendLine($"Create exactly {targetCuts} camera actions spread across the ENTIRE replay.");
			sb.AppendLine($"First camera action MUST be at frame {summary.StartFrame}.");
			sb.AppendLine($"Last camera action should be near frame {summary.EndFrame - (framesPerCut / 2)}.");
			sb.AppendLine();
			sb.AppendLine("IMPORTANT:");
			sb.AppendLine("1. Specify driverNumber for EVERY camera action (use car numbers from driver list)");
			sb.AppendLine("2. For battles: use secondaryDriver and switchToSecondaryAtFrame to show both cars");
			sb.AppendLine("3. For overtakes: start on attacker, switch to defender's reaction");
			sb.AppendLine("4. Use focusType: battle, overtake, incident, leader, field, or pack");
			sb.AppendLine("5. Use a VARIETY of different cameras - never repeat the same camera twice in a row");
			sb.AppendLine();
			sb.AppendLine("Respond with ONLY the JSON object, no other text.");

			return sb.ToString();
		}
	}
}
