using WalletWasabi.Fluent.Desktop;

// Initialization code. Don't use any Avalonia, third-party APIs or any
// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
// yet and stuff might break.
FluentProgram program = new();
program.Run(args);
