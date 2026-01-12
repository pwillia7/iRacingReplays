using iRacingReplayDirector.AI.Models;
using System.Collections.Generic;

namespace iRacingReplayDirector.AI.EventDetection
{
	public interface IEventDetector
	{
		RaceEventType EventType { get; }

		bool IsEnabled { get; set; }

		List<RaceEvent> DetectEvents(List<TelemetrySnapshot> snapshots);
	}
}
