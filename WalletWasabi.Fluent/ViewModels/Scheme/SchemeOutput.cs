namespace WalletWasabi.Fluent.ViewModels.Scheme;

public abstract record SchemeOutput(string Text);

public record SchemeOutputResult(string Text) : SchemeOutput(Text);
public record SchemeOutputError(string Text) : SchemeOutput(Text);
public record SchemeOutputCommand(string Text) : SchemeOutput(Text);
