using System.Security.Cryptography;

namespace WalletWasabi.Crypto.Randomness;

public class SecureRandom : WasabiRandom
{
	private bool _disposedValue;

	public SecureRandom()
	{
		Random = RandomNumberGenerator.Create();
	}

	private RandomNumberGenerator Random { get; }

	public override void GetBytes(byte[] buffer) => Random.GetBytes(buffer);

	public override void GetBytes(Span<byte> buffer) => Random.GetBytes(buffer);

	public override int GetInt(int fromInclusive, int toExclusive) => RandomNumberGenerator.GetInt32(fromInclusive, toExclusive);

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

	public override void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
