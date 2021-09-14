using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Morph;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class WalletBalanceChartTileViewModel : TileViewModel
	{
		private readonly ObservableCollection<HistoryItemViewModel> _history;

		public WalletBalanceChartTileViewModel(ObservableCollection<HistoryItemViewModel> history)
		{
			_history = history;

			Animator = new LineChartAnimatorViewModel();

			TimePeriodOptions = new ObservableCollection<TimePeriodOptionViewModel>();

			foreach (var item in (TimePeriodOption[]) Enum.GetValues(typeof(TimePeriodOption)))
			{
				TimePeriodOptions.Add(new TimePeriodOptionViewModel(item, UpdateSample)
				{
					IsSelected = item == TimePeriodOption.ThreeMonths
				});
			}
		}

		public LineChartAnimatorViewModel Animator { get; }

		public ObservableCollection<TimePeriodOptionViewModel> TimePeriodOptions { get; }

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_history.ToObservableChangeSet()
				.Throttle(TimeSpan.FromMilliseconds(50))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => UpdateSample(TimePeriodOptions.First(x => x.IsSelected)))
				.DisposeWith(disposables);
		}

		private void UpdateSample(TimePeriodOptionViewModel selectedPeriodOption)
		{
			foreach (var item in TimePeriodOptions)
			{
				item.IsSelected = item == selectedPeriodOption;
			}

			var timePeriod = selectedPeriodOption.Option;

			switch (timePeriod)
			{
				case TimePeriodOption.All:
					if (_history.Any())
					{
						var oldest = _history.Last().Date;

						UpdateSample((DateTimeOffset.Now - oldest) / 125, DateTimeOffset.Now - oldest);
					}

					break;

				case TimePeriodOption.Day:
					UpdateSample(TimeSpan.FromHours(0.5), TimeSpan.FromHours(24));
					break;

				case TimePeriodOption.Week:
					UpdateSample(TimeSpan.FromHours(12), TimeSpan.FromDays(7));
					break;

				case TimePeriodOption.Month:
					UpdateSample(TimeSpan.FromDays(1), TimeSpan.FromDays(30));
					break;

				case TimePeriodOption.ThreeMonths:
					UpdateSample(TimeSpan.FromDays(2), TimeSpan.FromDays(90));
					break;

				case TimePeriodOption.SixMonths:
					UpdateSample(TimeSpan.FromDays(3.5), TimeSpan.FromDays(182.5));
					break;

				case TimePeriodOption.Year:
					UpdateSample(TimeSpan.FromDays(7), TimeSpan.FromDays(365));
					break;
			}
		}

		private void UpdateSample(TimeSpan sampleTime, TimeSpan sampleBackFor)
		{
			Animator.StopTimer();

			var sampleLimit = DateTimeOffset.Now - sampleBackFor;

			Animator.XMinimum = sampleLimit.ToUnixTimeMilliseconds();

			var sourceXValues = Animator.XValues;
			var sourceYValues = Animator.YValues;

			if (Animator.AnimationFrames is { })
			{
				if (Animator.CurrentAnimationFrame < Animator.TotalAnimationFrames)
				{
					sourceXValues = Animator.AnimationFrames[Animator.TotalAnimationFrames - 1].XValues;
					sourceYValues = Animator.AnimationFrames[Animator.TotalAnimationFrames - 1].YValues;
				}
			}

			Animator.Source = new PolyLine()
			{
				XValues = new ObservableCollection<double>(sourceXValues),
				YValues = new ObservableCollection<double>(sourceYValues)
			};

			Animator.Target = new PolyLine()
			{
				XValues = new ObservableCollection<double>(),
				YValues = new ObservableCollection<double>()
			};

			Animator.XValues.Clear();
			Animator.YValues.Clear();

			var values = _history.SelectTimeSampleBackwards(
				x => x.Date,
				x => x.Balance,
				sampleTime,
				sampleLimit,
				Money.Zero,
				DateTime.Now);

			foreach (var (timestamp, balance) in values.Reverse())
			{
				Animator.Target.YValues.Add((double)balance.ToDecimal(MoneyUnit.BTC));
				Animator.Target.XValues.Add(timestamp.ToUnixTimeMilliseconds());
			}

			if (Animator.Target.YValues.Any())
			{
				var maxY = Animator.Target.YValues.Max();
				Animator.YLabels = new List<string> { "0", (maxY / 2).ToString("F2"), maxY.ToString("F2") };
			}
			else
			{
				Animator.YLabels = null;
			}

			if (Animator.Target.XValues.Any())
			{
				var minX = Animator.Target.XValues.Min();
				var maxX = Animator.Target.XValues.Max();
				var halfX = minX + ((maxX - minX) / 2);

				var range = DateTimeOffset.FromUnixTimeMilliseconds((long)maxX) -
							DateTimeOffset.FromUnixTimeMilliseconds((long)minX);

				var stringFormatOption = "MMM-d";

				if (range <= TimeSpan.FromDays(1))
				{
					stringFormatOption = "t";
				}
				else if (range <= TimeSpan.FromDays(7))
				{
					stringFormatOption = "ddd MMM-d";
				}

				Animator.XLabels = new List<string>
				{
					DateTimeOffset.FromUnixTimeMilliseconds((long)minX).ToLocalTime().ToString(stringFormatOption),
					DateTimeOffset.FromUnixTimeMilliseconds((long)halfX).ToLocalTime().ToString(stringFormatOption),
					DateTimeOffset.FromUnixTimeMilliseconds((long)maxX).ToLocalTime().ToString(stringFormatOption),
				};
			}
			else
			{
				Animator.XLabels = null;
			}

			Animator.UpdateValues();
		}
	}
}
