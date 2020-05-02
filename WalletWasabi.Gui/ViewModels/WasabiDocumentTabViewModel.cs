using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using AvalonStudio.Shell;
using Dock.Model;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class WasabiDocumentTabViewModel : ViewModelBase, IDocumentTabViewModel
	{
		private string _title;
		private bool _isSelected;
		private bool _isClosed;
		private object _dialogResult;

		protected WasabiDocumentTabViewModel(string title)
		{
			Title = title;
		}

		private CompositeDisposable Disposables { get; set; }

		public object Context { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public IDockable Parent { get; set; }

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

		// This interface member is called explicitly from Avalonia after the Tab was opened.
		void IDockableViewModel.OnOpen()
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			IsClosed = false;

			OnOpen(Disposables);
		}

		/// <summary>
		/// Called when a tab is opened in the dock for the fist time.
		/// </summary>
		/// <param name="disposables">Disposables add IDisposables to this where Dispose will be called when tab is closed.</param>
		public virtual void OnOpen(CompositeDisposable disposables)
		{
		}

		/// <summary>
		/// Called when the close button on the tab is clicked.
		/// </summary>
		/// <returns>true to confirm close, false to cancel.</returns>
		public virtual bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;

			IsSelected = false;
			IoC.Get<IShell>().RemoveDocument(this);
			IsClosed = true;
			return true;
		}

		public void Select()
		{
			IoC.Get<IShell>().Select(this);
		}

		public async Task<object> ShowDialogAsync()
		{
			DialogResult = null;

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
	}
}
