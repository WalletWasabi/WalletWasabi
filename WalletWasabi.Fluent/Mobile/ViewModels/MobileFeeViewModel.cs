using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

public sealed class MobileFeeViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private bool _hasEstimates;
	public MobileFeeViewModel(SendFeeViewModel source)
	{
		Source = source;
		Options = new[] { new MobileFeeOption("Economy", 6, source), new MobileFeeOption("Standard", 3, source), new MobileFeeOption("Priority", 1, source) };
		source.FeeChart.WhenAnyValue(x => x.ConfirmationTargetValues, x => x.SatoshiPerByteValues, x => x.CurrentConfirmationTarget)
			.ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				HasEstimates = source.FeeChart.ConfirmationTargetValues is { Length: > 0 } && source.FeeChart.SatoshiPerByteValues is { Length: > 0 };
				foreach (var option in Options) option.Refresh(HasEstimates);
			}).DisposeWith(_disposables);
	}
	public SendFeeViewModel Source { get; }
	public IReadOnlyList<MobileFeeOption> Options { get; }
	public bool HasEstimates { get => _hasEstimates; private set => this.RaiseAndSetIfChanged(ref _hasEstimates, value); }
	public void Dispose() => _disposables.Dispose();
}

public sealed class MobileFeeOption : ReactiveObject
{
	private readonly double _requestedTarget;
	private readonly SendFeeViewModel _source;
	private double _target;
	private string _rate = "Waiting for estimates";
	private string _estimate = "";
	private bool _selected;
	private bool _available;
	public MobileFeeOption(string title, double target, SendFeeViewModel source)
	{
		Title = title; _requestedTarget = target; _source = source;
		SelectCommand = new MobileActionCommand(() => { if (_available) _source.FeeChart.CurrentConfirmationTarget = _target; });
	}
	public string Title { get; }
	public string Rate { get => _rate; private set => this.RaiseAndSetIfChanged(ref _rate, value); }
	public string Estimate { get => _estimate; private set => this.RaiseAndSetIfChanged(ref _estimate, value); }
	public bool IsSelected { get => _selected; private set => this.RaiseAndSetIfChanged(ref _selected, value); }
	public ICommand SelectCommand { get; }
	public void Refresh(bool available)
	{
		_available = available;
		if (!available || _source.FeeChart.ConfirmationTargetValues is not { Length: > 0 } targets) { Rate = "Waiting for estimates"; IsSelected = false; return; }
		_target = Math.Clamp(_requestedTarget, targets.Min(), targets.Max());
		var rate = _source.FeeChart.GetSatoshiPerByte(_target);
		Rate = $"{rate:0.###} sat/vB";
		Estimate = $"~{Math.Ceiling(_target * 10):0} min";
		IsSelected = Math.Abs(_source.FeeChart.CurrentConfirmationTarget - _target) < 0.01;
	}
}
