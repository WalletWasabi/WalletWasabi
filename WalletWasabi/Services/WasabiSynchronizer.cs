using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class WasabiSynchronizer : NotifyPropertyChangedBase
	{
		#region MembersPropertiesEvents

		public SynchronizeResponse LastResponse { get; private set; }

		public WasabiClient WasabiClient { get; private set; }

		public Network Network { get; private set; }

		private decimal _usdExchangeRate;

		/// <summary>
		/// The Bitcoin price in USD.
		/// </summary>
		public decimal UsdExchangeRate
		{
			get => _usdExchangeRate;
			private set => RaiseAndSetIfChanged(ref _usdExchangeRate, value);
		}

		private AllFeeEstimate _allFeeEstimate;

		public AllFeeEstimate AllFeeEstimate
		{
			get => _allFeeEstimate;
			private set => RaiseAndSetIfChanged(ref _allFeeEstimate, value);
		}

		private TorStatus _torStatus;

		public TorStatus TorStatus
		{
			get => _torStatus;
			private set => RaiseAndSetIfChanged(ref _torStatus, value);
		}

		private BackendStatus _backendStatus;

		public BackendStatus BackendStatus
		{
			get => _backendStatus;
			private set => RaiseAndSetIfChanged(ref _backendStatus, value);
		}

		public TimeSpan MaxRequestIntervalForMixing { get; set; }
		private long _blockRequests; // There are priority requests in queue.

		public bool AreRequestsBlocked() => Interlocked.Read(ref _blockRequests) == 1;

		public void BlockRequests() => Interlocked.Exchange(ref _blockRequests, 1);

		public void EnableRequests() => Interlocked.Exchange(ref _blockRequests, 0);

		public BitcoinStore BitcoinStore { get; private set; }

		public event EventHandler<bool> ResponseArrivedIsGenSocksServFail;

		public event EventHandler<SynchronizeResponse> ResponseArrived;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		private CancellationTokenSource Cancel { get; set; }

		#endregion MembersPropertiesEvents

		#region ConstructorsAndInitializers

		public WasabiSynchronizer(Network network, BitcoinStore bitcoinStore, WasabiClient client)
		{
			CreateNew(network, bitcoinStore, client);
		}

		public WasabiSynchronizer(Network network, BitcoinStore bitcoinStore, Func<Uri> baseUriAction, EndPoint torSocks5EndPoint)
		{
			var client = new WasabiClient(baseUriAction, torSocks5EndPoint);
			CreateNew(network, bitcoinStore, client);
		}

		public WasabiSynchronizer(Network network, BitcoinStore bitcoinStore, Uri baseUri, EndPoint torSocks5EndPoint)
		{
			var client = new WasabiClient(baseUri, torSocks5EndPoint);
			CreateNew(network, bitcoinStore, client);
		}

		private void CreateNew(Network network, BitcoinStore bitcoinStore, WasabiClient client)
		{
			Network = Guard.NotNull(nameof(network), network);
			WasabiClient = Guard.NotNull(nameof(client), client);
			LastResponse = null;
			_running = 0;
			Cancel = new CancellationTokenSource();
			BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
		}

		public void Start(TimeSpan requestInterval, TimeSpan feeQueryRequestInterval, int maxFiltersToSyncAtInitialization)
		{
			Guard.NotNull(nameof(requestInterval), requestInterval);
			Guard.MinimumAndNotNull(nameof(feeQueryRequestInterval), feeQueryRequestInterval, requestInterval);
			Guard.MinimumAndNotNull(nameof(maxFiltersToSyncAtInitialization), maxFiltersToSyncAtInitialization, 0);

			MaxRequestIntervalForMixing = requestInterval; // Let's start with this, it'll be modified from outside.

			if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
			{
				return;
			}

			Task.Run(async () =>
			{
				try
				{
					DateTimeOffset lastFeeQueried = DateTimeOffset.UtcNow - feeQueryRequestInterval;
					bool ignoreRequestInterval = false;
					var hashChain = BitcoinStore.HashChain;
					EnableRequests();
					while (IsRunning)
					{
						try
						{
							while (AreRequestsBlocked())
							{
								await Task.Delay(3000, Cancel.Token);
							}

							EstimateSmartFeeMode? estimateMode = null;
							TimeSpan elapsed = DateTimeOffset.UtcNow - lastFeeQueried;
							if (elapsed >= feeQueryRequestInterval)
							{
								estimateMode = EstimateSmartFeeMode.Conservative;
							}

							SynchronizeResponse response;
							try
							{
								if (!IsRunning)
								{
									return;
								}

								response = await WasabiClient.GetSynchronizeAsync(hashChain.TipHash, maxFiltersToSyncAtInitialization, estimateMode, Cancel.Token).WithAwaitCancellationAsync(Cancel.Token, 300);
								// NOT GenSocksServErr
								BackendStatus = BackendStatus.Connected;
								TorStatus = TorStatus.Running;
								DoNotGenSocksServFail();
							}
							catch (ConnectionException ex)
							{
								TorStatus = TorStatus.NotRunning;
								BackendStatus = BackendStatus.NotConnected;
								HandleIfGenSocksServFail(ex);
								throw;
							}
							catch (TorSocks5FailureResponseException ex)
							{
								TorStatus = TorStatus.Running;
								BackendStatus = BackendStatus.NotConnected;
								HandleIfGenSocksServFail(ex);
								throw;
							}
							catch (Exception ex)
							{
								TorStatus = TorStatus.Running;
								BackendStatus = BackendStatus.Connected;
								HandleIfGenSocksServFail(ex);
								throw;
							}

							if (response.AllFeeEstimate != null && response.AllFeeEstimate.Estimations.Any())
							{
								lastFeeQueried = DateTimeOffset.UtcNow;
								AllFeeEstimate = response.AllFeeEstimate;
							}

							if (response.Filters.Count() == maxFiltersToSyncAtInitialization)
							{
								ignoreRequestInterval = true;
							}
							else
							{
								ignoreRequestInterval = false;
							}

							hashChain.UpdateServerTipHeight(response.BestHeight);
							ExchangeRate exchangeRate = response.ExchangeRates.FirstOrDefault();
							if (exchangeRate != default && exchangeRate.Rate != 0)
							{
								UsdExchangeRate = exchangeRate.Rate;
							}

							if (response.FiltersResponseState == FiltersResponseState.NewFilters)
							{
								var filters = response.Filters;

								var firstFilter = filters.First();
								if (hashChain.TipHeight + 1 != firstFilter.BlockHeight)
								{
									// We have a problem.
									// We have wrong filters, the heights are not in sync with the server's.
									Logger.LogError<WasabiSynchronizer>($"Inconsistent index state detected.{Environment.NewLine}" +
										$"{nameof(hashChain)}.{nameof(hashChain.TipHeight)}:{hashChain.TipHeight}{Environment.NewLine}" +
										$"{nameof(hashChain)}.{nameof(hashChain.HashesLeft)}:{hashChain.HashesLeft}{Environment.NewLine}" +
										$"{nameof(hashChain)}.{nameof(hashChain.TipHash)}:{hashChain.TipHash}{Environment.NewLine}" +
										$"{nameof(hashChain)}.{nameof(hashChain.HashCount)}:{hashChain.HashCount}{Environment.NewLine}" +
										$"{nameof(hashChain)}.{nameof(hashChain.ServerTipHeight)}:{hashChain.ServerTipHeight}{Environment.NewLine}" +
										$"{nameof(firstFilter)}.{nameof(firstFilter.BlockHash)}:{firstFilter.BlockHash}{Environment.NewLine}" +
										$"{nameof(firstFilter)}.{nameof(firstFilter.BlockHeight)}:{firstFilter.BlockHeight}");

									await BitcoinStore.IndexStore.RemoveAllImmmatureFiltersAsync(Cancel.Token, deleteAndCrashIfMature: true);
								}
								else
								{
									await BitcoinStore.IndexStore.AddNewFiltersAsync(filters, Cancel.Token);

									if (filters.Count() == 1)
									{
										Logger.LogInfo<WasabiSynchronizer>($"Downloaded filter for block {firstFilter.BlockHeight}.");
									}
									else
									{
										Logger.LogInfo<WasabiSynchronizer>($"Downloaded filters for blocks from {firstFilter.BlockHeight} to {filters.Last().BlockHeight}.");
									}
								}
							}
							else if (response.FiltersResponseState == FiltersResponseState.BestKnownHashNotFound)
							{
								// Reorg happened
								// 1. Rollback index
								FilterModel reorgedFilter = await BitcoinStore.IndexStore.RemoveLastFilterAsync(Cancel.Token);
								Logger.LogInfo<WasabiSynchronizer>($"REORG Invalid Block: {reorgedFilter.BlockHash}.");

								ignoreRequestInterval = true;
							}
							else if (response.FiltersResponseState == FiltersResponseState.NoNewFilter)
							{
								// We are synced.
								// Assert index state.
								if (response.BestHeight > hashChain.TipHeight) // If the server's tip height is larger than ours, we're missing a filter, our index got corrupted.
								{
									await BitcoinStore.IndexStore.RemoveAllImmmatureFiltersAsync(Cancel.Token, deleteAndCrashIfMature: true);
									// If still bad delete filters and crash the software?
								}
							}

							LastResponse = response;
							ResponseArrived?.Invoke(this, response);
						}
						catch (ConnectionException ex)
						{
							Logger.LogError<CcjClient>(ex);
							try
							{
								await Task.Delay(3000, Cancel.Token); // Give other threads time to do stuff.
							}
							catch (TaskCanceledException ex2)
							{
								Logger.LogTrace<CcjClient>(ex2);
							}
						}
						catch (Exception ex) when (ex is OperationCanceledException
												|| ex is TaskCanceledException
												|| ex is TimeoutException)
						{
							Logger.LogTrace<WasabiSynchronizer>(ex);
						}
						catch (Exception ex)
						{
							Logger.LogError<WasabiSynchronizer>(ex);
						}
						finally
						{
							if (IsRunning && !ignoreRequestInterval)
							{
								try
								{
									int delay = (int)Math.Min(requestInterval.TotalMilliseconds, MaxRequestIntervalForMixing.TotalMilliseconds);
									await Task.Delay(delay, Cancel.Token); // Ask for new index in every requestInterval.
								}
								catch (TaskCanceledException ex)
								{
									Logger.LogTrace<CcjClient>(ex);
								}
							}
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		#endregion ConstructorsAndInitializers

		#region Methods

		private void HandleIfGenSocksServFail(Exception ex)
		{
			// IS GenSocksServFail?
			if (ex.ToString().Contains("GeneralSocksServerFailure", StringComparison.OrdinalIgnoreCase))
			{
				// IS GenSocksServFail
				DoGenSocksServFail();
			}
			else
			{
				// NOT GenSocksServFail
				DoNotGenSocksServFail();
			}
		}

		private void DoGenSocksServFail()
		{
			ResponseArrivedIsGenSocksServFail?.Invoke(this, true);
		}

		private void DoNotGenSocksServFail()
		{
			ResponseArrivedIsGenSocksServFail?.Invoke(this, false);
		}

		public FeeRate GetFeeRate(int feeTarget)
		{
			if (AllFeeEstimate is null)
			{
				throw new InvalidOperationException("Cannot get fee estimations.");
			}
			return AllFeeEstimate.GetFeeRate(feeTarget);
		}

		#endregion Methods

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
			Cancel?.Cancel();
			while (Interlocked.CompareExchange(ref _running, 3, 0) == 2)
			{
				await Task.Delay(50);
			}

			Cancel?.Dispose();
			Cancel = null;
			WasabiClient?.Dispose();
			WasabiClient = null;

			EnableRequests(); // Enable requests (it's possible something is being blocked outside the class by AreRequestsBlocked.
		}
	}
}
