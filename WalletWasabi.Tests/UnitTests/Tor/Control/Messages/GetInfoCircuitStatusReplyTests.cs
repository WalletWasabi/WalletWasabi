using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.CircuitStatus;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Control.Messages;

/// <summary>
/// Tests for <see cref="GetInfoCircuitStatusReply"/> class.
/// </summary>
public class GetInfoCircuitStatusReplyTests
{
	[Fact]
	public async Task ParseReplyAsync()
	{
		StringBuilder sb = new();
		sb.Append("250+circuit-status=\r\n");
		sb.Append("1 BUILT $E9F71AC06F29B2110E3FC09016B0E50407444EE2~libertas,$D0423D3A13C18D2ED0F3D5BFD90E13E77C9AD239~d0xkb,$3A9559477D72F71215850C97FA62A0DA7380964B~PawNetBlue BUILD_FLAGS=NEED_CAPACITY PURPOSE=GENERAL TIME_CREATED=2021-05-15T14:04:17.615384\r\n");
		sb.Append("2 BUILT $E9F71AC06F29B2110E3FC09016B0E50407444EE2~libertas,$A0FA50A070CFB4B89737A27F3259F92C118A0AF0~pipiska,$7E77CC94D94C08609D70B517FF938CC61C9F8232~pitfall BUILD_FLAGS=NEED_CAPACITY PURPOSE=GENERAL TIME_CREATED=2021-05-15T14:04:18.628885\r\n");
		sb.Append("3 BUILT $E9F71AC06F29B2110E3FC09016B0E50407444EE2~libertas,$706A7674A217BA905FE677E82236B7B968A23DB7~rofltor04,$4D4938B725B89561773A161215D88B7C45C43C35~TheGreenDynamo,$18CA339AD0C33EAB035F1D869518F3D2D88BABC0~FreeAssange BUILD_FLAGS=IS_INTERNAL,NEED_CAPACITY PURPOSE=HS_CLIENT_HSDIR HS_STATE=HSCI_CONNECTING TIME_CREATED=2021-05-15T14:04:19.353271\r\n");
		sb.Append("4 EXTENDED $E9F71AC06F29B2110E3FC09016B0E50407444EE2~libertas BUILD_FLAGS=IS_INTERNAL,NEED_CAPACITY PURPOSE=MEASURE_TIMEOUT TIME_CREATED=2021-05-15T14:04:19.631228\r\n");
		sb.Append("5 BUILT $E9F71AC06F29B2110E3FC09016B0E50407444EE2~libertas,$31D270A38505D4BFBBCABF717E9FB4BCA6DDF2FF~Belgium,$B411027C926A9BFFCF7DA91E3CAF1856A321EFFD~pulsetor BUILD_FLAGS=IS_INTERNAL,NEED_CAPACITY PURPOSE=HS_CLIENT_REND HS_STATE=HSCR_JOINED REND_QUERY=wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad TIME_CREATED=2021-05-15T14:04:20.634686\r\n");
		sb.Append(".\r\n");
		sb.Append("250 OK\r\n");

		string data = sb.ToString();

		TorControlReply rawReply = await TorControlReplyReaderTest.ParseAsync(data);
		Assert.Equal(8, rawReply.ResponseLines.Count);

		GetInfoCircuitStatusReply reply = GetInfoCircuitStatusReply.FromReply(rawReply);

		Assert.Equal(5, reply.Circuits.Count);

		// Circuit #1.
		{
			CircuitInfo circuitInfo = reply.Circuits[0];

			Assert.Equal("1", circuitInfo.CircuitID);
			Assert.Equal(CircuitStatus.BUILT, circuitInfo.CircuitStatus);

			List<CircuitPath> circuitPaths = circuitInfo.CircuitPaths;
			Assert.Equal("$E9F71AC06F29B2110E3FC09016B0E50407444EE2", circuitPaths[0].FingerPrint);
			Assert.Equal("libertas", circuitPaths[0].Nickname);

			Assert.Equal("$D0423D3A13C18D2ED0F3D5BFD90E13E77C9AD239", circuitPaths[1].FingerPrint);
			Assert.Equal("d0xkb", circuitPaths[1].Nickname);

			Assert.Equal("$3A9559477D72F71215850C97FA62A0DA7380964B", circuitPaths[2].FingerPrint);
			Assert.Equal("PawNetBlue", circuitPaths[2].Nickname);

			BuildFlag buildFlag = Assert.Single(circuitInfo.BuildFlags);
			Assert.Equal(BuildFlag.NEED_CAPACITY, buildFlag);

			Assert.Equal(Purpose.GENERAL, circuitInfo.Purpose);
			Assert.Equal("2021-05-15T14:04:17.615384", circuitInfo.TimeCreated);

			Assert.Null(circuitInfo.Reason);
			Assert.Null(circuitInfo.RemoteReason);
			Assert.Null(circuitInfo.HsState);
			Assert.Null(circuitInfo.RendQuery);
			Assert.Null(circuitInfo.UserName);
			Assert.Null(circuitInfo.UserPassword);
		}

		// Circuit #2.
		{
			CircuitInfo circuitInfo = reply.Circuits[1];

			Assert.Equal("2", circuitInfo.CircuitID);
			Assert.Equal(CircuitStatus.BUILT, circuitInfo.CircuitStatus);

			List<CircuitPath> circuitPaths = circuitInfo.CircuitPaths;
			Assert.Equal("$E9F71AC06F29B2110E3FC09016B0E50407444EE2", circuitPaths[0].FingerPrint);
			Assert.Equal("libertas", circuitPaths[0].Nickname);

			Assert.Equal("$A0FA50A070CFB4B89737A27F3259F92C118A0AF0", circuitPaths[1].FingerPrint);
			Assert.Equal("pipiska", circuitPaths[1].Nickname);

			Assert.Equal("$7E77CC94D94C08609D70B517FF938CC61C9F8232", circuitPaths[2].FingerPrint);
			Assert.Equal("pitfall", circuitPaths[2].Nickname);

			BuildFlag buildFlag = Assert.Single(circuitInfo.BuildFlags);
			Assert.Equal(BuildFlag.NEED_CAPACITY, buildFlag);

			Assert.Equal(Purpose.GENERAL, circuitInfo.Purpose);
			Assert.Equal("2021-05-15T14:04:18.628885", circuitInfo.TimeCreated);

			Assert.Null(circuitInfo.Reason);
			Assert.Null(circuitInfo.RemoteReason);
			Assert.Null(circuitInfo.HsState);
			Assert.Null(circuitInfo.RendQuery);
			Assert.Null(circuitInfo.UserName);
			Assert.Null(circuitInfo.UserPassword);
		}

		// Circuit #3.
		{
			CircuitInfo circuitInfo = reply.Circuits[2];

			Assert.Equal("3", circuitInfo.CircuitID);
			Assert.Equal(CircuitStatus.BUILT, circuitInfo.CircuitStatus);

			List<CircuitPath> circuitPaths = circuitInfo.CircuitPaths;
			Assert.Equal("$E9F71AC06F29B2110E3FC09016B0E50407444EE2", circuitPaths[0].FingerPrint);
			Assert.Equal("libertas", circuitPaths[0].Nickname);

			Assert.Equal("$706A7674A217BA905FE677E82236B7B968A23DB7", circuitPaths[1].FingerPrint);
			Assert.Equal("rofltor04", circuitPaths[1].Nickname);

			Assert.Equal("$4D4938B725B89561773A161215D88B7C45C43C35", circuitPaths[2].FingerPrint);
			Assert.Equal("TheGreenDynamo", circuitPaths[2].Nickname);

			Assert.Equal("$18CA339AD0C33EAB035F1D869518F3D2D88BABC0", circuitPaths[3].FingerPrint);
			Assert.Equal("FreeAssange", circuitPaths[3].Nickname);

			Assert.Equal(2, circuitInfo.BuildFlags.Count);
			Assert.Equal(BuildFlag.IS_INTERNAL, circuitInfo.BuildFlags[0]);
			Assert.Equal(BuildFlag.NEED_CAPACITY, circuitInfo.BuildFlags[1]);

			Assert.Equal(Purpose.HS_CLIENT_HSDIR, circuitInfo.Purpose);
			Assert.Equal(HsState.HSCI_CONNECTING, circuitInfo.HsState);
			Assert.Equal("2021-05-15T14:04:19.353271", circuitInfo.TimeCreated);

			Assert.Null(circuitInfo.Reason);
			Assert.Null(circuitInfo.RemoteReason);
			Assert.Null(circuitInfo.RendQuery);
			Assert.Null(circuitInfo.UserName);
			Assert.Null(circuitInfo.UserPassword);
		}

		// Circuit #4.
		{
			CircuitInfo circuitInfo = reply.Circuits[3];

			Assert.Equal("4", circuitInfo.CircuitID);
			Assert.Equal(CircuitStatus.EXTENDED, circuitInfo.CircuitStatus);

			List<CircuitPath> circuitPaths = circuitInfo.CircuitPaths;
			Assert.Equal("$E9F71AC06F29B2110E3FC09016B0E50407444EE2", circuitPaths[0].FingerPrint);
			Assert.Equal("libertas", circuitPaths[0].Nickname);

			Assert.Equal(2, circuitInfo.BuildFlags.Count);
			Assert.Equal(BuildFlag.IS_INTERNAL, circuitInfo.BuildFlags[0]);
			Assert.Equal(BuildFlag.NEED_CAPACITY, circuitInfo.BuildFlags[1]);
			Assert.Equal(Purpose.MEASURE_TIMEOUT, circuitInfo.Purpose);
			Assert.Equal("2021-05-15T14:04:19.631228", circuitInfo.TimeCreated);

			Assert.Null(circuitInfo.HsState);
			Assert.Null(circuitInfo.Reason);
			Assert.Null(circuitInfo.RemoteReason);
			Assert.Null(circuitInfo.RendQuery);
			Assert.Null(circuitInfo.UserName);
			Assert.Null(circuitInfo.UserPassword);
		}

		// Circuit #5.
		{
			CircuitInfo circuitInfo = reply.Circuits[4];

			Assert.Equal("5", circuitInfo.CircuitID);
			Assert.Equal(CircuitStatus.BUILT, circuitInfo.CircuitStatus);

			List<CircuitPath> circuitPaths = circuitInfo.CircuitPaths;
			Assert.Equal("$E9F71AC06F29B2110E3FC09016B0E50407444EE2", circuitPaths[0].FingerPrint);
			Assert.Equal("libertas", circuitPaths[0].Nickname);

			Assert.Equal("$31D270A38505D4BFBBCABF717E9FB4BCA6DDF2FF", circuitPaths[1].FingerPrint);
			Assert.Equal("Belgium", circuitPaths[1].Nickname);

			Assert.Equal("$B411027C926A9BFFCF7DA91E3CAF1856A321EFFD", circuitPaths[2].FingerPrint);
			Assert.Equal("pulsetor", circuitPaths[2].Nickname);

			Assert.Equal(2, circuitInfo.BuildFlags.Count);
			Assert.Equal(BuildFlag.IS_INTERNAL, circuitInfo.BuildFlags[0]);
			Assert.Equal(BuildFlag.NEED_CAPACITY, circuitInfo.BuildFlags[1]);
			Assert.Equal(Purpose.HS_CLIENT_REND, circuitInfo.Purpose);
			Assert.Equal(HsState.HSCR_JOINED, circuitInfo.HsState);
			Assert.Equal("wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad", circuitInfo.RendQuery);
			Assert.Equal("2021-05-15T14:04:20.634686", circuitInfo.TimeCreated);

			Assert.Null(circuitInfo.Reason);
			Assert.Null(circuitInfo.RemoteReason);
			Assert.Null(circuitInfo.UserName);
			Assert.Null(circuitInfo.UserPassword);
		}
	}
}
