using Avalonia.Threading;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using Dock.Model;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class WasabiDocumentTabViewModel : ViewModelBase, IDocumentTabViewModel
	{
		private string _title;
		private bool _isSelected;
		private bool _isClosed;
		private string _warningMessage;
		private string _successMessage;
		private object _dialogResult;

		protected WasabiDocumentTabViewModel(Global global, string title)
		{
			Title = title;
			Global = Guard.NotNull(nameof(global), global);
			DoItCommand = ReactiveCommand.Create(DisplayActionTab);
		}

		public Global Global { get; }
		public Guid Id { get; set; } = Guid.NewGuid();
		public object Context { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public IView Parent { get; set; }

		public string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
		}

		public bool IsClosed
		{
			get => _isClosed;
			set => this.RaiseAndSetIfChanged(ref _isClosed, value);
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		public string SuccessMessage
		{
			get => _successMessage;
			set => this.RaiseAndSetIfChanged(ref _successMessage, value);
		}

		public object DialogResult
		{
			get => _dialogResult;
			set => this.RaiseAndSetIfChanged(ref _dialogResult, value);
		}

		public bool IsDirty { get; set; }

		public virtual void OnSelected()
		{
			IsSelected = true;
		}

		public virtual void OnDeselected()
		{
			IsSelected = false;
		}

		public virtual void OnOpen()
		{
			IsClosed = false;
		}

		public virtual bool OnClose()
		{
			IsSelected = false;
			IoC.Get<IShell>().RemoveDocument(this);
			IsClosed = true;
			return true;
		}

		public void DisplayActionTab()
		{
			IoC.Get<IShell>().AddOrSelectDocument(this);
		}

		public ReactiveCommand<Unit, Unit> DoItCommand { get; }

		public void Select()
		{
			IoC.Get<IShell>().Select(this);
		}

		public async Task<object> ShowDialogAsync()
		{
			DialogResult = null;
			DisplayActionTab();

			while (!IsClosed)
			{
				if (!IsSelected) // Prevent de-selection of tab.
				{
					Select();
				}
				await Task.Delay(100);
			}
			return DialogResult;
		}

		protected void SetWarningMessage(string message)
		{
			SuccessMessage = "";
			WarningMessage = message;

			if (string.IsNullOrWhiteSpace(message))
			{
				return;
			}

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await Task.Delay(7000);
				if (WarningMessage == message)
				{
					WarningMessage = "";
				}
			});
		}

		public void SetSuccessMessage(string message)
		{
			SuccessMessage = message;
			WarningMessage = "";

			if (string.IsNullOrWhiteSpace(message))
			{
				return;
			}

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await Task.Delay(7000);
				if (SuccessMessage == message)
				{
					SuccessMessage = "";
				}
			});
		}
	}
}
