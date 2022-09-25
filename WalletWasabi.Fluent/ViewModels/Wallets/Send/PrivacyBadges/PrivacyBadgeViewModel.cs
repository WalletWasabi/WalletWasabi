using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public abstract partial class PrivacyBadgeViewModel : ViewModelBase
{
	[AutoNotify] private string _badgeName = "";
	[AutoNotify] private bool _isVisible = true;
	[AutoNotify] private bool _isOpen;
	[AutoNotify] private PrivacyBadgeStatus _status;
	[AutoNotify] private string _description = "";
	[AutoNotify] private string? _reason;
	[AutoNotify] private SuggestionViewModel? _previewSuggestion;
	[AutoNotify] private SuggestionViewModel? _selectedSuggestion;

	public PrivacyBadgeViewModel()
	{
		this.WhenAnyValue(x => x.IsOpen)
			.Subscribe(x =>
			{
				if (!x)
				{
					PreviewSuggestion = null;
				}
			});

		this.WhenAnyValue(x => x.SelectedSuggestion)
			.Where(x => x is { IsEnabled: true })
			.Subscribe(_ => IsOpen = false);

		this.WhenAnyValue(x => x.Status)
			.Subscribe(x =>
			{
				this.RaisePropertyChanged(nameof(IsAchieved));
				this.RaisePropertyChanged(nameof(IsMinor));
				this.RaisePropertyChanged(nameof(IsMajor));
				this.RaisePropertyChanged(nameof(IsSevere));
			});
	}

	public bool IsAchieved => Status == PrivacyBadgeStatus.Achieved;
	public bool IsMinor => Status == PrivacyBadgeStatus.Minor;
	public bool IsMajor => Status == PrivacyBadgeStatus.Major;
	public bool IsSevere => Status == PrivacyBadgeStatus.Severe;

	public ObservableCollection<SuggestionViewModel> Suggestions { get; } = new();
}
