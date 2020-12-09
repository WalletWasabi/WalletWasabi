using System.Runtime.CompilerServices;

// This is temporary and to facilitate the migration to new UI.
[assembly: InternalsVisibleTo("WalletWasabi.Fluent")]
[assembly: InternalsVisibleTo("WalletWasabi.Fluent.Desktop")]

namespace WalletWasabi.Gui
{
	public static class Program
	{
		/// Warning! In Avalonia applications Main must not be async. Otherwise application may not run on OSX.
		/// see https://github.com/AvaloniaUI/Avalonia/wiki/Unresolved-platform-support-issues
		private static void Main(string[] args)
		{
			GuiProgram program = new();
			program.Run(args);
		}
	}
}
