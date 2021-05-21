using System;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers
{
	public static class WalletHelpers
	{
		public static WalletType GetType(KeyManager keyManager)
		{
			if (keyManager.Icon is { } icon &&
			    Enum.TryParse(typeof(WalletType), icon, true, out var type) &&
			    type is { })
			{
				return (WalletType) type;
			}

			return keyManager.IsHardwareWallet ? WalletType.Hardware : WalletType.Normal;
		}
	}
}
