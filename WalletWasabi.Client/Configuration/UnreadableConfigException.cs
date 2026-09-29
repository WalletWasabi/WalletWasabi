using System;

namespace WalletWasabi.Client.Configuration;

/// <summary>A config file exists but can't be read. It was left as it is, so the user can fix it or choose the default settings.</summary>
public class UnreadableConfigException(string filePath, Exception innerException) : Exception(
	$"""
	Wasabi can't read '{filePath}':
	{innerException.Message}

	The file was not changed. Fix it and start Wasabi again, or start Wasabi with {PersistentConfigManager.ResetUnreadableConfigArgument} to move it aside and use the default settings.
	""",
	innerException);
