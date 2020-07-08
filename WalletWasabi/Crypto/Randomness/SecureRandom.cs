using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WalletWasabi.Crypto.Randomness
{
	public class SecureRandom : IWasabiRandom, IDisposable
	{
		private bool _disposedValue;

		public SecureRandom()
		{
			Random = RandomNumberGenerator.Create();
		}

		private RandomNumberGenerator Random { get; }

		public void GetBytes(byte[] buffer) => Random.GetBytes(buffer);

		public void GetBytes(Span<byte> buffer) => Random.GetBytes(buffer);

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Random?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
