using iRacingReplayDirector.AI.Models;
using System.Threading;
using System.Threading.Tasks;

namespace iRacingReplayDirector.AI.LLM
{
	public interface ILLMProvider
	{
		string Name { get; }

		string ModelName { get; }

		bool IsConfigured { get; }

		bool RequiresApiKey { get; }

		Task<CameraPlan> GenerateCameraPlanAsync(RaceEventSummary summary, CancellationToken cancellationToken = default);

		Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
	}
}
