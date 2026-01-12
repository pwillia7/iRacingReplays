namespace iRacingReplayDirector.AI.LLM
{
	public class LocalModelProvider : LLMProviderBase
	{
		public string EndpointUrl { get; set; } = "http://localhost:11434/v1/chat/completions";

		public string Model { get; set; } = "llama3";

		public override string Name => "Local Model";

		public override string ModelName => Model;

		public override bool IsConfigured => !string.IsNullOrWhiteSpace(EndpointUrl) && !string.IsNullOrWhiteSpace(Model);

		public override bool RequiresApiKey => false;

		protected override string Endpoint => EndpointUrl;

		protected override string GetAuthorizationHeader()
		{
			return null; // Local models typically don't need auth
		}
	}
}
