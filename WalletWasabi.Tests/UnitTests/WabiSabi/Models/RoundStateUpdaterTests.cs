using Moq;
using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models
{
	public class RoundStateUpdaterTests
	{
		[Fact]
		public async Task RoundStateUpdaterTestsAsync()
		{
			var roundState1 = RoundState.FromRound(WabiSabiFactory.CreateRound(new()));
			var roundState2 = RoundState.FromRound(WabiSabiFactory.CreateRound(new()));

			using CancellationTokenSource cancellationTokenSource = new();
			var cancellationToken = cancellationTokenSource.Token;

			var mockApiClient = new Mock<IWabiSabiApiRequestHandler>();
			mockApiClient.SetupSequence(apiClient => apiClient.GetStatusAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(() => new[] { roundState1 with { Phase = Phase.InputRegistration } })
				.ReturnsAsync(() => new[] { roundState1 with { Phase = Phase.OutputRegistration } })
				.ReturnsAsync(() => new[] { roundState1 with { Phase = Phase.OutputRegistration }, roundState2 with { Phase = Phase.InputRegistration } })
				.ReturnsAsync(() => new[] { roundState2 with { Phase = Phase.OutputRegistration } })
				.ReturnsAsync(() => Array.Empty<RoundState>());

			using RoundStateUpdater roundStatusUpdater = new(TimeSpan.FromDays(1), mockApiClient.Object);

			using var round1TSCts = new CancellationTokenSource();
			var round1IRTask = roundStatusUpdater.CreateRoundAwaiter(roundState1.Id, rs => rs.Phase == Phase.InputRegistration, cancellationToken);
			var round1ORTask = roundStatusUpdater.CreateRoundAwaiter(roundState1.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken);
			var round1TSTask = roundStatusUpdater.CreateRoundAwaiter(roundState1.Id, rs => rs.Phase == Phase.TransactionSigning, round1TSCts.Token);
			var round1TBTask = roundStatusUpdater.CreateRoundAwaiter(roundState1.Id, rs => rs.Phase == Phase.TransactionBroadcasting, cancellationToken);
			var round2IRTask = roundStatusUpdater.CreateRoundAwaiter(roundState2.Id, rs => rs.Phase == Phase.InputRegistration, cancellationToken);

			await roundStatusUpdater.StartAsync(cancellationTokenSource.Token);

			var round1 = await round1IRTask;
			Assert.Equal(roundState1.Id, round1.Id);
			Assert.Equal(Phase.InputRegistration, round1.Phase);
			Assert.All(new[] { round1ORTask, round1TSTask, round1TBTask, round2IRTask }, t => Assert.Equal(TaskStatus.WaitingForActivation, t.Status));

			await roundStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromMilliseconds(10));
			await roundStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromMilliseconds(10));
			var round2 = await round2IRTask;
			Assert.Equal(roundState2.Id, round2.Id);
			Assert.Equal(Phase.InputRegistration, round2.Phase);
			Assert.All(new[] { round1TSTask, round1TBTask }, t => Assert.Equal(TaskStatus.WaitingForActivation, t.Status));

			round1 = await round1ORTask;
			Assert.Equal(roundState1.Id, round1.Id);
			Assert.Equal(Phase.OutputRegistration, round1.Phase);
			Assert.All(new[] { round1TSTask, round1TBTask }, t => Assert.Equal(TaskStatus.WaitingForActivation, t.Status));

			round1TSCts.Cancel();
			Assert.True(round1TSTask.IsCanceled);

			await roundStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromMilliseconds(10));
			var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await round1TBTask);
			Assert.Contains(round1.Id.ToString(), ex.Message);
			Assert.Contains("not running", ex.Message);

			await roundStatusUpdater.StopAsync(cancellationToken);
		}
	}
}
