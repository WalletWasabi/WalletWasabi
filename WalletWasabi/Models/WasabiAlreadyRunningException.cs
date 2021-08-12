using System;

namespace WalletWasabi.Models
{
	public class WasabiAlreadyRunningException : Exception
	{
		public WasabiAlreadyRunningException() : 
			base("Wasabi is already running. " +
			     "Please close other instances of Wasabi and try launching again.")
		{ }
	}
}