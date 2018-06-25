using System;
using System.IO;

namespace WalletWasabi.Gui
{
	public static class Utils
	{
		public static string GetNextWalletName()
		{
			for (int i = 0; i < int.MaxValue; i++)
			{
				if (!File.Exists(Path.Combine(Global.WalletsDir, $"Wallet{i}.json")))
				{
					return $"Wallet{i}";
				}
			}

			throw new NotSupportedException("This is impossible.");
		}
	}
}
