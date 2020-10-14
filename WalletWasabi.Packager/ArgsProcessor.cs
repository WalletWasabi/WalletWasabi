using System;

namespace WalletWasabi.Packager
{
	/// <summary>
	/// Class for processing program's command line arguments.
	/// </summary>
	public class ArgsProcessor
	{
		public ArgsProcessor(string[] args)
		{
			Args = args;
		}

		public string[] Args { get; }

		public bool IsReduceOnionsMode() => IsOneOf("reduceonions", "reduceonion");

		public bool IsOnlyBinariesMode() => IsOneOf("onlybinaries");

		public bool IsGetOnionsMode() => IsOneOf("getonions", "getonion");

		public bool IsContinuousDeliveryMode() => IsOneOf("cdelivery");

		private bool IsOneOf(params string[] values)
		{
			foreach (var value in values)
			{
				foreach (var arg in Args)
				{
					if (arg.Trim().TrimStart('-').Equals(value, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}
