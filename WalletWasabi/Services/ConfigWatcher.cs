using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Services;

public class ConfigWatcher : PeriodicRunner
{
	public ConfigWatcher(TimeSpan period, IConfig config, Action executeWhenChanged) : base(period)
	{
		Config = Guard.NotNull(nameof(config), config);
		ExecuteWhenChanged = Guard.NotNull(nameof(executeWhenChanged), executeWhenChanged);
		config.AssertFilePathSet();
	}

	public IConfig Config { get; }
	public Action ExecuteWhenChanged { get; }

	protected override Task ActionAsync(CancellationToken cancel)
	{
		if (Config.CheckFileChange())
		{
			cancel.ThrowIfCancellationRequested();
			Config.LoadOrCreateDefaultFile();

			ExecuteWhenChanged();
		}
		return Task.CompletedTask;
	}
}
