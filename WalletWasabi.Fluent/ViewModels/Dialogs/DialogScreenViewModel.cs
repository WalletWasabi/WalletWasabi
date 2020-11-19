using System;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class DialogScreenViewModel : ViewModelBase, IScreen
	{
		private bool _isClosing;
		private bool _isDialogOpen;

		public DialogScreenViewModel()
		{
			Observable.FromEventPattern(Router.NavigationStack, nameof(Router.NavigationStack.CollectionChanged))
				.Subscribe(_ =>
				{
					if (!_isClosing)
					{
						IsDialogOpen = Router.NavigationStack.Count >= 1;
					}
				});

			this.WhenAnyValue(x => x.IsDialogOpen)
				.Skip(1) // Skip the initial value change (which is false).
				.DistinctUntilChanged()
				.Subscribe(x =>
			{
				if (!x && !_isClosing)
				{
					// Reset navigation when Dialog is using IScreen for navigation instead of the default IDialogHost.
					Close();
				}
			});
		}

		public RoutingState Router { get; } = new RoutingState();

		public ReactiveCommand<Unit, Unit> GoBack => Router.NavigateBack;

		public bool IsDialogOpen
		{
			get => _isDialogOpen;
			set => this.RaiseAndSetIfChanged(ref _isDialogOpen, value);
		}

		public void Close()
		{
			if (!_isClosing)
			{
				_isClosing = true;
				if (Router.NavigationStack.Count >= 1)
				{
					foreach (var routable in Router.NavigationStack)
					{
						// Close all dialogs so the awaited tasks can complete.
						// - DialogViewModelBase.ShowDialogAsync()
						// - DialogViewModelBase.GetDialogResultAsync()
						if (routable is DialogViewModelBase dialog)
						{
							dialog.IsDialogOpen = false;
						}
					}
					Router.NavigationStack.Clear();
				}
				IsDialogOpen = false;
				_isClosing = false;
			}
		}
	}
}