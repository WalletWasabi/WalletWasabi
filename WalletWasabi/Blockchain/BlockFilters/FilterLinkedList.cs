using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Blockchain.BlockFilters;

public class FilterLinkedList : IEnumerable<FilterModel>
{
	record Node(FilterModel Filter, Node? Next)
	{
		public int Count { get; } = (Next?.Count ?? 0) + 1;
	}

	private Node? _head;
	private readonly object _syncObject = new();

	public void Add(FilterModel filter)
	{
		lock (_syncObject)
		{
			_head = new Node(filter, _head);
		}
	}

	public void RemoveLast()
	{
		lock (_syncObject)
		{
			if (_head is not { } nonNullHead)
			{
				throw new InvalidOperationException("The linked list is empty.");
			}

			_head = nonNullHead.Next;
		}
	}

	public int Count
	{
		get
		{
			lock (_syncObject)
			{
				return _head switch
				{
					null => 0,
					{ } nonNullHead => nonNullHead.Count
				};
			}
		}
	}

	public IEnumerator<FilterModel> GetEnumerator()
	{
		IEnumerable<FilterModel> GetEnumerable()
		{
			Node? p;
			lock (_syncObject)
			{
				p = _head;
			}

			while (p != null)
			{
				yield return p.Filter;
				p = p.Next;
			}
		}

		return GetEnumerable().Reverse().GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public FilterModel Last()
	{
		lock (_syncObject)
		{
			return _head?.Filter
			       ?? throw new ArgumentOutOfRangeException();
		}
	}
}
