using System;

namespace WalletWasabi.QualityGate
{
	// This software runs on CI and fails if the quality of a pull request does not conform to our rules.
	public class Program
	{
		private static void Main(string[] args)
		{
			if (args[0] == "foo")
			{
				Console.WriteLine("Failing gate.");
				throw new Exception("foo");
			}
			else
			{
				Console.WriteLine("Passing gate.");
				return;
			}
		}
	}
}
