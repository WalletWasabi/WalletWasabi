using Avalonia;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace WalletWasabi.Tests
{
	public class AvaloniaTests
	{
		[Fact]
		public async Task ClipboardTestsAsync()
		{
			var texts = new[] {
				"    w¾3AÍ-dCdï×¾M\\Øò¹ãÔÕýÈÝÁÐ9oEp¨}r:SR¦·ßNó±¥*W!¢ê#ikÇå<ðtÇf·a\\]§,à±H7«®È4nèNmæo4.qØ-¾ûda¯ºíö¾,¥¢½\\¹õèKeÁìÍSÈ@r±ØÙ2[r©UQÞ¶xN\"?:Ö@°&\n",
				"M%LakuO\\2+u*HQsRPM9,1+]0+vLn0`E2G)kekBfgSdjLAV2?KI^mamvda901M#\"zO0Ue.+)NVv\"cJx|4N6C`YZGrZn4wfrnY+ODp+i{mnGw+w~9|U+uhpVi/QjSGy.re",
				"",
				")&vtyHopB.w8w01<66lI,(Y@A-KJK%:cFsR-Z:;y(k}]6aDJ1O2Da%1,(#sB.KXj:FM*nVCBBV}.55;_u&ZRJW[6*Nf2N[H_w\"\"Si}dG.>QhPR~\"bE`jbE\"uDZt*jT1Lr(+M\\:&K}kv\":gX - e * u,#m$1:9'9|(V5R3Sn~i;f=(z?U(:a694z6rtjl`hVF73Ysle-v2|fN',Qd>ZXj]wxaO3g9]bLGbUEP5dm/,ZQ!Q]5g8JAn^9YO5=tTj~rIxm^",
				"%WnLm<jW#sp(j&<tkKg`kA2R",
				"yx9^4`54'}})QW6$^2P,PAO76n'97:N+iK42uhmA0#6:tnyh(A`m+7xh|Nm(3UEOa|`27x1Lm'LdHlj54x\"948095, 8J2ZqT68J3lhI77b26Hw!D77: m3 & MG346G4\"8x",
				"                  "
			};

			await AvaloniaTest.RunTestAsync(async () =>
				{
					var clipboard = Application.Current.Clipboard;

					foreach (var actual in texts)
					{
						await clipboard.SetTextAsync(actual);
						var result = await clipboard.GetTextAsync();
						Assert.Equal(actual, result);

						await clipboard.ClearAsync();
						result = await clipboard.GetTextAsync();
						Assert.Null(result);
					}
				});
		}
	}

	public class AvaloniaTest
	{
		/// <summary>
		/// Runs a test synchronously. Warning! This is not compatible with code that uses async/await.
		/// </summary>
		/// <param name="testCode">Test code to run.</param>
		public static void RunTestSynchronously(Action testCode)
		{
			BuildAvaloniaApp().Start((app, args) =>
			{
				testCode();
			}, null);
		}

		/// <summary>
		/// Runs a test asynchronously. Required if the test uses async/await.
		/// </summary>
		/// <param name="testCode"></param>
		/// <returns></returns>
		public static Task RunTestAsync(Func<Task> testCode)
		{
			var tcs = new TaskCompletionSource<bool>();

			BuildAvaloniaApp().Start(async (app, args) =>
			{
				try
				{
					if (Dispatcher.UIThread.CheckAccess())
					{
						await testCode();
					}
					else
					{
						await Dispatcher.UIThread.InvokeAsync(async () => await testCode());
					}
				}
				catch (Exception e)
				{
					tcs.SetException(e);

					return;
				}

				tcs.SetResult(true);
			}, null);

			return tcs.Task;
		}

		private static AppBuilder BuildAvaloniaApp()
		{
			return AppBuilder.Configure<App>().UsePlatformDetect();
		}
	}

	internal class App : Application
	{
	}
}
