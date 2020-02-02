namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public struct AdvancedDetailPair
	{
		public AdvancedDetailPair(string title, bool isSensitive, string targetProperty)
		{
			Title = title;
			IsSensitive = isSensitive;
			TargetProperty = targetProperty;
		}

		public string Title { get; }
		public bool IsSensitive { get; }
		public string TargetProperty { get; }
	}
}