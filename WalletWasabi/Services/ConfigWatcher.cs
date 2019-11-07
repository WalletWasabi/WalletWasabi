using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class ConfigWatcher : PeriodicRunner
	{
		public IConfig Config { get; }
		public Func<Task> ExecuteWhenChangedAsync { get; }

		public ConfigWatcher(TimeSpan period, IConfig config, Func<Task> executeWhenChangedAsync) : base(period, null)
		{
			Config = Guard.NotNull(nameof(config), config);
			ExecuteWhenChangedAsync = Guard.NotNull(nameof(executeWhenChangedAsync), executeWhenChangedAsync);
			config.AssertFilePathSet();
		}

		public override async Task<object> ActionAsync(CancellationToken cancel)
		{
			if (await Config.CheckFileChangeAsync().ConfigureAwait(false))
			{
				cancel.ThrowIfCancellationRequested();
				await Config.LoadOrCreateDefaultFileAsync().ConfigureAwait(false);

				await ExecuteWhenChangedAsync?.Invoke();
			}

			return null;
		}
	}
}
