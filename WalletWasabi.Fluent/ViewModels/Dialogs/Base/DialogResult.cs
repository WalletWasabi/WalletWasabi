namespace WalletWasabi.Fluent.ViewModels.Dialogs.Base;

public readonly struct DialogResult<TResult>
{
	public DialogResult(TResult? result, DialogResultKind kind)
	{
		Result = result;
		Kind = kind;
	}

	public TResult? Result { get; }

	public DialogResultKind Kind { get; }
}
