using System;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class RestartNeededEventArgs : EventArgs
	{
		public bool IsRestartNeeded { get; init; }
	}
}