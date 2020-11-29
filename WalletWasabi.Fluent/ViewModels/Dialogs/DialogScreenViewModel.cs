using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class DialogScreenViewModel : TargettedNavigationStack
	{
		private bool _isDialogOpen;

		public DialogScreenViewModel() : base(NavigationTarget.DialogScreen)
		{
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

		protected override void OnNavigated(RoutableViewModel? oldPage, bool oldInStack, RoutableViewModel newPage, bool newInStack)
		{
			base.OnNavigated(oldPage, oldInStack, newPage, newInStack);

			IsDialogOpen = CurrentPage is { };
		}

		public bool IsDialogOpen
		{
			get => _isDialogOpen;
			set => this.RaiseAndSetIfChanged(ref _isDialogOpen, value);
		}

		private void CloseDialogs(IEnumerable<RoutableViewModel> navigationStack)
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
			var navStack = Stack.ToList();
			Clear();

			CloseDialogs(navStack);
		}
	}
}