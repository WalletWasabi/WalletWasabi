using System.Threading;
using WalletWasabi.WabiSabi.Coordinator;

namespace WalletWasabi.Coordinator;

public class WabiSabiConfigProvider
{
	public WabiSabiConfigProvider(string path)
	{
		_path = path;
		_config = WabiSabiConfig.LoadFile(_path);
	}

	/// <remarks>Constructor for tests which does not lead to config reloading.</remarks>
	public WabiSabiConfigProvider(WabiSabiConfig config)
	{
		_path = null;
		_config = config;
	}

	/// <summary>Path with WabiSabi config, or null if the config is provided directly.</summary>
	private readonly string? _path;
	private readonly Lock _lock = new();

	/// <remarks>Access requires <see cref="_lock"/>.</remarks>
	private WabiSabiConfig _config;

	public WabiSabiConfig GetCurrent()
	{
		if (_path is null)
		{
			lock (_lock)
			{
				return _config;
			}
		}
		else
		{
			// TODO: Consider using a time reference to avoid reading the file too often.
			var newConfig = WabiSabiConfig.LoadFile(_config.FilePath);

			lock (_lock)
			{
				_config = newConfig;
			}

			return newConfig;
		}
	}
}
