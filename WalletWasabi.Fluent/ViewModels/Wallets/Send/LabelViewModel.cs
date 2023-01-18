using System.Reactive.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class LabelViewModel : ViewModelBase
{
	[ObservableProperty] // TODO SourceGenerator: private setter
	private bool _isBlackListed;

	[ObservableProperty] // TODO SourceGenerator: private setter
	private bool _isHighlighted;

	[ObservableProperty] // TODO SourceGenerator: private internal
	private bool _isFaded;

	[ObservableProperty] private bool _isPointerOver;
	[ObservableProperty] private string _toolTip;

	public LabelViewModel(LabelSelectionViewModel owner, string label)
	{
		Value = label;

		this.WhenAnyValue(x => x.IsPointerOver)
			.Skip(1)
			.Where(value => value == true)
			.Subscribe(_ =>
			{
				owner.OnPointerOver(this);
				owner.OnFade(this);
			});

		ClickedCommand = ReactiveCommand.Create(() => owner.SwapLabel(this));

		_toolTip = label;
	}

	public bool IsDangerous { get; set; }

	public string Value { get; }

	public ICommand ClickedCommand { get; }

	public void Swap() => IsBlackListed = !IsBlackListed;

	public void Highlight(LabelViewModel triggerSource)
	{
		triggerSource
			.WhenAnyValue(x => x.IsPointerOver)
			.TakeUntil(value => value == false)
			.Subscribe(value => IsHighlighted = value);
	}

	public void Fade(LabelViewModel triggerSource)
	{
		triggerSource
			.WhenAnyValue(x => x.IsPointerOver)
			.TakeUntil(value => value == false)
			.Subscribe(value => IsFaded = value);
	}
}
