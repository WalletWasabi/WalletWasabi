using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public static class EnumerableExtensions
	{
		public static IEnumerable<(DateTimeOffset timestamp, TResult result)> TimeSampleDataSet<TSource, TResult>(this IEnumerable<TSource> source,
			Func<TSource, DateTimeOffset> timeSampler, Func<TSource, TResult> sampler,
			TimeSpan interval, TimeSpan limit)
		{
			if (!source.Any())
			{
				yield break;
			}

			var currentTime = timeSampler(source.First());
			var endTime = currentTime - limit;

			var lastFound = (timestamp: currentTime, result: sampler(source.First()));

			yield return lastFound;

			currentTime -= interval;

			while (currentTime >= endTime)
			{
				var current = source.FirstOrDefault(x => timeSampler(x) <= currentTime);

				if (current is { })
				{
					lastFound = (currentTime, sampler(current));

					yield return lastFound;
				}
				else
				{
					yield return (currentTime, lastFound.result);
				}

				currentTime -= interval;
			}
		}
	}

	public partial class WalletBalanceChartTileViewModel : TileViewModel
	{
		private readonly ReadOnlyObservableCollection<HistoryItemViewModel> _history;
		[AutoNotify] private ObservableCollection<double> _yValues;
		[AutoNotify] private ObservableCollection<double> _xValues;
		[AutoNotify] private double? _xMinimum;
		[AutoNotify] private List<string> _yLabels;
		[AutoNotify] private List<string> _xLabels;

		private TimeSpan _sampleTime;
		private TimeSpan _sampleLimit;


		public WalletBalanceChartTileViewModel(ReadOnlyObservableCollection<HistoryItemViewModel> history)
		{
			_sampleTime = TimeSpan.FromDays(2);
			_sampleLimit = TimeSpan.FromDays(90);

			_history = history;
			_yValues = new ObservableCollection<double>();
			_xValues = new ObservableCollection<double>();

			var filtered = _history.ToObservableChangeSet().Subscribe(_ => UpdateSample ());

			DayCommand = ReactiveCommand.Create(() =>
			{
				_sampleTime = TimeSpan.FromHours(0.5);
				_sampleLimit = TimeSpan.FromDays(1);
				UpdateSample();
			});

			WeekCommand = ReactiveCommand.Create(() =>
			{
				_sampleTime = TimeSpan.FromHours(12);
				_sampleLimit = TimeSpan.FromDays(7);
				UpdateSample();
			});

			MonthCommand = ReactiveCommand.Create(() =>
			{
				_sampleTime = TimeSpan.FromDays(1);
				_sampleLimit = TimeSpan.FromDays(30);
				UpdateSample();
			});

			ThreeMonthCommand = ReactiveCommand.Create(() =>
			{
				_sampleTime = TimeSpan.FromDays(2);
				_sampleLimit = TimeSpan.FromDays(90);
				UpdateSample();
			});

			SixMonthCommand = ReactiveCommand.Create(() =>
			{
				_sampleTime = TimeSpan.FromDays(3.5);
				_sampleLimit = TimeSpan.FromDays(182.5);
				UpdateSample();
			});

			YearCommand = ReactiveCommand.Create(() =>
			{
				_sampleTime = TimeSpan.FromDays(7);
				_sampleLimit = TimeSpan.FromDays(365);
				UpdateSample();
			});
		}

		private double? GetMinimum(TimeSpan limit)
		{
			var latest = _history.FirstOrDefault();

			if (latest is { })
			{
				return (double) (latest.Date - limit).ToUnixTimeMilliseconds();
			}

			return null;
		}

		private void UpdateSample()
		{
			XMinimum = GetMinimum(_sampleLimit);
			var values = _history.TimeSampleDataSet(x => x.Date, x => x.Balance, _sampleTime, _sampleLimit);

			XValues.Clear();
			YValues.Clear();

			foreach (var (timestamp, balance) in values.Reverse())
			{
				YValues.Add((double) balance.ToDecimal(MoneyUnit.BTC));
				XValues.Add((double) timestamp.ToUnixTimeMilliseconds());
			}

			if (YValues.Any())
			{
				var maxY = YValues.Max();
				YLabels = new List<string> {"0", (maxY / 2).ToString("F2"), maxY.ToString("F2")};
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

				var range = DateTimeOffset.FromUnixTimeMilliseconds((long)maxX) -
				            DateTimeOffset.FromUnixTimeMilliseconds((long)minX);

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

		public ICommand DayCommand { get; }

		public ICommand WeekCommand { get; }

		public ICommand MonthCommand { get; }

		public ICommand ThreeMonthCommand { get; }

		public ICommand SixMonthCommand { get; }

		public ICommand YearCommand { get; }
	}
}