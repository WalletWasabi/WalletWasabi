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

			var lastFound = (currentTime, sampler(source.First()));

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
					yield return lastFound;
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
		private TimeSpan _sampleTime;
		private TimeSpan _sampleLimit;


		public WalletBalanceChartTileViewModel(ReadOnlyObservableCollection<HistoryItemViewModel> history)
		{
			_sampleTime = TimeSpan.FromDays(1);
			_sampleLimit = TimeSpan.FromDays(30);

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

		private void UpdateSample()
		{
			var values = _history.TimeSampleDataSet(x => x.Date, x => x.Balance, _sampleTime, _sampleLimit);

			XValues.Clear();
			YValues.Clear();

			foreach (var (timestamp, balance) in values.Reverse())
			{
				YValues.Add((double) balance.ToDecimal(MoneyUnit.BTC));
				XValues.Add((double) timestamp.ToUnixTimeMilliseconds());
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