using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Services;
using SubscriptionRegistry = Dictionary<Type, List<EventBus.Subscription>>;

public class EventBus
{
	private readonly SubscriptionRegistry _subscriptions = new();
	private readonly object _syncObj = new();

	public IDisposable Subscribe<TEvent>(Action<TEvent> action) where TEvent : notnull
	{
		lock (_syncObj)
		{
			if (!_subscriptions.ContainsKey(typeof(TEvent)))
			{
				_subscriptions.Add(typeof(TEvent), []);
			}

			var subscription = Subscription.Create(action, this);
			_subscriptions[typeof(TEvent)].Add(subscription);
			return subscription;
		}
	}

	private void Unsubscribe(Subscription subscription)
	{
		lock (_syncObj)
		{
			Type type = subscription.Type;
			if (_subscriptions.TryGetValue(type, out var allSubscriptions))
			{
				var subscriptionToRemove = allSubscriptions.FirstOrDefault(x => x == subscription);
				if (subscriptionToRemove != null)
				{
					allSubscriptions.Remove(subscriptionToRemove);
				}
			}
		}
	}

	public void Publish<TEvent>(TEvent eventItem) where TEvent : notnull
	{
		Subscription[] allSubscriptions;
		lock (_syncObj)
		{
			if (!_subscriptions.TryGetValue(typeof(TEvent), out var subscriptions))
			{
				return;
			}

			allSubscriptions = subscriptions.ToArray();
		}

		foreach (var subscription in allSubscriptions)
		{
			try
			{
				subscription.Invoke(eventItem);
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error in event subscriber: {ex}");
			}
		}
	}

	internal class Subscription : IDisposable
	{
		public Type Type { get; }
		private readonly Action<object> _action;
		private readonly EventBus _eventBus;

		public static Subscription Create<TEvent>(Action<TEvent> action, EventBus eventBus) =>
			new(o => action((TEvent)o), typeof(TEvent), eventBus);

		private Subscription(Action<object> action, Type type, EventBus eventBus)
		{
			Type = type;
			_action = action;
			_eventBus = eventBus;
		}

		public void Invoke<TEvent>(TEvent param) where TEvent : notnull
		{
			_action(param);
		}

		public void Dispose()
		{
			_eventBus.Unsubscribe(this);
		}
	}
}


public record ExchangeRateChanged(decimal UsdBtcRate);
public record MiningFeeRatesChanged(FeeRateEstimations AllFeeEstimate);
public record ServerTipHeightChanged(int Height);
public record NewSoftwareVersionAvailable(UpdateManager.UpdateStatus UpdateStatus);
public record NewSoftwareVersionInstallerAvailable(string InstallerPath);

public record InstallOnClosedPreferenceChanged(bool InstallOnClose);
public record IndexerAvailabilityStateChanged(bool IsIndexerAvailable);
public record TorConnectionStateChanged(bool IsTorRunning);

public record TorNetworkStatusChanged(Issue[] ReportedIssues);
public record IndexerIncompatibilityDetected();

public record RpcStatusChanged(Result<ConnectedRpcStatus, string> Status);
public record FilterProcessed(FilterModel Filter);
public record Tick(DateTime DateTime);

public record BitcoinPeersChanged(bool Added, int NodesCount);
public record CpfpInfoArrived;
