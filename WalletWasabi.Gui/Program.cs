using System.Runtime.CompilerServices;
using WalletWasabi.Gui;

// This is temporary and to facilitate the migration to new UI.
[assembly: InternalsVisibleTo("WalletWasabi.Fluent")]
[assembly: InternalsVisibleTo("WalletWasabi.Fluent.Desktop")]

// Warning! In Avalonia applications Main must not be async. Otherwise application may not run on OSX.
// See https://github.com/AvaloniaUI/Avalonia/wiki/Unresolved-platform-support-issues
GuiProgram program = new();
program.Run(args);
