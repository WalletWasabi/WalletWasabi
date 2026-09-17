using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public abstract partial class RoutableViewModel : ViewModelBase, INavigatable
{
	private CompositeDisposable? _currentDisposable;
	private ICommand? _nextCommand;
	private ICommand? _skipCommand;
	private ICommand _backCommand;
	private ICommand _cancelCommand;

	[AutoNotify] private bool _isBusy;
	[AutoNotify] private bool _enableCancelOnPressed;
	[AutoNotify] private bool _enableCancelOnEscape;
	[AutoNotify] private bool _enableBack;
	[AutoNotify] private bool _enableCancel;
	[AutoNotify] private bool _isActive;

	protected RoutableViewModel(UiContext uiContext) : base(uiContext)
	{
		_backCommand = ReactiveCommand.Create(() => Navigate().Back(), this.WhenAnyValue(model => model.IsBusy, b => !b));
		_cancelCommand = ReactiveCommand.Create(() => Navigate().Clear());
	}

	public abstract string Title { get; protected set; }

	public NavigationTarget CurrentTarget { get; internal set; }

	public virtual NavigationTarget DefaultTarget => NavigationTarget.HomeScreen;

	// Some routes recreate commands in OnNavigatedTo. Both compiled bindings and
	// command-activity observers must see replacements. Ownership remains with the route.
	public ICommand? NextCommand
	{
		get => _nextCommand;
		protected set => this.RaiseAndSetIfChanged(ref _nextCommand, value);
	}

	public ICommand? SkipCommand
	{
		get => _skipCommand;
		protected set => this.RaiseAndSetIfChanged(ref _skipCommand, value);
	}

	public ICommand BackCommand
	{
		get => _backCommand;
		protected set => this.RaiseAndSetIfChanged(ref _backCommand, value);
	}

	public ICommand CancelCommand
	{
		get => _cancelCommand;
		protected set => this.RaiseAndSetIfChanged(ref _cancelCommand, value);
	}

	private void DoNavigateTo(bool isInHistory)
	{
		if (_currentDisposable is { })
		{
			throw new Exception("Can't navigate to something that has already been navigated to.");
		}

		_currentDisposable = new CompositeDisposable();

		OnNavigatedTo(isInHistory, _currentDisposable);
	}

	protected virtual void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
	}

	private void DoNavigateFrom(bool isInHistory)
	{
		OnNavigatedFrom(isInHistory);

		_currentDisposable?.Dispose();
		_currentDisposable = null;
	}

	public INavigationStack<RoutableViewModel> Navigate()
	{
		var currentTarget = CurrentTarget == NavigationTarget.Default ? DefaultTarget : CurrentTarget;

		return Navigate(currentTarget);
	}

	public INavigationStack<RoutableViewModel> Navigate(NavigationTarget currentTarget)
	{
		return UiContext.Navigate(currentTarget);
	}

	public void OnNavigatedTo(bool isInHistory)
	{
		DoNavigateTo(isInHistory);
	}

	void INavigatable.OnNavigatedFrom(bool isInHistory)
	{
		DoNavigateFrom(isInHistory);
	}

	protected virtual void OnNavigatedFrom(bool isInHistory)
	{
	}

	protected void EnableAutoBusyOn(params ICommand[] commands)
	{
		foreach (var command in commands)
		{
			(command as IReactiveCommand)?.IsExecuting
				.ObserveOn(RxApp.MainThreadScheduler)
				.Skip(1)
				.Subscribe(x => IsBusy = x);
		}
	}

	public async Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog)
		=> await NavigateDialogAsync(dialog, CurrentTarget);

	public async Task<DialogResult<TResult>> NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationTarget target, NavigationMode navigationMode = NavigationMode.Normal)
	{
		target = NavigationExtensions.GetTarget(this, target);

		return await UiContext.Navigate(target).NavigateDialogAsync(dialog, navigationMode);
	}

	protected async Task ShowErrorAsync(string title, string message, string caption, NavigationTarget navigationTarget = NavigationTarget.Default)
	{
		var target =
			navigationTarget != NavigationTarget.Default
			? navigationTarget
			: CurrentTarget == NavigationTarget.CompactDialogScreen
				? NavigationTarget.CompactDialogScreen
				: NavigationTarget.DialogScreen;

		await Navigate(target).ShowErrorAsync(UiContext, title, message, caption);
	}

	protected void SetupCancel(bool enableCancel, bool enableCancelOnEscape, bool enableCancelOnPressed, bool escapeGoesBack = false)
	{
		EnableCancel = enableCancel;
		EnableCancelOnEscape = enableCancelOnEscape && !escapeGoesBack;
		EnableCancelOnPressed = enableCancelOnPressed;
	}
}
