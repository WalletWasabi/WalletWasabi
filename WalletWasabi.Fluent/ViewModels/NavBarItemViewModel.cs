using ReactiveUI;
using System;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Fluent.ViewModels
{
	public abstract class NavBarItemViewModel : ViewModelBase, IComparable<NavBarItemViewModel>
	{		
		private bool _isSelected;
		private bool _isExpanded;
		private string _title;

		public bool IsExpanded
		{
			get => _isExpanded;
			set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
		}

		public string Title
		{
			get => _title;
			set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public bool IsSelected
		{
			get { return _isSelected; }
			set { this.RaiseAndSetIfChanged(ref _isSelected, value); }
		}

		public abstract int CompareTo([AllowNull] NavBarItemViewModel other);
	}
}
