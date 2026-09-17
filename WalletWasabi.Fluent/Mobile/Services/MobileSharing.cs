using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace WalletWasabi.Fluent.Mobile.Services;

/// <summary>Present a user-controlled native sharesheet; completion does not mean a message was delivered.</summary>
public interface IMobileShareService
{
	Task PresentAsync(string payload, CancellationToken cancellationToken);
}

/// <summary>Application-scoped platform services; no static activity or wallet lifetime is retained.</summary>
public static class MobileSharing
{
	private static readonly ConditionalWeakTable<Application, Registration> Registrations = new();
	public static void Register(Application application, IMobileShareService service)
	{
		ArgumentNullException.ThrowIfNull(application);
		ArgumentNullException.ThrowIfNull(service);
		Dispatcher.UIThread.VerifyAccess();
		Registrations.GetValue(application, _ => new Registration()).Service = service;
	}
	public static IMobileShareService? GetService(Application? application) =>
		application is not null && Registrations.TryGetValue(application, out var registration) ? registration : null;
	private sealed class Registration : IMobileShareService
	{
		public IMobileShareService? Service { get; set; }
		public Task PresentAsync(string payload, CancellationToken cancellationToken)
		{
			Dispatcher.UIThread.VerifyAccess();
			return (Service ?? throw new InvalidOperationException("Sharing is unavailable."))
				.PresentAsync(payload, cancellationToken);
		}
	}
}
