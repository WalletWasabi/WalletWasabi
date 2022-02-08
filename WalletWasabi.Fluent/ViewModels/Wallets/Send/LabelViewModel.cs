using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class LabelViewModel : ViewModelBase
{
	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isBlackListed;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isHighlighted;

	[AutoNotify(SetterModifier = AccessModifier.Internal)]
	private bool _isFaded;

	[AutoNotify] private bool _isPointerOver;

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
	}

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
