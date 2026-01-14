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

AVAILABLE CAMERAS:
- TV1, TV2, TV3: Wide broadcast angles - establishing shots, showing gaps, field spread
- Rear Chase: Behind the car - BEST for battles, overtakes, close racing action
- Cockpit, Roll Bar: Driver POV - intense moments, key passes, incidents
- Chopper, Blimp: Aerial views - pack racing, restarts, multiple cars
- Nose, Gearbox, Gyro: Unique angles - use sparingly for variety

DO NOT USE: Scenic, Pit Lane, Pit Lane 2, Chase, Far Chase

CAMERA FLOW PATTERNS (build dramatic tension):

For BATTLES (close racing):
  Short (<10s): Rear Chase only
  Medium (10-20s): TV (establish) -> Rear Chase (action)
  Long (>20s): TV -> Rear Chase -> Cockpit (intensity) -> Rear Chase

For OVERTAKES:
  Rear Chase (during pass) -> Cockpit (reaction shot, 2-3 seconds after)

For INCIDENTS:
  TV or Chopper (context, see what happened) -> Cockpit (driver perspective)

For GAPS (no events):
  Use TV or Chopper for wide field shots, show the racing environment

PACING RULES:
1. Each camera should last 5-12 seconds (300-720 frames)
2. Never use the same camera twice in a row
3. During events: switch cameras to build drama
4. During gaps: use fewer cuts, let wide shots breathe
5. Transition from gap to event: cut to action camera as event begins

OUTPUT FORMAT - Respond with ONLY valid JSON:
{""cameraActions"":[{""frame"":1000,""cameraName"":""TV1"",""reason"":""Establish field before battle""},{""frame"":1400,""cameraName"":""Rear Chase"",""reason"":""Battle begins - close action""},{""frame"":1900,""cameraName"":""Cockpit"",""reason"":""Intense moment in battle""}]}

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
				sb.AppendLine("TV1, TV2, TV3, Cockpit, Rear Chase, Chopper, Blimp, Roll Bar");
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
					CameraGuidance = "Use TV and Chopper for wide field coverage"
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
						CameraGuidance = "Use TV or Chopper for wide shots, establish the field"
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
					CameraGuidance = "Use TV or Chopper for closing wide shots"
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
						? "Use Rear Chase for close battle action"
						: durationSec < 20
							? "Start with TV (establish), then Rear Chase (action)"
							: "TV (establish) -> Rear Chase (action) -> Cockpit (intense), repeat pattern";
					break;

				case RaceEventType.Overtake:
					segment.RecommendedCameras = Math.Max(baseCameras, durationSec < 8 ? 1 : 2);
					segment.CameraGuidance = durationSec < 8
						? "Use Rear Chase to capture the pass"
						: "Rear Chase (during pass) -> Cockpit (reaction shot 2-3s after)";
					break;

				case RaceEventType.Incident:
					segment.RecommendedCameras = Math.Max(baseCameras, durationSec < 8 ? 1 : 2);
					segment.CameraGuidance = durationSec < 8
						? "Use TV or Chopper for context of incident"
						: "TV/Chopper (context, what happened) -> Cockpit (driver view)";
					break;

				case RaceEventType.PitStop:
					segment.RecommendedCameras = Math.Max(1, baseCameras);
					segment.CameraGuidance = "Use TV for pit lane action";
					break;

				default:
					segment.RecommendedCameras = baseCameras;
					segment.CameraGuidance = "Use TV or Chopper for general coverage";
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
						current.CameraGuidance = "Use TV or Chopper for wide shots";
					}
					else
					{
						// For events, recalculate based on merged duration
						current.RecommendedCameras = CalculateEventCameras(current.DurationSeconds);
						current.CameraGuidance = current.DurationSeconds < 20
							? "Vary between TV, Rear Chase, and Cockpit"
							: "TV (establish) -> Rear Chase (action) -> Cockpit (intense), repeat pattern as needed";
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
