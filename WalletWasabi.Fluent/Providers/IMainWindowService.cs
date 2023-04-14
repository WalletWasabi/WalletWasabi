namespace WalletWasabi.Fluent.Providers;

public interface IMainWindowService
{
	void Show();

	void Hide();

	void Shutdown(bool restart);
}
