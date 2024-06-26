namespace WalletWasabi.Fluent.Helpers;

using System;
using System.Collections.Generic;

	public class LambdaComparer<T> : IEqualityComparer<T>
	{
		private readonly Func<T?, T?, bool> _lambdaComparer;
		private readonly Func<T?, int> _lambdaHash;

		public LambdaComparer(Func<T?, T?, bool> lambdaComparer) :
			this(lambdaComparer, _ => 0)
		{
		}

		public LambdaComparer(Func<T?, T?, bool> lambdaComparer, Func<T?, int> lambdaHash)
		{
			_lambdaComparer = lambdaComparer ?? throw new ArgumentNullException("lambdaComparer");
			_lambdaHash = lambdaHash ?? throw new ArgumentNullException("lambdaHash");
		}

		public bool Equals(T? x, T? y)
		{
			return _lambdaComparer(x, y);
		}

		public int GetHashCode(T obj)
		{
			return _lambdaHash(obj);
		}
	}
