using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class DialogScreenViewModel : ViewModelBase, IScreen
	{
		private bool _isDialogOpen;

		public DialogScreenViewModel()
		{
			Observable.FromEventPattern(Router.NavigationStack, nameof(Router.NavigationStack.CollectionChanged))
				.Subscribe(_ =>
				{
					IsDialogOpen = Router.NavigationStack.Count >= 1;
				});

			this.WhenAnyValue(x => x.IsDialogOpen)
				.Skip(1) // Skip the initial value change (which is false).
				.DistinctUntilChanged()
				.Subscribe(x =>
				{
					if (!x)
					{
						CloseScreen();
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

		private void CloseDialogs(IEnumerable<IRoutableViewModel> navigationStack)
		{
			// Close all dialogs so the awaited tasks can complete.
			// - DialogViewModelBase.ShowDialogAsync()
			// - DialogViewModelBase.GetDialogResultAsync()

			foreach (var routable in navigationStack)
			{
				if (routable is DialogViewModelBase dialog)
				{
					dialog.IsDialogOpen = false;
				}
			}
		}

		private void CloseScreen()
		{
			// Save Router.NavigationStack as it can be modified when closing Dialog.
			var navigationStack = Router.NavigationStack.ToList();

			// Reset navigation when Dialog is using IScreen for navigation instead of the default IDialogHost.
			if (Router.NavigationStack.Count > 0)
			{
				Router.NavigationStack.Clear();
			}

			CloseDialogs(navigationStack);
		}
	}
}