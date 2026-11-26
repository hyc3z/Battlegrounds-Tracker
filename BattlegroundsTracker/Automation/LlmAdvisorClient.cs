using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hearthstone_Deck_Tracker.Automation
{
	internal sealed class LlmAdviceResponse
	{
		public bool Success { get; }
		public string Content { get; }
		public long ElapsedMs { get; }

		public LlmAdviceResponse(bool success, string content, long elapsedMs)
		{
			Success = success;
			Content = content;
			ElapsedMs = elapsedMs;
		}
	}

	/// <summary>
	/// Minimal OpenAI-compatible Chat Completions client (supports OpenAI, DeepSeek, SiliconFlow,
	/// OpenRouter, Qwen/DashScope, Kimi, Ollama, LM Studio, etc.) using Newtonsoft.Json.
	/// </summary>
	internal static class LlmAdvisorClient
	{
		private static readonly HttpClient Http = CreateHttpClient();

		private const string SystemPrompt =
			"你是一位《炉石传说》酒馆战棋万分高分段教练，正在实时观战并给玩家提供简明、可执行的决策建议。\n" +
			"严格基于用户提供的当前场面、手牌、商店、金币、回合数和近期操作记录进行分析，不要编造不存在的卡牌或费用。\n" +
			"请用简洁中文按以下三段输出（总字数控制在 220 字以内，方便对局中快速扫读）：\n" +
			"【操作点评】1~2 句点评玩家近期操作和当前阵容方向（无近期操作则简评当前局势）。\n" +
			"【推荐操作】按先后顺序列出 2~4 步具体动作（精确到牌名、是否升本/刷新/冻结、关键站位调整）。\n" +
			"【核心理由】1~2 句说明经济节奏、即时战力、三连或阵容成型收益。";

		private static HttpClient CreateHttpClient()
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			return new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(45)
			};
		}

		internal static string ResolveCompletionsEndpoint(string baseUrl)
		{
			var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
			if(string.IsNullOrEmpty(trimmed))
				return "https://api.openai.com/v1/chat/completions";
			if(trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
				return trimmed;
			if(trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
				return trimmed + "/chat/completions";
			return trimmed + "/v1/chat/completions";
		}

		internal static Task<LlmAdviceResponse> RequestAdviceAsync(string statePrompt, CancellationToken ct)
			=> SendChatAsync(
				Config.Instance.BgsLlmBaseUrl,
				Config.Instance.BgsLlmApiKey,
				Config.Instance.BgsLlmModel,
				SystemPrompt,
				statePrompt,
				maxTokens: 450,
				ct);

		internal static Task<LlmAdviceResponse> TestConnectionAsync(string baseUrl, string apiKey, string model, CancellationToken ct)
			=> SendChatAsync(
				baseUrl,
				apiKey,
				model,
				"你是一个连接测试助手。",
				"请只回复：连接正常",
				maxTokens: 32,
				ct);

		private static async Task<LlmAdviceResponse> SendChatAsync(
			string baseUrl,
			string apiKey,
			string model,
			string systemPrompt,
			string userPrompt,
			int maxTokens,
			CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			var endpoint = ResolveCompletionsEndpoint(baseUrl);
			var resolvedModel = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model.Trim();

			if(string.IsNullOrWhiteSpace(apiKey) && !endpoint.Contains("localhost") && !endpoint.Contains("127.0.0.1"))
				return new LlmAdviceResponse(false, "未配置 API Key，请先在「设置 → 酒馆战棋 → AI 教练 (LLM)」填写。", 0);

			try
			{
				var payload = new
				{
					model = resolvedModel,
					temperature = 0.3,
					max_tokens = maxTokens,
					messages = new object[]
					{
						new { role = "system", content = systemPrompt },
						new { role = "user", content = userPrompt }
					}
				};

				var json = JsonConvert.SerializeObject(payload);
				using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
				{
					Content = new StringContent(json, Encoding.UTF8, "application/json")
				};
				if(!string.IsNullOrWhiteSpace(apiKey))
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

				using var response = await Http.SendAsync(request, ct);
				var body = await response.Content.ReadAsStringAsync();
				sw.Stop();

				if(!response.IsSuccessStatusCode)
				{
					var errMsg = TryExtractErrorMessage(body) ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
					return new LlmAdviceResponse(false, $"请求失败：{errMsg}", sw.ElapsedMilliseconds);
				}

				var parsed = JObject.Parse(body);
				var content = parsed["choices"]?[0]?["message"]?["content"]?.Value<string>()?.Trim();
				if(string.IsNullOrEmpty(content))
					return new LlmAdviceResponse(false, "模型返回内容为空。", sw.ElapsedMilliseconds);

				return new LlmAdviceResponse(true, content!, sw.ElapsedMilliseconds);
			}
			catch(OperationCanceledException)
			{
				throw;
			}
			catch(Exception ex)
			{
				sw.Stop();
				return new LlmAdviceResponse(false, $"调用异常：{ex.Message}", sw.ElapsedMilliseconds);
			}
		}

		private static string? TryExtractErrorMessage(string body)
		{
			try
			{
				var obj = JObject.Parse(body);
				return obj["error"]?["message"]?.Value<string>()
					?? obj["message"]?.Value<string>();
			}
			catch
			{
				return string.IsNullOrWhiteSpace(body) ? null : body.Substring(0, Math.Min(180, body.Length));
			}
		}
	}
}
