using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class ArenaTimeBenchmarker
{
	public ConcurrentBag<TimeSpan> ArenaAction { get; set; } = new();
	public ConcurrentBag<TimeSpan> RegisterInput { get; set; } = new();
	public ConcurrentBag<TimeSpan> ConfirmConnectionZero { get; set; } = new();

	public ConcurrentBag<TimeSpan> ConfirmConnectionReal { get; set; } = new();
	public ConcurrentBag<TimeSpan> RegisterOutput { get; set; } = new();
	public ConcurrentBag<TimeSpan> SignTransaction { get; set; } = new();
	public ConcurrentBag<TimeSpan> Reissuance { get; set; } = new();
	public ConcurrentBag<TimeSpan> GetStatus { get; set; } = new();
	public ConcurrentBag<TimeSpan> ReadyToSign { get; set; } = new();

	public ConcurrentBag<TimeSpan> RegisterOutputLock { get; set; } = new();
	public ConcurrentBag<TimeSpan> RegisterInputLock { get; set; } = new();
	public ConcurrentBag<TimeSpan> ReissuanceLock { get; set; } = new();

	public string GetResults(ConcurrentBag<TimeSpan> timeSpans)
	{
		var array = timeSpans.ToArray();
		var count = array.Length;
		if (!array.Any())
		{
			array = new[] { TimeSpan.Zero };
		}

		var max = array.Max();
		var average = TimeSpan.FromMilliseconds(array.AsEnumerable().Select(t => t.TotalMilliseconds).Average());

		return $"Average: {average} Maximum: {max} Samples: {count}";
	}

	internal void AddAction(TimeSpan timeSpan)
	{
		ArenaAction.Add(timeSpan);
	}

	internal void AddRegisterInput(TimeSpan timeSpan)
	{
		RegisterInput.Add(timeSpan);
	}

	internal void AddConfirmConnectionZero(TimeSpan timeSpan)
	{
		ConfirmConnectionZero.Add(timeSpan);
	}

	internal void AddConfirmConnectionReal(TimeSpan timeSpan)
	{
		ConfirmConnectionReal.Add(timeSpan);
	}

	internal void AddRegisterOutput(TimeSpan timeSpan)
	{
		RegisterOutput.Add(timeSpan);
	}

	internal void AddSignTransaction(TimeSpan timeSpan)
	{
		SignTransaction.Add(timeSpan);
	}

	internal void AddReissuance(TimeSpan timeSpan)
	{
		Reissuance.Add(timeSpan);
	}

	internal void AddGetStatus(TimeSpan timeSpan)
	{
		GetStatus.Add(timeSpan);
	}

	internal void AddRegisterInputLock(TimeSpan timeSpan)
	{
		RegisterInputLock.Add(timeSpan);
	}

	internal void AddReissuanceLock(TimeSpan timeSpan)
	{
		ReissuanceLock.Add(timeSpan);
	}

	internal void AddRegisterOutputLock(TimeSpan timeSpan)
	{
		RegisterOutputLock.Add(timeSpan);
	}

	internal void AddReadyToSign(TimeSpan timeSpan)
	{
		ReadyToSign.Add(timeSpan);
	}
}
