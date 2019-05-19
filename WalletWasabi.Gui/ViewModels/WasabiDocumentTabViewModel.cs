using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using Dock.Model;
using ReactiveUI;
using System;

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class WasabiDocumentTabViewModel : ViewModelBase, IDocumentTabViewModel
	{
		public WasabiDocumentTabViewModel(string title)
		{
			Title = title;
		}

		public WasabiDocumentTabViewModel()
		{
		}

		public Guid Id { get; set; } = Guid.NewGuid();
		public virtual string Title { get; set; }
		public object Context { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public IView Parent { get; set; }
		private bool _isSelected;

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
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
		}

		public virtual bool OnClose()
		{
			IsSelected = false;
			IoC.Get<IShell>().RemoveDocument(this);

			return true;
		}
	}
}
