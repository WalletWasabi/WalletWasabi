using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class EventHandlerExtensionsTests
	{
		public event EventHandler<EventArgs> handler;

		[Fact]
		public void InvokeAllSubscribers()
		{
			var count = 0;
			handler += (s, e) => count++;
			handler += (s, e) => count++;
			handler += (s, e) => count++;

			handler.SafeInvoke(this, null);
			Assert.Equal(3, count);
		}

		[Fact]
		public void InvokeAllSubscribersEvenIfExceptions()
		{
			var count = 0;
			handler += (s, e) => count++;
			handler += (s, e) => throw new Exception("Something really bad happened here!");
			handler += (s, e) => count++;

			// In a normal Invoke, if there is an exception in one of the handlers the next handlers are not invoked.
			try
			{
				handler.Invoke(this, null);
				throw new Exception("An exception was expected but never happened.");
			}
			catch(Exception)
			{
				Assert.Equal(1, count);
			}

			count = 0;
			// The SafeInvoke makes sure that even if one handler fails, the others are invoked anyway.
			handler.SafeInvoke(this, null);
			Assert.Equal(2, count);
		}
	}
}