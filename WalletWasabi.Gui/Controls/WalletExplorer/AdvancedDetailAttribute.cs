using System;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
	public sealed class AdvancedDetailAttribute : Attribute
	{
		public AdvancedDetailAttribute(string detailTitle, bool isSensitive = false)
		{
			DetailTitle = detailTitle;
			IsSensitive = isSensitive;
		}

		public string DetailTitle { get; }
		public bool IsSensitive { get; }
	}
}
