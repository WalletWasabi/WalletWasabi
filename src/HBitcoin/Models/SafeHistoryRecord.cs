using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NBitcoin;

namespace HBitcoin.Models
{
	public class SafeHistoryRecord
    {
		// http://stackoverflow.com/questions/35582162/how-to-implement-inotifypropertychanged-in-c-sharp-6-0
		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}

		private DateTimeOffset _timeStamp;
		public DateTimeOffset TimeStamp
		{
			get { return _timeStamp; }
			set { SetField(ref _timeStamp, value); }
		}
		private Money _amount;
		public Money Amount
		{
			get { return _amount; }
			set { SetField(ref _amount, value); }
		}
		private Height _blockHeight;
		public Height BlockHeight
		{
			get { return _blockHeight; }
			set { SetField(ref _blockHeight, value); }
		}

		public bool Confirmed => BlockHeight.Type == HeightType.Chain;

		private uint256 _transactionId;
		public uint256 TransactionId
		{
			get { return _transactionId; }
			set { SetField(ref _transactionId, value); }
		}
	}
}
