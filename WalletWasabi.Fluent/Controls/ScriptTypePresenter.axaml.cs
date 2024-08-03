using Avalonia;
using Avalonia.Controls.Primitives;
using WalletWasabi.Fluent.Models.Wallets;


namespace WalletWasabi.Fluent.Controls;

public class ScriptTypePresenter : TemplatedControl
{
	public static readonly StyledProperty<ScriptType> ScriptTypeProperty =
		AvaloniaProperty.Register<ScriptTypePresenter, ScriptType>(nameof(ScriptType));

	public ScriptType ScriptType
	{
		get => GetValue(ScriptTypeProperty);
		set => SetValue(ScriptTypeProperty, value);
	}

	protected override Type StyleKeyOverride => typeof(ScriptTypePresenter);
}
