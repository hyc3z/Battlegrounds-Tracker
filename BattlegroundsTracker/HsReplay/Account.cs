using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.HsReplay.Enums;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json;

namespace Hearthstone_Deck_Tracker.HsReplay
{
	/// <summary>
	/// HSReplay.net account cache. Collection/upload token tracking has been removed together with the
	/// uploader; only the account identity needed by the Battlegrounds premium features remains.
	/// </summary>
	internal sealed class Account : INotifyPropertyChanged
	{
		public static string CacheFilePath => Path.Combine(Config.Instance.DataDir, "hsreplay.cache");

		private static readonly Lazy<Account> Lazy = new Lazy<Account>(Load);

		private Account()
		{
			HSReplayNetOAuth.AccountDataUpdated += () =>
			{
				if(HSReplayNetOAuth.AccountData != null)
					Update(HSReplayNetOAuth.AccountData.Id, HSReplayNetOAuth.AccountData.Username);
			};
		}

		public void Update(int id, string? username)
		{
			Id = id;
			Username = username;
			LastUpdated = DateTime.Now;
			OnPropertyChanged(nameof(Status));
			Save();
		}

		public void Reset() => Update(0, null);

		public static Account Instance => Lazy.Value;

		[JsonIgnore]
		public AccountStatus Status => Id == 0 ? AccountStatus.Anonymous : AccountStatus.Registered;

		public string? UploadToken { get; set; }

		public bool? TokenClaimed { get; set; }

		public string? Username { get; set; }

		public int Id { get; set; }

		public DateTime LastUpdated { get; set; }

		public override string ToString() => $"Id={Id}, Username={Username}";

		public static bool Save()
		{
			var json = JsonConvert.SerializeObject(Instance);
			try
			{
				using(var sw = new StreamWriter(CacheFilePath))
					sw.WriteLine(json);
				return true;
			}
			catch(Exception ex)
			{
				Log.Error(ex);
				return false;
			}
		}

		public static void DeleteCacheFile()
		{
			if(!File.Exists(CacheFilePath))
				return;
			try
			{
				File.Delete(CacheFilePath);
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
		}

		private static Account Load()
		{
			if(!File.Exists(CacheFilePath))
				return new Account();
			try
			{
				using(var sr = new StreamReader(CacheFilePath))
					return JsonConvert.DeserializeObject<Account>(sr.ReadToEnd()) ?? new Account();
			}
			catch(Exception ex)
			{
				Log.Error(ex);
				return new Account();
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		[NotifyPropertyChangedInvocator]
		private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
