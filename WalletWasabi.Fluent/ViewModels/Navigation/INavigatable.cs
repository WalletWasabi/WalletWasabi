namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public interface INavigatable
	{
		void OnNavigatedTo();

		void OnNavigatedFrom();
	}
}