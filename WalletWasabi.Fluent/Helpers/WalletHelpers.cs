using System;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers
{
	public static class WalletHelpers
	{
		public static WalletType GetType(KeyManager keyManager)
		{
			if (keyManager.Icon is { } icon)
			{
				return Enum.TryParse(typeof(WalletType), icon, true, out var typ) && typ is { }
					? (WalletType) typ
					: WalletType.Normal;
			}

			return keyManager.IsHardwareWallet ? WalletType.Hardware : WalletType.Normal;
		}
	}
}
