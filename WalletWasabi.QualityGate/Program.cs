using System;

namespace WalletWasabi.QualityGate
{
	// This software runs on CI and fails if the quality of a pull request does not conform to our rules.
	public static class Program
	{
		public static int Main(string[] args)
		{
			var command = args[0];
			if (command == "prtoolarge")
			{
				return -1;
				// throw new Exception("The pull request is too large. Please break it down to smaller pull requests.");
			}

			return 0;
		}
	}
}
