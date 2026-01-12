using iRacingReplayDirector.AI.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iRacingReplayDirector.AI.LLM
{
	public abstract class LLMProviderBase : ILLMProvider
	{
		protected static readonly HttpClient SharedHttpClient;

		static LLMProviderBase()
		{
			SharedHttpClient = new HttpClient();
			SharedHttpClient.Timeout = TimeSpan.FromSeconds(60);
		}

		public abstract string Name { get; }

		public abstract string ModelName { get; }

		public abstract bool IsConfigured { get; }

		public abstract bool RequiresApiKey { get; }

		protected abstract string Endpoint { get; }

		protected abstract string GetAuthorizationHeader();

		public async Task<CameraPlan> GenerateCameraPlanAsync(RaceEventSummary summary, CancellationToken cancellationToken = default)
		{
			if (!IsConfigured)
				throw new InvalidOperationException($"{Name} is not configured properly.");

			string systemPrompt = PromptTemplates.SystemPrompt;
			string userPrompt = PromptTemplates.BuildUserPrompt(summary);

			var requestBody = new
			{
				model = ModelName,
				messages = new[]
				{
					new { role = "system", content = systemPrompt },
					new { role = "user", content = userPrompt }
				},
				temperature = 0.7,
				max_tokens = 4096
			};

			string jsonBody = JsonConvert.SerializeObject(requestBody);
			var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

			using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
			{
				request.Content = content;

				string authHeader = GetAuthorizationHeader();
				if (!string.IsNullOrEmpty(authHeader))
				{
					request.Headers.TryAddWithoutValidation("Authorization", authHeader);
				}

				var response = await SharedHttpClient.SendAsync(request, cancellationToken);
				response.EnsureSuccessStatusCode();

				string responseJson = await response.Content.ReadAsStringAsync();
				return ParseResponse(responseJson, summary);
			}
		}

		public string LastError { get; protected set; }

		public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
		{
			LastError = null;

			if (!IsConfigured)
			{
				LastError = "Provider is not configured";
				return false;
			}

			try
			{
				var requestBody = new
				{
					model = ModelName,
					messages = new[]
					{
						new { role = "user", content = "Say 'OK' if you can read this." }
					},
					temperature = 0,
					max_tokens = 10
				};

				string jsonBody = JsonConvert.SerializeObject(requestBody);

				using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
				{
					request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

					string authHeader = GetAuthorizationHeader();
					if (!string.IsNullOrEmpty(authHeader))
					{
						request.Headers.TryAddWithoutValidation("Authorization", authHeader);
					}

					using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
					{
						cts.CancelAfter(TimeSpan.FromSeconds(30));
						var response = await SharedHttpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

						if (response.IsSuccessStatusCode)
						{
							return true;
						}

						// Read error response to provide better feedback
						try
						{
							string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
							var errorObj = JObject.Parse(errorBody);
							LastError = errorObj["error"]?["message"]?.ToString()
								?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
						}
						catch
						{
							LastError = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
						}

						return false;
					}
				}
			}
			catch (TaskCanceledException)
			{
				LastError = "Connection timed out";
				return false;
			}
			catch (HttpRequestException httpEx)
			{
				LastError = $"Network error: {httpEx.Message}";
				return false;
			}
			catch (Exception ex)
			{
				LastError = ex.Message;
				return false;
			}
		}

		protected virtual CameraPlan ParseResponse(string responseJson, RaceEventSummary summary)
		{
			var responseObj = JObject.Parse(responseJson);
			var contentStr = responseObj["choices"]?[0]?["message"]?["content"]?.ToString();

			if (string.IsNullOrEmpty(contentStr))
				throw new Exception("Empty response from LLM");

			// Try to extract JSON from the response (it might be wrapped in markdown code blocks)
			string jsonContent = ExtractJson(contentStr);

			var plan = JsonConvert.DeserializeObject<CameraPlan>(jsonContent);

			if (plan == null)
				plan = new CameraPlan();

			plan.GeneratedBy = $"{Name} ({ModelName})";
			plan.GeneratedAt = DateTime.Now;
			plan.TotalDurationFrames = summary.TotalFrames;

			return plan;
		}

		private string ExtractJson(string content)
		{
			// Remove markdown code block markers if present
			content = content.Trim();

			if (content.StartsWith("```json"))
			{
				content = content.Substring(7);
			}
			else if (content.StartsWith("```"))
			{
				content = content.Substring(3);
			}

			if (content.EndsWith("```"))
			{
				content = content.Substring(0, content.Length - 3);
			}

			content = content.Trim();

			// Find the JSON object boundaries
			int startIdx = content.IndexOf('{');
			int endIdx = content.LastIndexOf('}');

			if (startIdx >= 0 && endIdx > startIdx)
			{
				content = content.Substring(startIdx, endIdx - startIdx + 1);
			}

			return content;
		}
	}
}
