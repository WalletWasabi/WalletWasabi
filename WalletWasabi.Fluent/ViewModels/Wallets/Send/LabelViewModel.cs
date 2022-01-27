using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class LabelViewModel : ViewModelBase
{
	[AutoNotify] private bool _isBlackListed;
	[AutoNotify] private bool _isPointerOver;
	[AutoNotify] private bool _isHighlighted;
	[AutoNotify] private bool _mustHave;

	public LabelViewModel(LabelSelectionViewModel owner, string label)
	{
		Value = label;

		this.WhenAnyValue(x => x.IsPointerOver)
			.Skip(1)
			.Subscribe(isPointerOver => owner.OnPointerOver(this, isPointerOver));

		ClickedCommand = ReactiveCommand.Create(() => owner.SwapLabel(this),
			this.WhenAnyValue(x => x.MustHave, x => x.IsBlackListed).Select(x => x.Item2 || !x.Item1 && !x.Item2));
	}

	public string Value { get; }

	public ICommand ClickedCommand { get; }
}
