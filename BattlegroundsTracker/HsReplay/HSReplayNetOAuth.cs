using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.HsReplay.Data;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;
using HSReplay.OAuth;
using HSReplay.OAuth.Data;
using HSReplay.Responses;

namespace Hearthstone_Deck_Tracker.HsReplay
{
	/// <summary>
	/// Minimal HSReplay.net OAuth client. The deck uploading, collection uploading and twitch related parts
	/// of the upstream implementation have been removed; only authentication and authenticated API access
	/// (which the Battlegrounds Tier7 features require) remain.
	/// </summary>
	// ReSharper disable InconsistentNaming
	internal sealed class HSReplayNetOAuth
	{
		private static readonly JsonSerializer<OAuthData> Serializer;
		private static readonly Lazy<OAuthClient> Client;
		private static readonly Lazy<OAuthData> Data;
		private static readonly OAuthTokenRefresher Refresher;
		private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromMinutes(5);

		private static readonly int[] Ports = { 17781, 17782, 17783, 17784, 17785, 17786, 17787, 17788, 17789 };
		private const string HSReplayNetClientId = "jIpNwuUWLFI6S3oeQkO3xlW6UCnfogw1IpAbFXqq";
		private const string SuccessUrl = "https://hsdecktracker.net/hsreplaynet/oauth_success/";
		private const string ErrorUrl = "https://hsdecktracker.net/hsreplaynet/oauth_error/";

		public static event Action? Authenticated;
		public static event Action? LoggedOut;
		public static event Action? AccountDataUpdated;

		static HSReplayNetOAuth()
		{
			Serializer = new JsonSerializer<OAuthData>("hsreplay_oauth", true);
			Data = new Lazy<OAuthData>(Serializer.Load);
			Client = new Lazy<OAuthClient>(LoadClient);
			Refresher = new OAuthTokenRefresher(() => Data.Value.TokenData?.AccessToken, HasFreshToken, RefreshTokenData);
		}

		private static readonly Scope[] _requiredScopes = { Scope.FullAccess };

		private static OAuthClient LoadClient() => new OAuthClient(HSReplayNetClientId, Helper.GetUserAgent(), Data.Value.TokenData);

		public static void Save() => Serializer.Save(Data.Value);

		public static async Task<AuthenticationErrorType> Authenticate(string? successUrl = null, string? errorUrl = null)
		{
			Log.Info("Authenticating with HSReplay.net...");
			string url;
			try
			{
				url = Client.Value.GetAuthenticationUrl(_requiredScopes, Ports);
			}
			catch(Exception e)
			{
				Log.Error(e);
				return AuthenticationErrorType.CallbackListener;
			}
			if(string.IsNullOrEmpty(url))
			{
				Log.Error("Authentication failed, could not create callback listener");
				return AuthenticationErrorType.CallbackListener;
			}
			var callbackTask = Client.Value.ReceiveAuthenticationCallback(successUrl ?? SuccessUrl, errorUrl ?? ErrorUrl);
			if(!Helper.TryOpenUrl(url))
				Log.Warn("Could not open the browser for the HSReplay.net authentication.");
			Log.Info("Waiting for callback...");
			var data = await callbackTask;
			if(data == null)
			{
				Log.Error("Authentication failed, received no data");
				return AuthenticationErrorType.Authentication;
			}
			Data.Value.Code = data.Code;
			Data.Value.RedirectUrl = data.RedirectUrl;
			Data.Value.TokenData = null;
			if(!await UpdateToken())
			{
				Log.Error("Authentication failed, could not exchange the authorization code for a token");
				Data.Value.Code = null;
				Data.Value.RedirectUrl = null;
				Save();
				return AuthenticationErrorType.Token;
			}
			Save();
			await UpdateAccountData();
			Authenticated?.Invoke();
			return AuthenticationErrorType.None;
		}

		public static async Task Logout()
		{
			Serializer.DeleteCacheFile();
			Data.Value.Account = null;
			Data.Value.Code = null;
			Data.Value.RedirectUrl = null;
			Data.Value.TokenData = null;
			Data.Value.TokenDataCreatedAt = DateTime.MinValue;
			Save();
			LoggedOut?.Invoke();
			await Task.CompletedTask;
		}

		public static Task<bool> UpdateToken() => Refresher.EnsureFresh();

