using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Input;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class WalletBalanceChartTileViewModel : TileViewModel
	{
		private readonly ObservableCollection<HistoryItemViewModel> _history;
		[AutoNotify] private ObservableCollection<double> _yValues;
		[AutoNotify] private ObservableCollection<double> _xValues;
		[AutoNotify] private double? _xMinimum;
		[AutoNotify] private List<string>? _yLabels;
		[AutoNotify] private List<string>? _xLabels;
		private TimePeriodOption _currentTimePeriod = TimePeriodOption.ThreeMonths;

		public WalletBalanceChartTileViewModel(ObservableCollection<HistoryItemViewModel> history)
		{
			_history = history;
			_yValues = new ObservableCollection<double>();
			_xValues = new ObservableCollection<double>();

			DayCommand = ReactiveCommand.Create(() => UpdateSample(TimePeriodOption.Day));
			WeekCommand = ReactiveCommand.Create(() => UpdateSample(TimePeriodOption.Week));
			MonthCommand = ReactiveCommand.Create(() => UpdateSample(TimePeriodOption.Month));
			ThreeMonthCommand = ReactiveCommand.Create(() => UpdateSample(TimePeriodOption.ThreeMonths));
			SixMonthCommand = ReactiveCommand.Create(() => UpdateSample(TimePeriodOption.SixMonths));
			YearCommand = ReactiveCommand.Create(() => UpdateSample(TimePeriodOption.Year));
			AllCommand = ReactiveCommand.Create(() => { UpdateSample(TimePeriodOption.All); });
		}

		private enum TimePeriodOption
		{
			All,
			Day,
			Week,
			Month,
			ThreeMonths,
			SixMonths,
			Year
		}

		public ICommand DayCommand { get; }

		public ICommand WeekCommand { get; }

		public ICommand MonthCommand { get; }

		public ICommand ThreeMonthCommand { get; }

		public ICommand SixMonthCommand { get; }

		public ICommand YearCommand { get; }

		public ICommand AllCommand { get; }

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_history.ToObservableChangeSet()
				.Subscribe(_ => UpdateSample())
				.DisposeWith(disposables);
		}

		private void UpdateSample()
		{
			UpdateSample(_currentTimePeriod);
		}

		private void UpdateSample(TimePeriodOption timePeriod)
		{
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

			_currentTimePeriod = timePeriod;
		}

		private void UpdateSample(TimeSpan sampleTime, TimeSpan sampleBackFor)
		{
			var sampleLimit = DateTimeOffset.Now - sampleBackFor;

			XMinimum = sampleLimit.ToUnixTimeMilliseconds();

			var values = _history.SelectTimeSampleBackwards(
				x => x.Date,
				x => x.Balance,
				sampleTime,
				sampleLimit,
				DateTime.Now);

			XValues.Clear();
			YValues.Clear();

			foreach (var (timestamp, balance) in values.Reverse())
			{
				YValues.Add((double) balance.ToDecimal(MoneyUnit.BTC));
				XValues.Add(timestamp.ToUnixTimeMilliseconds());
			}

			if (YValues.Any())
			{
				var maxY = YValues.Max();
				YLabels = new List<string> { "0", (maxY / 2).ToString("F2"), maxY.ToString("F2") };
			}
			else
			{
				YLabels = null;
			}

			if (XValues.Any())
			{
				var minX = XValues.Min();
				var maxX = XValues.Max();
				var halfX = minX + ((maxX - minX) / 2);

				var range = DateTimeOffset.FromUnixTimeMilliseconds((long) maxX) -
				            DateTimeOffset.FromUnixTimeMilliseconds((long) minX);

				if (range <= TimeSpan.FromDays(1))
				{
					XLabels = new List<string>
					{
						DateTimeOffset.FromUnixTimeMilliseconds((long) minX).DateTime.ToString("t"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) halfX).DateTime.ToString("t"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) maxX).DateTime.ToString("t"),
					};
				}
				else if (range <= TimeSpan.FromDays(7))
				{
					XLabels = new List<string>
					{
						DateTimeOffset.FromUnixTimeMilliseconds((long) minX).DateTime.ToString("dddd MMM-d"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) halfX).DateTime.ToString("dddd MMM-d"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) maxX).DateTime.ToString("dddd MMM-d"),
					};
				}
				else
				{
					XLabels = new List<string>
					{
						DateTimeOffset.FromUnixTimeMilliseconds((long) minX).DateTime.ToString("MMM-d"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) halfX).DateTime.ToString("MMM-d"),
						DateTimeOffset.FromUnixTimeMilliseconds((long) maxX).DateTime.ToString("MMM-d"),
					};
				}
			}
			else
			{
				XLabels = null;
			}
		}
	}
}
