namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public struct DialogResult<TResult>
	{
		public DialogResult(TResult? result, DialogResultKind kind)
		{
			Result = result;
			Kind = kind;
		}

		public TResult? Result;

		public DialogResultKind Kind;
	}
}