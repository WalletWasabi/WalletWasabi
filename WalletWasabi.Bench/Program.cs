using BenchmarkDotNet.Running;

namespace WalletWasabi.Benck
{
	public class Program
	{
		public static void Main(string[] args)
		{
			BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
		}
	}
}