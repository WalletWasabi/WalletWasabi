using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(
	Title = "Privacy",
	Caption = "Manage privacy settings",
	Order = 1,
	Category = "Settings",
	Keywords = new[] { "Settings", "Privacy", "Minimal", "Medium", "Strong", "Anonymity", "Level" },
	IconName = "settings_privacy_regular")]
public partial class PrivacySettingsTabViewModel : SettingsTabViewModelBase
{
	[AutoNotify] private int _minAnonScoreTarget;
	[AutoNotify] private int _maxAnonScoreTarget;

	public PrivacySettingsTabViewModel()
	{
		_minAnonScoreTarget = Services.Config.MinAnonScoreTarget;
		_maxAnonScoreTarget = Services.Config.MaxAnonScoreTarget;

		this.WhenAnyValue(
				x => x.MinAnonScoreTarget,
				x => x.MaxAnonScoreTarget)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Skip(1)
			.Subscribe(_ => Save());

		this.WhenAnyValue(x => x.MinAnonScoreTarget)
			.Subscribe(
				x =>
				{
					if (x >= MaxAnonScoreTarget)
					{
						MaxAnonScoreTarget = x + 1;
					}
				});

		this.WhenAnyValue(x => x.MaxAnonScoreTarget)
			.Subscribe(
				x =>
				{
					if (x <= MinAnonScoreTarget)
					{
						MinAnonScoreTarget = x - 1;
					}
				});
	}

	protected override void EditConfigOnSave(Config config)
	{
		config.MinAnonScoreTarget = MinAnonScoreTarget;
		config.MaxAnonScoreTarget = MaxAnonScoreTarget;
	}
}
