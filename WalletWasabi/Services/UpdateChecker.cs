using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class UpdateChecker
	{
		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		private CancellationTokenSource Stop { get; set; }

		public WasabiClient WasabiClient { get; }

		private string WorkFolderPath { get; set; }
		public byte[] LegalIssuesHash { get; private set; }
		public byte[] PrivacyPolicyHash { get; private set; }
		public byte[] TermsAndConditionsHash { get; private set; }

		public UpdateChecker(WasabiClient client)
		{
			WasabiClient = client;
			_running = 0;
			Stop = new CancellationTokenSource();
		}

		public async Task InitializeAsync(string workFolderPath)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			IoHelpers.EnsureDirectoryExists(WorkFolderPath);

			var legalIssuesPath = Path.Combine(WorkFolderPath, "LegalIssues.txt");
			var privacyPolicyPath = Path.Combine(WorkFolderPath, "PrivacyPolicy.txt");
			var termsAndConditionsPath = Path.Combine(WorkFolderPath, "TermsAndConditions.txt");

			LegalIssuesHash = File.Exists(legalIssuesPath) ? HashHelpers.GenerateSha256Hash(await File.ReadAllBytesAsync(legalIssuesPath)) : null;
			PrivacyPolicyHash = File.Exists(privacyPolicyPath) ? HashHelpers.GenerateSha256Hash(await File.ReadAllBytesAsync(privacyPolicyPath)) : null;
			TermsAndConditionsHash = File.Exists(termsAndConditionsPath) ? HashHelpers.GenerateSha256Hash(await File.ReadAllBytesAsync(termsAndConditionsPath)) : null;
		}

		public void Start(TimeSpan period, Func<Task> executeIfBackendIncompatible, Func<Task> executeIfClientOutOfDate, Func<Task> legalDocumentsOutOfDate)
		{
			if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
			{
				return;
			}

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							var versions = await WasabiClient.GetVersionsAsync(Stop.Token);

							if (int.Parse(Constants.BackendMajorVersion) != versions.BackendMajorVersion) // If the backend major and the client major are equal, then our softwares are compatible.
							{
								await executeIfBackendIncompatible?.Invoke();
							}

							if (Constants.ClientVersion < versions.ClientVersion) // If the client version locally is smaller than the backend's reported client version, then need to update.
							{
								await executeIfClientOutOfDate?.Invoke();
							}

							var legalUpToDate = ByteHelpers.CompareFastUnsafe(versions.LegalIssuesHash, LegalIssuesHash) && ByteHelpers.CompareFastUnsafe(versions.PrivacyPolicyHash, PrivacyPolicyHash) && ByteHelpers.CompareFastUnsafe(versions.TermsAndConditionsHash, TermsAndConditionsHash);

							if (!legalUpToDate)
							{
								await legalDocumentsOutOfDate?.Invoke();
							}
						}
						catch (ConnectionException ex)
						{
							Logger.LogError<UpdateChecker>(ex);
							try
							{
								await Task.Delay(period, Stop.Token); // Give other threads time to do stuff, update check is not crucial.
							}
							catch (TaskCanceledException ex2)
							{
								Logger.LogTrace<UpdateChecker>(ex2);
							}
						}
						catch (Exception ex) when (ex is OperationCanceledException
												|| ex is TaskCanceledException
												|| ex is TimeoutException)
						{
							Logger.LogTrace<UpdateChecker>(ex);
						}
						catch (Exception ex)
						{
							Logger.LogDebug<UpdateChecker>(ex);
						}
						finally
						{
							await Task.Delay(period, Stop.Token);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
			Stop?.Cancel();
			while (Interlocked.CompareExchange(ref _running, 3, 0) == 2)
			{
				await Task.Delay(50);
			}
			Stop?.Dispose();
			Stop = null;
		}
	}
}
