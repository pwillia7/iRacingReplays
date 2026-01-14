using iRacingReplayDirector.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace iRacingReplayDirector.AI.LLM
{
	public static class PromptTemplates
	{
		public static string SystemPrompt => @"You are a motorsport broadcast director planning CAMERA SEQUENCES for iRacing replays.

YOUR JOB: Plan camera angles for each broadcast segment. The system automatically selects which driver to follow - you choose the CAMERA ANGLES and plan how to flow through each segment.

FRAME NUMBERS: At 60fps: 1 second = 60 frames, 1 minute = 3600 frames.

CAMERA CATEGORIES (use ALL categories for variety):

WIDE/ESTABLISHING:
- TV1, TV2, TV3: Trackside broadcast cameras - great for showing gaps, field spread
- Chopper, Blimp: Aerial views - pack racing, restarts, showing multiple cars

CHASE/ACTION:
- Rear Chase: Behind the car - excellent for battles, overtakes, close racing
- Gearbox: Low rear angle - dramatic for acceleration, exits

ONBOARD/POV:
- Cockpit: Driver's view - intense moments, key passes
- Roll Bar: Over driver's shoulder - good visibility of track ahead

DYNAMIC/UNIQUE:
- Nose: Front-facing - shows approaching corners, other cars ahead
- Gyro: Stabilized onboard - smooth dramatic shots
- LF Susp, RF Susp, LR Susp, RR Susp: Suspension cameras - low angle wheel views, great for curbs and close racing

DO NOT USE: Scenic, Pit Lane, Pit Lane 2, Chase, Far Chase

VARIETY IS KEY: Use cameras from ALL categories throughout the broadcast. Don't over-rely on just TV and Rear Chase.

CAMERA FLOW PATTERNS:

For BATTLES: Wide (TV) -> Action (Rear Chase/Gearbox) -> POV (Cockpit/Roll Bar) -> Unique (Gyro/Suspension)
For OVERTAKES: Action camera during pass -> POV for reaction -> back to action
For INCIDENTS: Wide for context -> POV for driver perspective
For GAPS: Mix of Wide and Dynamic cameras to keep visuals interesting

PACING RULES:
1. Each camera should last 5-12 seconds (300-720 frames)
2. Never use the same camera twice in a row
3. Mix camera categories - don't use two Wide shots back-to-back
4. Use Dynamic/Unique cameras (Gyro, Nose, Suspension) every 4-5 cuts for variety
5. Transition from gap to event: cut to action camera as event begins

OUTPUT FORMAT - Respond with ONLY valid JSON:
{""cameraActions"":[{""frame"":1000,""cameraName"":""TV1"",""reason"":""Wide establishing shot""},{""frame"":1400,""cameraName"":""Rear Chase"",""reason"":""Battle action""},{""frame"":1900,""cameraName"":""Gyro"",""reason"":""Dynamic angle for variety""}]}

Required fields: frame (int), cameraName (string), reason (string)";

		public static string BuildUserPrompt(RaceEventSummary summary)
		{
			var sb = new StringBuilder();

			int fps = summary.FrameRate > 0 ? summary.FrameRate : 60;
			int totalFrames = summary.TotalFrames > 0 ? summary.TotalFrames : (summary.EndFrame - summary.StartFrame);
			double durationSeconds = totalFrames / (double)fps;
			double durationMinutes = durationSeconds / 60.0;

			sb.AppendLine("=== BROADCAST CAMERA PLAN REQUEST ===");
			sb.AppendLine();
			sb.AppendLine($"Track: {summary.TrackName ?? "Unknown Track"}");
			sb.AppendLine($"Session: {summary.SessionType ?? "Race"}");
			sb.AppendLine($"Duration: {durationMinutes:F1} minutes ({durationSeconds:F0} seconds)");
			sb.AppendLine($"Frame Range: {summary.StartFrame} to {summary.EndFrame} ({totalFrames} frames at {fps}fps)");
			sb.AppendLine();

			// Available cameras
			sb.AppendLine("=== AVAILABLE CAMERAS (use exact names) ===");
			if (summary.AvailableCameras != null && summary.AvailableCameras.Count > 0)
			{
				var excludedCameras = new[] { "Scenic", "Pit Lane", "Pit Lane 2", "Chase", "Far Chase" };
				var validCameras = summary.AvailableCameras
					.Where(c => !excludedCameras.Any(ex => c.GroupName.Equals(ex, StringComparison.OrdinalIgnoreCase)))
					.Select(c => c.GroupName);
				sb.AppendLine(string.Join(", ", validCameras));
			}
			else
			{
				sb.AppendLine("TV1, TV2, TV3, Cockpit, Rear Chase, Chopper, Blimp, Roll Bar, Gyro, Nose, Gearbox, LF Susp, RF Susp, LR Susp, RR Susp");
			}
			sb.AppendLine();

			// Build broadcast segments from events
			var segments = BuildBroadcastSegments(summary, fps);

			sb.AppendLine("=== BROADCAST SEGMENTS ===");
			sb.AppendLine("Plan camera switches for each segment. Follow the camera count guidance.");
			sb.AppendLine();

			int totalCamerasNeeded = 0;
			int segmentNum = 1;

			foreach (var segment in segments)
			{
				sb.AppendLine($"SEGMENT {segmentNum}: Frames {segment.StartFrame}-{segment.EndFrame} ({segment.DurationSeconds:F0}s)");

				if (segment.IsGap)
				{
					sb.AppendLine($"  Type: GAP (no major events)");
					sb.AppendLine($"  Cameras needed: {segment.RecommendedCameras}");
					sb.AppendLine($"  -> Use TV or Chopper for wide field coverage");
				}
				else
				{
					sb.AppendLine($"  Type: {segment.EventType.ToString().ToUpper()}");
					if (!string.IsNullOrEmpty(segment.Description))
						sb.AppendLine($"  Details: {segment.Description}");
					sb.AppendLine($"  Cameras needed: {segment.RecommendedCameras}");
					sb.AppendLine($"  -> {segment.CameraGuidance}");
				}

				sb.AppendLine();
				totalCamerasNeeded += segment.RecommendedCameras;
				segmentNum++;
			}

			// Ensure minimum camera count based on total duration (at least 1 per 10 seconds)
			int minimumCameras = Math.Max(5, (int)(durationSeconds / 10));
			if (minimumCameras > 80) minimumCameras = 80; // Cap at 80 for very long replays

			if (totalCamerasNeeded < minimumCameras)
			{
				totalCamerasNeeded = minimumCameras;
			}

			sb.AppendLine("=== YOUR TASK ===");
			sb.AppendLine($"Create approximately {totalCamerasNeeded} camera switches total.");
			sb.AppendLine($"First camera MUST be at frame {summary.StartFrame}.");
			sb.AppendLine($"Spread cameras evenly - approximately one every {(int)(durationSeconds / totalCamerasNeeded)} seconds.");
			sb.AppendLine();
			sb.AppendLine("CRITICAL RULES:");
			sb.AppendLine("1. Create the requested number of camera switches");
			sb.AppendLine("2. Use action cameras (Rear Chase, Cockpit) during events");
			sb.AppendLine("3. Use wide cameras (TV, Chopper) during gaps and to establish");
			sb.AppendLine("4. Build drama: wide shot -> close action -> intense POV");
			sb.AppendLine("5. NEVER use the same camera twice in a row");
			sb.AppendLine("6. Cover the ENTIRE replay from start to end frame");
			sb.AppendLine();
			sb.AppendLine("Respond with ONLY the JSON object, no other text.");

			return sb.ToString();
		}

		private static List<BroadcastSegment> BuildBroadcastSegments(RaceEventSummary summary, int fps)
		{
			var segments = new List<BroadcastSegment>();

			if (summary.Events == null || summary.Events.Count == 0)
			{
				// No events - entire replay is a gap
				int totalFrames = summary.EndFrame - summary.StartFrame;
				double durationSec = totalFrames / (double)fps;
				int cameras = CalculateGapCameras(durationSec);

				segments.Add(new BroadcastSegment
				{
					StartFrame = summary.StartFrame,
					EndFrame = summary.EndFrame,
					DurationSeconds = durationSec,
					IsGap = true,
					RecommendedCameras = cameras,
					CameraGuidance = "Mix all camera types: Wide (TV/Chopper/Blimp), Onboard (Cockpit/Roll Bar), Dynamic (Gyro/Nose/Gearbox/Suspension)"
				});

				return segments;
			}

			// Sort events by frame
			var sortedEvents = summary.Events.OrderBy(e => e.Frame).ToList();

			int currentFrame = summary.StartFrame;

			foreach (var evt in sortedEvents)
			{
				int eventStart = evt.Frame;
				int eventEnd = evt.Frame + Math.Max(evt.DurationFrames, fps * 3); // Minimum 3 seconds per event

				// Check for gap before this event
				if (eventStart > currentFrame + (fps * 2)) // Gap of at least 2 seconds
				{
					double gapDuration = (eventStart - currentFrame) / (double)fps;
					int gapCameras = CalculateGapCameras(gapDuration);

					segments.Add(new BroadcastSegment
					{
						StartFrame = currentFrame,
						EndFrame = eventStart,
						DurationSeconds = gapDuration,
						IsGap = true,
						RecommendedCameras = gapCameras,
						CameraGuidance = gapCameras <= 2
							? "Mix Wide (TV/Chopper) with unique angles (Gyro/Nose)"
							: "Alternate Wide (TV/Chopper/Blimp) with Dynamic (Gyro/Nose/Suspension) cameras"
					});
				}

				// Add the event segment
				double eventDuration = (eventEnd - eventStart) / (double)fps;
				var eventSegment = CreateEventSegment(evt, eventStart, eventEnd, eventDuration);
				segments.Add(eventSegment);

				currentFrame = eventEnd;
			}

			// Check for gap after last event
			if (currentFrame < summary.EndFrame - (fps * 2))
			{
				double gapDuration = (summary.EndFrame - currentFrame) / (double)fps;
				int gapCameras = CalculateGapCameras(gapDuration);

				segments.Add(new BroadcastSegment
				{
					StartFrame = currentFrame,
					EndFrame = summary.EndFrame,
					DurationSeconds = gapDuration,
					IsGap = true,
					RecommendedCameras = gapCameras,
					CameraGuidance = gapCameras <= 2
						? "Mix Wide (TV/Chopper) with unique angles for closing"
						: "Alternate Wide (TV/Blimp) with Dynamic (Gyro/Nose/Gearbox) for variety"
				});
			}

			// Merge overlapping or adjacent event segments
			segments = MergeOverlappingSegments(segments);

			return segments;
		}

		private static BroadcastSegment CreateEventSegment(RaceEvent evt, int startFrame, int endFrame, double durationSec)
		{
			var segment = new BroadcastSegment
			{
				StartFrame = startFrame,
				EndFrame = endFrame,
				DurationSeconds = durationSec,
				IsGap = false,
				EventType = evt.EventType,
				Description = evt.Description ?? $"P{evt.Position} action"
			};

			// Calculate cameras needed based on event type and duration
			// Base rate: 1 camera per 8 seconds for events (more frequent than gaps)
			int baseCameras = Math.Max(1, (int)(durationSec / 8));

			switch (evt.EventType)
			{
				case RaceEventType.Battle:
					// Battles get more camera variety
					segment.RecommendedCameras = Math.Max(baseCameras, durationSec < 10 ? 1 : durationSec < 20 ? 2 : 3);
					segment.CameraGuidance = durationSec < 10
						? "Use Rear Chase or Gearbox for close action"
						: durationSec < 20
							? "TV/Chopper (establish) -> Rear Chase/Gearbox (action)"
							: "Wide -> Action (Rear Chase/Gearbox) -> POV (Cockpit/Roll Bar) -> Unique (Gyro/Suspension)";
					break;

				case RaceEventType.Overtake:
					segment.RecommendedCameras = Math.Max(baseCameras, durationSec < 8 ? 1 : 2);
					segment.CameraGuidance = durationSec < 8
						? "Use Rear Chase or Nose to capture the pass"
						: "Rear Chase/Gearbox (during pass) -> Cockpit/Gyro (reaction)";
					break;

				case RaceEventType.Incident:
					segment.RecommendedCameras = Math.Max(baseCameras, durationSec < 8 ? 1 : 2);
					segment.CameraGuidance = durationSec < 8
						? "Use TV, Chopper, or Suspension for dramatic angle"
						: "Wide (TV/Chopper) for context -> POV (Cockpit/Roll Bar) for driver view";
					break;

				case RaceEventType.PitStop:
					segment.RecommendedCameras = Math.Max(1, baseCameras);
					segment.CameraGuidance = "Use TV or Gearbox for pit action";
					break;

				default:
					segment.RecommendedCameras = baseCameras;
					segment.CameraGuidance = "Mix Wide (TV/Chopper) with Unique angles (Gyro/Nose/Suspension)";
					break;
			}

			return segment;
		}

		private static int CalculateGapCameras(double durationSec)
		{
			// For gaps, use slightly fewer cameras than events (longer holds)
			// One camera every 10-12 seconds during gaps
			if (durationSec < 10) return 1;
			if (durationSec < 20) return 2;
			return Math.Max(2, (int)(durationSec / 10));
		}

		private static int CalculateEventCameras(double durationSec)
		{
			// For events, more frequent camera changes (every 8 seconds)
			if (durationSec < 8) return 1;
			return Math.Max(1, (int)(durationSec / 8));
		}

		private static List<BroadcastSegment> MergeOverlappingSegments(List<BroadcastSegment> segments)
		{
			if (segments.Count <= 1) return segments;

			var merged = new List<BroadcastSegment>();
			var sorted = segments.OrderBy(s => s.StartFrame).ToList();

			BroadcastSegment current = sorted[0];

			for (int i = 1; i < sorted.Count; i++)
			{
				var next = sorted[i];

				// If segments overlap or are adjacent (within 60 frames = 1 second)
				if (next.StartFrame <= current.EndFrame + 60)
				{
					// Merge: extend current to include next
					current.EndFrame = Math.Max(current.EndFrame, next.EndFrame);
					current.DurationSeconds = (current.EndFrame - current.StartFrame) / 60.0;

					// If merging an event into a gap, the merged segment becomes an event
					if (!next.IsGap)
					{
						current.IsGap = false;
						current.EventType = next.EventType;
						current.Description = next.Description;
					}

					// Recalculate cameras based on NEW merged duration
					if (current.IsGap)
					{
						current.RecommendedCameras = CalculateGapCameras(current.DurationSeconds);
						current.CameraGuidance = "Mix Wide (TV/Chopper/Blimp) with Dynamic (Gyro/Nose/Suspension)";
					}
					else
					{
						// For events, recalculate based on merged duration
						current.RecommendedCameras = CalculateEventCameras(current.DurationSeconds);
						current.CameraGuidance = current.DurationSeconds < 20
							? "Vary: Wide (TV) -> Action (Rear Chase/Gearbox) -> POV (Cockpit/Gyro)"
							: "Full variety: Wide -> Action -> POV -> Unique (Nose/Suspension), repeat pattern";
					}
				}
				else
				{
					// No overlap, add current and move to next
					merged.Add(current);
					current = next;
				}
			}

			// Add the last segment
			merged.Add(current);

			return merged;
		}

		// Helper class for broadcast segments
		private class BroadcastSegment
		{
			public int StartFrame { get; set; }
			public int EndFrame { get; set; }
			public double DurationSeconds { get; set; }
			public bool IsGap { get; set; }
			public RaceEventType EventType { get; set; }
			public string Description { get; set; }
			public int RecommendedCameras { get; set; }
			public string CameraGuidance { get; set; }
		}
	}
}
