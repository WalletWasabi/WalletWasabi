using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Services
{
	public class LegalChecker : IDisposable
	{
		private bool _disposedValue;

		public LegalChecker(UpdateChecker updateChecker)
		{
			UpdateChecker = updateChecker;
			UpdateChecker.UpdateStatusChanged += UpdateChecker_UpdateStatusChanged;
		}

		private UpdateChecker UpdateChecker { get; }

		private void UpdateChecker_UpdateStatusChanged(object? sender, Models.UpdateStatus e)
		{
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					UpdateChecker.UpdateStatusChanged -= UpdateChecker_UpdateStatusChanged;
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
