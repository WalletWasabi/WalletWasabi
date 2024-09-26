using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Backend;

namespace WalletWasabi.Services;

public class ConfigWatcher : PeriodicRunner
{
	public ConfigWatcher(TimeSpan period, WabiSabiConfig config, Action executeWhenChanged) : base(period)
	{
		_config = config;
		_executeWhenChanged = executeWhenChanged;
		config.AssertFilePathSet();
	}

	private readonly WabiSabiConfig _config;
	private readonly Action _executeWhenChanged;

	protected override Task ActionAsync(CancellationToken cancel)
	{
		if (ConfigManager.CheckFileChange(_config.FilePath, _config))
		{
			cancel.ThrowIfCancellationRequested();
			_config.LoadFile(createIfMissing: true);

			_executeWhenChanged();
		}

		return Task.CompletedTask;
	}
}
