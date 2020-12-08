using Avalonia;
using Avalonia.Dialogs;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Extensibility.Platforms;
using Splat;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.CrashReport;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Wallets;

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
			GuiProgram program = new GuiProgram();
			program.Run(args);
		}

	}
}
