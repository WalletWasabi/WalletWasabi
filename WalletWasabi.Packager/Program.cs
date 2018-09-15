using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Packager
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			string packagerProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\"));
			string solutionDirectory = Path.GetFullPath(Path.Combine(packagerProjectDirectory, "..\\"));
			string guiProjectDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "WalletWasabi.Gui\\"));
			Console.WriteLine($"{nameof(solutionDirectory)}:\t\t{solutionDirectory}");
			Console.WriteLine($"{nameof(packagerProjectDirectory)}:\t{packagerProjectDirectory}");
			Console.WriteLine($"{nameof(guiProjectDirectory)}:\t\t{guiProjectDirectory}");

			string version = Helpers.Constants.ClientVersion.ToString();
			Console.WriteLine();
			Console.WriteLine($"{nameof(version)}:\t\t\t{version}");

			// https://docs.microsoft.com/en-us/dotnet/articles/core/rid-catalog
			// Tor supports: win32, linux32, linux64, osx64
			// .NET Core supports: win32, win64, linux64, osx64
			// We'll only support x64, if someone complains, we can come back to it.
			var targets = new List<string>
			{
				"win-x64",
				"linux-x64",
				"osx-x64"
			};
			Console.WriteLine();
			Console.Write($"{nameof(targets)}:\t\t\t");
			targets.ForEach(x =>
			{
				if (targets.Last() != x)
				{
					Console.Write($"{x}, ");
				}
				else
				{
					Console.Write(x);
				}
			});
			Console.WriteLine();

			Console.WriteLine();
			Console.WriteLine("FINISHED! Press key to exit...");
			Console.ReadKey();
		}
	}
}
