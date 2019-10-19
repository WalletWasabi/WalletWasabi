using System.Runtime.InteropServices;
using BenchmarkDotNet.Running;

namespace WalletWasabi.Bench
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				switcher.Run(args, new AllowNonOptimized());
			}
			else
			{
				switcher.Run(args);
			}
		}
	}
}