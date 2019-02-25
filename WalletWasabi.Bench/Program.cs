using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Validators;
using System.Linq;

namespace WalletWasabi.Benck
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var debug = false;
#if DEBUG
			debug = true;
#endif
			IConfig config = DefaultConfig.Instance;
			if(debug)
			{
				var emptyConfig = ManualConfig.CreateEmpty();
				emptyConfig.Add(JitOptimizationsValidator.DontFailOnError);
				config = emptyConfig;
			} 

			BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
		}
	}
}