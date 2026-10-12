namespace WalletWasabi.Tor;

/// <summary>
/// Port number selector for the bundled Tor process.
/// </summary>
/// <remarks>By default, it is allowed for Wasabi to choose any available ports for Tor.</remarks>
public class TorPortSelector
{
	public const int PortScanningOffset = 10;
	public const int MaxPortRetriesForAutomaticPorts = 5;

	private int _portRetryAttempt;
	private bool _isScanningStopped;

	/// <param name="settings">Settings requested by the user. A SOCKS port of 0 means that any available port can be used.</param>
	public TorPortSelector(TorSettings settings)
	{
		if (settings.SocksPort == 0 || settings.ControlPort == 0)
		{
			int socksPort = settings.SocksPort == 0 ? TorSettings.DefaultSocksPort : settings.SocksPort;
			int controlPort = settings.ControlPort == 0 ? TorSettings.DefaultControlPort : settings.ControlPort;

			_isScanningStopped = false;
			CurrentSettings = settings with
			{
				SocksPort = socksPort,
				ControlPort = controlPort
			};

			Logger.LogInfo($"Defaulting to ports: SOCKS={socksPort}, Control={controlPort}");
		}
		else
		{
			Logger.LogInfo($"Using user ports: SOCKS={settings.SocksPort}, Control={settings.ControlPort}");
			_isScanningStopped = true;
			CurrentSettings = settings;
		}
	}

	/// <summary>Settings with the ports that should be used right now.</summary>
	public TorSettings CurrentSettings { get; private set; }

	/// <summary>
	/// Moves to the next pair of ports if there are retries left.
	/// </summary>
	/// <param name="reason">Why the current ports cannot be used. It is logged together with the new ports.</param>
	/// <returns><c>true</c> if <see cref="CurrentSettings"/> now contains new ports, <c>false</c> if no retries are left.</returns>
	public bool TryMoveToNextPorts(string reason)
	{
		if (_isScanningStopped || (_portRetryAttempt >= MaxPortRetriesForAutomaticPorts))
		{
			return false;
		}

		var newSocksPort = CurrentSettings.SocksPort + PortScanningOffset;
		var newControlPort = CurrentSettings.ControlPort + PortScanningOffset;
		Logger.LogInfo($"{reason} Trying different ports: SOCKS={newSocksPort}, Control={newControlPort}");

		CurrentSettings = CurrentSettings with
		{
			SocksPort = newSocksPort,
			ControlPort = newControlPort
		};

		_portRetryAttempt++;
		return true;
	}

	public void StopScanning()
	{
		_isScanningStopped = true;
	}
}
