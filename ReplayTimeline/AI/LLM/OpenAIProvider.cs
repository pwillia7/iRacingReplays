namespace iRacingReplayDirector.AI.LLM
{
	public class OpenAIProvider : LLMProviderBase
	{
		private const string OPENAI_ENDPOINT = "https://api.openai.com/v1/chat/completions";

		public string ApiKey { get; set; }

		public string Model { get; set; } = "gpt-4o";

		public override string Name => "OpenAI";

		public override string ModelName => Model;

		public override bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

		public override bool RequiresApiKey => true;

		protected override string Endpoint => OPENAI_ENDPOINT;

		// OpenAI API supports response_format for JSON mode (gpt-4o, gpt-3.5-turbo-1106+)
		protected override bool SupportsJsonResponseFormat => true;

		protected override string GetAuthorizationHeader()
		{
			return $"Bearer {ApiKey}";
		}
	}
}
