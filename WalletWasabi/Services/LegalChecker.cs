using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Legal;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
	public class LegalChecker : IDisposable
	{
		private bool _disposedValue;

		public LegalChecker(UpdateChecker updateChecker, string dataDir)
		{
			UpdateChecker = updateChecker;
			DataDir = dataDir;
			UpdateChecker.UpdateStatusChanged += UpdateChecker_UpdateStatusChangedAsync;
		}

		private AsyncLock LegalDocumentLock { get; } = new();
		private UpdateChecker UpdateChecker { get; }
		private string DataDir { get; }
		private LegalDocuments? CurrentLegalDocument { get; set; }
		private LegalDocuments? NewLegalDocument { get; set; }
		private string? NewLegalContent { get; set; }

		public bool IsAgreementRequired => NewLegalDocument is { };

		private async void UpdateChecker_UpdateStatusChangedAsync(object? _, UpdateStatus updateStatus)
		{
			try
			{
				using (await LegalDocumentLock.LockAsync())
				{
					// If we don't have it or there is a new one.
					if (CurrentLegalDocument is null || CurrentLegalDocument.Version < updateStatus.LegalDocumentsVersion)
					{
						var legalFolderPath = Path.Combine(DataDir, LegalDocuments.LegalFolderName);
						var filePath = Path.Combine(legalFolderPath, $"{updateStatus.LegalDocumentsVersion}.txt");

						// Store the content, in case of agreement it will be saved to file.
						NewLegalContent = await UpdateChecker.WasabiClient.GetLegalDocumentsAsync(CancellationToken.None).ConfigureAwait(false);

						NewLegalDocument = new LegalDocuments(filePath);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Could not get legal documents.", ex);
			}
		}

		public async Task AgreeAsync()
		{
			using (await LegalDocumentLock.LockAsync())
			{
				if (NewLegalDocument is not { } newLegalDocument || string.IsNullOrEmpty(NewLegalContent))
				{
					throw new InvalidOperationException("Cannot agree the new legal document.");
				}

				await newLegalDocument.ToFileAsync(NewLegalContent).ConfigureAwait(false);

				CurrentLegalDocument = NewLegalDocument;
				NewLegalDocument = null;
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					UpdateChecker.UpdateStatusChanged -= UpdateChecker_UpdateStatusChangedAsync;
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
