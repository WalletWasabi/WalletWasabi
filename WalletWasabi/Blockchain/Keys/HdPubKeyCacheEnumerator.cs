using System.Collections;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyCacheEnumerator : IEnumerator<HdPubKeyInfo>
{
	private readonly List<HdPubKeyInfo> _list;
	private readonly int _count;
	private int _currentIndex;
	private HdPubKeyInfo? _current;

	public HdPubKeyCacheEnumerator(List<HdPubKeyInfo> list)
	{
		_list = list;
		_count = list.Count;
		_currentIndex = 0;
		_current = default;
	}

	public HdPubKeyInfo Current => _current;

	object? IEnumerator.Current
	{
		get
		{
			if (_currentIndex == 0 || _currentIndex == _count)
			{
				throw new InvalidOperationException(
					"This should never happen. You have to call MoveNext before getting Current.");
			}
			return Current;
		}
	}

	public bool MoveNext()
	{
		if (_currentIndex >= _count)
		{
			_current = default;
			_currentIndex = _count;
			return false;
		}

		_current = _list[_currentIndex];
		_currentIndex++;
		return true;
	}

	public void Reset()
	{
		_currentIndex = 0;
		_current = default;
	}

	public void Dispose()
	{
	}
}
