using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public static class AdvancedDetailTabHelper
	{
		public static AdvancedDetailTabViewModel GenerateAdvancedDetailTab<T>(T targetVM) where T : ViewModelBase
		{
			return new AdvancedDetailTabViewModel("Dummy");
		}
	}
}