		private static bool HasFreshToken()
		{
			var data = Data.Value;
			if(data.TokenData == null || data.TokenDataCreatedAt == null)
				return false;
			var expiresIn = TimeSpan.FromSeconds(data.TokenData.ExpiresIn);
			return DateTime.Now - data.TokenDataCreatedAt.Value < expiresIn - TokenRefreshMargin;
		}

		private static async Task<bool> RefreshTokenData()
		{
			var data = Data.Value;
			if(string.IsNullOrEmpty(data.Code) || string.IsNullOrEmpty(data.RedirectUrl))
			{
				Log.Error("Could not update token, we don't have a code or redirect url.");
				return false;
			}
			if(!string.IsNullOrEmpty(data.TokenData?.RefreshToken))
			{
				Log.Info("Refreshing token data...");
				try
				{
					var tokenData = await Client.Value.RefreshToken();
					if(tokenData != null)
					{
						SaveTokenData(tokenData);
						return true;
					}
				}
				catch(Exception e)
				{
					Log.Error(e);
				}
			}
			try
			{
				Log.Info("Fetching new token...");
				var tokenData = await Client.Value.GetToken(data.Code, data.RedirectUrl);
				if(tokenData == null)
				{
					Log.Error("We did not receive any token data.");
					return false;
				}
				SaveTokenData(tokenData);
				return true;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		public static async Task<bool> UpdateAccountData()
		{
			Log.Info("Updating account data...");
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return false;
				}
				var account = await SendWithRetry(c => c.GetHSReplayNetAccount());
				Data.Value.Account = account;
				Save();
				Log.Info($"Found account: {account?.Username ?? "None"}");
				AccountDataUpdated?.Invoke();
				return account != null;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		public static bool IsFullyAuthenticated => IsAuthenticatedFor(_requiredScopes);

		public static bool IsAuthenticatedFor(params Scope[] scopes)
		{
			if(string.IsNullOrEmpty(Data.Value.TokenData?.Scope))
				return false;
			var currentScopes = Data.Value.TokenData!.Scope.Split(' ');
			if(currentScopes.Contains(Scope.FullAccess.Name))
				return true;
			return scopes.All(s => currentScopes.Contains(s.Name));
		}

		public static bool IsAuthenticatedForAnything() => !string.IsNullOrEmpty(Data.Value.TokenData?.Scope);

		public static User? AccountData => Data.Value.Account;

		public static void SaveTokenData(TokenData data)
		{
			Data.Value.TokenData = data;
			Data.Value.TokenDataCreatedAt = DateTime.Now;
			Save();
			Log.Info("Saved token data");
		}

		public static async Task<T?> MakeRequest<T>(Func<OAuthClient, Task<T>> request) where T : class
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return null;
				}
				var response = await SendWithRetry(request);
				Log.Debug(response.ToString());
				return response;
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}

		private static async Task<T> SendWithRetry<T>(Func<OAuthClient, Task<T>> request)
		{
			var accessToken = Refresher.AccessToken;
			try
			{
				return await request(Client.Value);
			}
			catch(WebException e) when(e.Response is HttpWebResponse { StatusCode: HttpStatusCode.Unauthorized })
			{
				Log.Warn("Access token was rejected, refreshing and retrying...");
				if(!await Refresher.RefreshRejected(accessToken))
					throw;
				return await request(Client.Value);
			}
		}

		// requests can't be sent twice, so retries need a new one
		public static async Task<HttpResponseMessage?> SendAsyncWithAuth(Func<HttpRequestMessage> createRequest)
		{
			try
			{
				if(!await UpdateToken())
				{
					Log.Error("Could not update token data");
					return null;
				}
				var accessToken = Refresher.AccessToken;
				var response = await SendWithAuth(createRequest());
				if(response.StatusCode != HttpStatusCode.Unauthorized)
					return response;
				Log.Warn("Access token was rejected, refreshing and retrying...");
				if(!await Refresher.RefreshRejected(accessToken))
					return response;
				response.Dispose();
				return await SendWithAuth(createRequest());
			}
			catch(Exception e)
			{
				Log.Error(e);
				return null;
			}
		}

		private static async Task<HttpResponseMessage> SendWithAuth(HttpRequestMessage req)
		{
			using(req)
			{
				req.Headers.Accept.ParseAdd("application/json");
				var authToken = Data.Value.TokenData!;
				req.Headers.Authorization = new AuthenticationHeaderValue(authToken.TokenType, authToken.AccessToken);
				req.Headers.UserAgent.ParseAdd(Helper.GetUserAgent());
				return await Core.HttpClient.SendAsync(req);
			}
		}
	}
}
