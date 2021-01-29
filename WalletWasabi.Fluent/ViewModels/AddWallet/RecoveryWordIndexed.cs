namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public readonly struct RecoveryWordIndexed
	{
		public RecoveryWordIndexed(int index, string word)
		{
			Index = index;
			Word = word;
		}

		public int Index { get; }
		public string Word { get; }

		public override string ToString()
		{
			return Word;
		}
	}
}