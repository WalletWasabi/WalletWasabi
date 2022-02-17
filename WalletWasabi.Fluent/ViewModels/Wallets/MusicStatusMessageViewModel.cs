using Avalonia.Threading;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicStatusMessageViewModel : ViewModelBase
{
	[AutoNotify] private string? _message;
}

public class AutoUpdateMusicStatusMessageViewModel : MusicStatusMessageViewModel, IDisposable
{
	private readonly IDisposable _timerSubscription;
	private readonly Func<string> _onUpdate;

	public AutoUpdateMusicStatusMessageViewModel(Func<string> onUpdate)
	{
		_onUpdate = onUpdate;

		Update();

		DispatcherTimer.Run(() =>
		{
			Update();
			return true;
		}, TimeSpan.FromSeconds(1));
	}

	public void Update()
	{
		Message = _onUpdate();
	}

	public void Dispose()
	{
		_timerSubscription.Dispose();
	}
}