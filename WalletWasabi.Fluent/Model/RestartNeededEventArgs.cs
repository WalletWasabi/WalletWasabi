using System;

namespace WalletWasabi.Fluent.Model
{
	public class RestartNeededEventArgs : EventArgs
	{
		public bool IsRestartNeeded { get; init; }
	}
}