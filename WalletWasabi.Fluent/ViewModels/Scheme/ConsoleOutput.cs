namespace WalletWasabi.Fluent.ViewModels.Scheme;

public abstract record ConsoleOutput(string Text);

public record ConsoleOutputResult(string Text) : ConsoleOutput(Text);
public record ConsoleOutputError(string Text) : ConsoleOutput(Text);
public record ConsoleOutputCommand(string Text) : ConsoleOutput(Text);
