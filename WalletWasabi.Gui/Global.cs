using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Gui
{
	public static class Global
	{
		public static string DataDir { get; }
		public static string TorLogsFile { get; }
		public static string WalletsDir { get; }
		public static string WalletBackupsDir { get; }

		public static string IndexFilePath { get; private set; }
		public static Config Config { get; private set; }

		public static string AddressManagerFilePath { get; private set; }
		public static AddressManager AddressManager { get; private set; }
		public static MemPoolService MemPoolService { get; private set; }

		public static NodesGroup Nodes { get; private set; }
		public static WasabiSynchronizer Synchronizer { get; private set; }
		public static CcjClient ChaumianClient { get; private set; }
		public static WalletService WalletService { get; private set; }
		public static Node RegTestMemPoolServingNode { get; private set; }
		public static UpdateChecker UpdateChecker { get; private set; }
		public static TorProcessManager TorManager { get; private set; }

		public static bool KillRequested { get; private set; } = false;

		public static UiConfig UiConfig { get; private set; }

		public static Network Network => Config.Network;

		static Global()
		{
			DataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
			TorLogsFile = Path.Combine(DataDir, "TorLogs.txt");
			WalletsDir = Path.Combine(DataDir, "Wallets");
			WalletBackupsDir = Path.Combine(DataDir, "WalletBackups");
		}

		public static void InitializeUiConfig(UiConfig uiConfig)
		{
			UiConfig = Guard.NotNull(nameof(uiConfig), uiConfig);
		}

		private static int IsDesperateDequeuing = 0;

		public static async Task TryDesperateDequeueAllCoinsAsync()
		{
			// If already desperate dequeueing then return.
			// If not desperate dequeueing then make sure we're doing that.
			if (Interlocked.CompareExchange(ref IsDesperateDequeuing, 1, 0) == 1)
			{
				return;
			}
			try
			{
				await DesperateDequeueAllCoinsAsync();
			}
			catch (NotSupportedException ex)
			{
				Logger.LogWarning(ex.Message, nameof(Global));
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
			finally
			{
				Interlocked.Exchange(ref IsDesperateDequeuing, 0);
			}
		}

		public static async Task DesperateDequeueAllCoinsAsync()
		{
			if (WalletService is null || ChaumianClient is null)
			{
				return;
			}

			SmartCoin[] enqueuedCoins = WalletService.Coins.Where(x => x.CoinJoinInProgress).ToArray();
			if (enqueuedCoins.Any())
			{
				Logger.LogWarning("Unregistering coins in CoinJoin process.", nameof(Global));
				await ChaumianClient.DequeueCoinsFromMixAsync(enqueuedCoins);
			}
		}

		public static async Task InitializeNoWalletAsync()
		{
			WalletService = null;
			ChaumianClient = null;
			AddressManager = null;
			TorManager = null;

			Config = new Config(Path.Combine(DataDir, "Config.json"));
			await Config.LoadOrCreateDefaultFileAsync();
			Logger.LogInfo<Config>("Config is successfully initialized.");

			IndexFilePath = Path.Combine(DataDir, $"Index{Network}.dat");

			AppDomain.CurrentDomain.ProcessExit += async (s, e) => await TryDesperateDequeueAllCoinsAsync();
			Console.CancelKeyPress += async (s, e) =>
			{
				e.Cancel = true;
				Logger.LogWarning("Process was signaled for killing.", nameof(Global));

				KillRequested = true;
				await TryDesperateDequeueAllCoinsAsync();
				Dispatcher.UIThread.PostLogException(() =>
				{
					Application.Current?.MainWindow?.Close();
				});
			};

			var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");
			AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
			var blocksFolderPath = Path.Combine(DataDir, $"Blocks{Network}");
			var connectionParameters = new NodeConnectionParameters();

			if (Config.UseTor.Value)
			{
				TorManager = new TorProcessManager(Config.GetTorSocks5EndPoint(), TorLogsFile);
			}
			else
			{
				TorManager = TorProcessManager.Mock();
			}
			TorManager.Start(false, DataDir);

			try
			{
				await HwiProcessManager.EnsureHwiInstalledAsync(DataDir, Network);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, nameof(Global));
			}

			var fallbackRequestTestUri = new Uri(Config.GetFallbackBackendUri(), "/api/software/versions");
			TorManager.StartMonitor(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7), DataDir, fallbackRequestTestUri);

			Logger.LogInfo<TorProcessManager>($"{nameof(TorProcessManager)} is initialized.");

			var needsToDiscoverPeers = true;
			if (Network == Network.RegTest)
			{
				AddressManager = new AddressManager();
				Logger.LogInfo<AddressManager>($"Fake {nameof(AddressManager)} is initialized on the RegTest.");
			}
			else
			{
				try
				{
					AddressManager = AddressManager.LoadPeerFile(AddressManagerFilePath);

					// The most of the times we don't need to discover new peers. Instead, we can connect to
					// some of those that we already discovered in the past. In this case we assume that we
					// assume that discovering new peers could be necessary if out address manager has less
					// than 500 addresses. A 500 addresses could be okay because previously we tried with
					// 200 and only one user reported he/she was not able to connect (there could be many others,
					// of course).
					// On the other side, increasing this number forces users that do not need to discover more peers
					// to spend resources (CPU/bandwith) to discover new peers.
					needsToDiscoverPeers = Config.UseTor == true || AddressManager.Count < 500;
					Logger.LogInfo<AddressManager>($"Loaded {nameof(AddressManager)} from `{AddressManagerFilePath}`.");
				}
				catch (DirectoryNotFoundException ex)
				{
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} did not exist at `{AddressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace<AddressManager>(ex);
					AddressManager = new AddressManager();
				}
				catch (FileNotFoundException ex)
				{
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} did not exist at `{AddressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace<AddressManager>(ex);
					AddressManager = new AddressManager();
				}
				catch (OverflowException ex)
				{
					// https://github.com/zkSNACKs/WalletWasabi/issues/712
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} has thrown `{nameof(OverflowException)}`. Attempting to autocorrect.");
					File.Delete(AddressManagerFilePath);
					Logger.LogTrace<AddressManager>(ex);
					AddressManager = new AddressManager();
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} autocorrection is successful.");
				}
				catch (FormatException ex)
				{
					// https://github.com/zkSNACKs/WalletWasabi/issues/880
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} has thrown `{nameof(FormatException)}`. Attempting to autocorrect.");
					File.Delete(AddressManagerFilePath);
					Logger.LogTrace<AddressManager>(ex);
					AddressManager = new AddressManager();
					Logger.LogInfo<AddressManager>($"{nameof(AddressManager)} autocorrection is successful.");
				}
			}

			var addressManagerBehavior = new AddressManagerBehavior(AddressManager)
			{
				Mode = needsToDiscoverPeers ? AddressManagerBehaviorMode.Discover : AddressManagerBehaviorMode.None
			};
			connectionParameters.TemplateBehaviors.Add(addressManagerBehavior);
			MemPoolService = new MemPoolService();
			connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(MemPoolService));

			if (Network == Network.RegTest)
			{
				Nodes = new NodesGroup(Network, requirements: Constants.NodeRequirements);
				try
				{
					Node node = await Node.ConnectAsync(Network.RegTest, new IPEndPoint(IPAddress.Loopback, 18444));
					Nodes.ConnectedNodes.Add(node);

					RegTestMemPoolServingNode = await Node.ConnectAsync(Network.RegTest, new IPEndPoint(IPAddress.Loopback, 18444));

					RegTestMemPoolServingNode.Behaviors.Add(new MemPoolBehavior(MemPoolService));
				}
				catch (SocketException ex)
				{
					Logger.LogError(ex, nameof(Global));
				}
			}
			else
			{
				if (Config.UseTor is true)
				{
					// onlyForOnionHosts: false - Connect to clearnet IPs through Tor, too.
					connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(Config.GetTorSocks5EndPoint(), onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
					// allowOnlyTorEndpoints: true - Connect only to onions and don't connect to clearnet IPs at all.
					// This of course makes the first setting unneccessary, but it's better if that's around, in case someone wants to tinker here.
					connectionParameters.EndpointConnector = new DefaultEndpointConnector(allowOnlyTorEndpoints: true);

					await AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager);
				}
				Nodes = new NodesGroup(Network, connectionParameters, requirements: Constants.NodeRequirements);

				RegTestMemPoolServingNode = null;
			}

			if (Config.UseTor.Value)
			{
				Synchronizer = new WasabiSynchronizer(Network, IndexFilePath, () => Config.GetCurrentBackendUri(), Config.GetTorSocks5EndPoint());
			}
			else
			{
				Synchronizer = new WasabiSynchronizer(Network, IndexFilePath, Config.GetFallbackBackendUri(), null);
			}

			UpdateChecker = new UpdateChecker(Synchronizer.WasabiClient);

			Nodes.Connect();
			Logger.LogInfo("Start connecting to nodes...");

			if (RegTestMemPoolServingNode != null)
			{
				RegTestMemPoolServingNode.VersionHandshake();
				Logger.LogInfo("Start connecting to mempool serving regtest node...");
			}

			var requestInterval = TimeSpan.FromSeconds(30);
			if (Network == Network.RegTest)
			{
				requestInterval = TimeSpan.FromSeconds(5);
			}

			int maxFiltSyncCount = Network == Network.Main ? 1000 : 10000; // On testnet, filters are empty, so it's faster to query them together

			Synchronizer.Start(requestInterval, TimeSpan.FromMinutes(5), maxFiltSyncCount);
			Logger.LogInfo("Start synchronizing filters...");
		}

		private static async Task AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager addressManager)
		{
			var onions = new []{
				"226eupdnaouu4h2v.onion:8333",
				"2363mzfyhtaqisc6.onion:8333",
				"23wdfqkzttmenvki.onion:8333",
				"27et4tn3kmxim7lf.onion:8333",
				"2bfsxzluysybysnr.onion:8333",
				"2cg5rn5ke5sf4gye.onion:8333",
				"2g34i6wosztpuli2.onion:8333",
				"2ggj4ytzs5jlsydx.onion:8333",
				"2qudbhlnvqpli3sz.onion:8333",
				"2rgvda7arxp4cra4.onion:8333",
				"2ujxdfovfyjpmdto.onion:8333",
				"2w2qhja7y2on4f3h.onion:8333",
				"3esk7tmbjiusuilx.onion:8333",
				"3ihjnsvwc3x6dp2o.onion:8333",
				"3r44ddzjitznyahw.onion:8333",
				"3wauhhcdlqkz767z.onion:8333",
				"42ifiyia5wwmy4ak.onion:8333",
				"4cb573gir6xkhsiv.onion:8333",
				"4ekcosssksohmh2c.onion:8333",
				"4glotrrcfhsch3uu.onion:8333",
				"4iogxfitcp55kafm.onion:8333",
				"4ls4o6iszcd7mkfw.onion:8333",
				"4mewwo2bfxk6lg3f.onion:8333",
				"4rnzfqkmoqi7so6c.onion:8333",
				"4u24dwtlaadhzlmt.onion:8333",
				"4zhilusjsuall6ll.onion:8333",
				"55zzzsk7iqv6p3ew.onion:8333",
				"56stijc6kcgw6flk.onion:8333",
				"57acrug2miefbr2r.onion:8333",
				"5c7of5s55r75jnkp.onion:8333",
				"5ityqoefmhlqhjgr.onion:8333",
				"5iv4c4plh2dt35rx.onion:8333",
				"5n6mizutgidro4ee.onion:8333",
				"5oismdgoxjt3i5n4.onion:8333",
				"5qdxswcgigxilqin.onion:8333",
				"5qhgxcn24n4ffi4o.onion:8333",
				"5r3g7gjggyhfbqdl.onion:8333",
				"5wnkqzjzjehmq7hn.onion:8333",
				"5z2she4d6fvrdnme.onion:8333",
				"627jdioviypreq7v.onion:8333",
				"6fp3i7f2pbie7w7t.onion:8333",
				"6tsduvdfed4ns5x6.onion:8333",
				"6wcfnbb3vmaw6cwa.onion:8333",
				"72pulxp4k4nkjco2.onion:8333",
				"74qvj45antins3ik.onion:8333",
				"7a744wgfanb6i425.onion:8333",
				"7h2hri7mjbzb53io.onion:8333",
				"7oicn4slyvovosvc.onion:8333",
				"7rjgknhabdumwxhg.onion:8333",
				"7sgbn3tbqirrudlg.onion:8333",
				"7u3dlwlrnz4fr2f6.onion:8333",
				"ai5r2diozoe7rrdz.onion:8333",
				"alhlegtjkdmbqsvt.onion:8333",
				"alybxrrgrdzfnhed.onion:8333",
				"ap4zz4imxbdl6plr.onion:8333",
				"aqj65bjrfpehwn3u.onion:8333",
				"aszhe54dqoiihjbs.onion:8333",
				"avn4hevnwomzme3d.onion:8333",
				"b6mutxpzskpeuew5.onion:8333",
				"ba4vmd2seysfkfsz.onion:8333",
				"bc7i62e65vkge3tv.onion:8333",
				"bddx3vczvpio35wd.onion:8333",
				"bgmozc72qevogtez.onion:8333",
				"bitcoin4rlfa4wqx.onion:8333",
				"bitcoinzi27kiwf6.onion:8333",
				"bwzr3bpv6tu7gc5c.onion:8333",
				"bxxvkb7czrxtvz2c.onion:8333",
				"c5aourpwazvoxrqd.onion:8333",
				"c5urqrojr3rcdw3v.onion:8333",
				"cernrmrk5zomzozn.onion:8333",
				"cgk4u2lxrvml4jvb.onion:8333",
				"chiphuvuwoietiye.onion:8333",
				"ckdcaofvfv73dja5.onion:8333",
				"cmbosvl47eyvxz6t.onion:8333",
				"cpyfqbs4fs3vnbpf.onion:8333",
				"cqoenl4w6r3pcz5c.onion:8333",
				"cyvpgt25274i5b7c.onion:8333",
				"d5upe4qdsy6tit5z.onion:8333",
				"d6fan7afc3dc7lxi.onion:8333",
				"d6hwbavvfww3x5oz.onion:8333",
				"d7axutsxafzswgze.onion:8333",
				"dbbrlggnzagdeh75.onion:8333",
				"dbgf6fc7ndpbfkpe.onion:8333",
				"dex67kijcj7qoxxs.onion:8333",
				"do2ebqj4kzhlw444.onion:8333",
				"dxtorauxe4mxja72.onion:8333",
				"dz6f6f7qhdgjz7kk.onion:8333",
				"e7rjoluxbw3ocec2.onion:8333",
				"ep2jcmtrhfmaruly.onion:8333",
				"ep2mjzox3kvb6ax4.onion:8333",
				"epeg6av6fb6pfz6w.onion:8333",
				"etofym3tnrn3k42u.onion:8333",
				"ezjy6vcbldavkgg6.onion:8333",
				"ezkr7stq4w7ohjrt.onion:8333",
				"f4xkelqlxeybxgwf.onion:8333",
				"f6kzyiyaasb5ae6t.onion:8333",
				"fno4aakpl6sg6y47.onion:8333",
				"fqqtematwhfoqbat.onion:8333",
				"fscb2bbkargtesw7.onion:8333",
				"fvae4ebvuuulkbro.onion:8333",
				"fwnq4qmhuwgh2kdf.onion:8333",
				"fz6nsij6jiyuwlsc.onion:8333",
				"fznj2nijtuc7qmf4.onion:8333",
				"g3xgqirastjhpsn6.onion:8333",
				"g6rxigsqxwlmysyf.onion:8333",
				"gclq4svxe3qb6ohh.onion:8333",
				"gddgd6s562x4dndd.onion:8333",
				"gfbi4yc2e4r5ppfk.onion:8333",
				"ggdy2pb2avlbtjwq.onion:8333",
				"godfxzfs63mwxdfn.onion:8333",
				"gqjjzlubeyhoz7n5.onion:8333",
				"gs7hn7npx7hin4bb.onion:8333",
				"hbuair37dxnblurw.onion:8333",
				"hodliooelmajvwyc.onion:8233",
				"hwiyj3zk53gsdlkj.onion:8333",
				"hwo2biyndrrvpl6f.onion:8333",
				"hyipxou2glnor6ug.onion:8333",
				"i3fu5n3sseg5rbiu.onion:8333",
				"ibiuq4qr3jejkvm3.onion:8333",
				"idt2wgpuqtsc3wo5.onion:8333",
				"ihfgsiuulcnbuzy2.onion:8333",
				"iieuwsb4krwktkrz.onion:8333",
				"iixzdt2qbs2eeoxt.onion:8333",
				"imperialnza3tqgh.onion:8333",
				"in4msxbyffjvhuyh.onion:8333",
				"iqdog7ifcsvffsqr.onion:8220",
				"j3w566u6mpqwa27s.onion:8333",
				"j3wqsmrfkcy66yes.onion:8333",
				"j7jji2z35b3enrab.onion:8333",
				"jd7rbpuml375dtft.onion:8333",
				"jjrsewwt7sqkfnvu.onion:8333",
				"jjsg3dol757yihwv.onion:8333",
				"jkdzluvkxl5o4uhs.onion:8333",
				"jnumdbzb3hb3bmje.onion:8333",
				"jzvmidh23z7ltxfs.onion:8333",
				"k5uro6qhbn4nxr7i.onion:8333",
				"ka47ld4bkxryumap.onion:8333",
				"kavhbptm33bmihrh.onion:8333",
				"kbbjqhxp47f3dgtz.onion:8333",
				"kedn3x6peubcwlqm.onion:8333",
				"khhcpfkrlbuokmkh.onion:8333",
				"kkdas3qebkosygu5.onion:8333",
				"kmm7zr5eh4jnjmo3.onion:8333",
				"ktqeffpxgzkra4om.onion:8333",
				"l5oddj46pvoddzd4.onion:8333",
				"l6vfl6zepydtoygw.onion:8333",
				"legtokg7ff4nrxvj.onion:8333",
				"lgkvbvro67jomosw.onion:8333",
				"lhnter7kuddwcwzh.onion:8333",
				"lv3ctweq3fxejwjx.onion:8333",
				"lw65yqrh523g2den.onion:8333",
				"m3vfylbw5d3q6ziq.onion:8333",
				"m5mcgk7ijzq4ljoo.onion:8333",
				"m6jrhrqvjbnzla3r.onion:8333",
				"mdte2do4t5pehjif.onion:8333",
				"mlrycyguwqvimzl5.onion:8333",
				"mrwpvwqjp6rs2naf.onion:9333",
				"n3iymrfvevmp2yuf.onion:8333",
				"n4a3uusw3kiupjy3.onion:8333",
				"nbi2nmia4glwhbve.onion:8333",
				"nihbp4nyxtyv2qz7.onion:8333",
				"njfg7f35qzpnt2ff.onion:8333",
				"nogxmgjwisao75c6.onion:8333",
				"nqkhbvckh45k6xwo.onion:8333",
				"nrrmkgmulpgsbwlt.onion:8333",
				"nwnik2ibigscln7t.onion:8333",
				"nzvi2gphc6zbufeg.onion:8333",
				"o2tku2dbsd6iumch.onion:8333",
				"o4gv4de546woh7pp.onion:8333",
				"o5zsgfa7zocvhejp.onion:8335",
				"o7ol2b23cdu7w5ze.onion:8333",
				"obmai7gv7vumbbsf.onion:8333",
				"odzbk575vkbyo7hu.onion:8333",
				"p23ifh4k3os5we73.onion:8333",
				"p2yx46bipnbwkrzx.onion:8333",
				"p3aj5tjey6dzsjr7.onion:8333",
				"p3u6ynrcd2q54dyg.onion:8333",
				"pcfhsdqzs6q63ryu.onion:8333",
				"peyiyaj5uehktohe.onion:8333",
				"pffwqxvuldeq55zc.onion:8333",
				"pfyj6jcs6qfz6whg.onion:8333",
				"pvwgqbzzi3jwd2yf.onion:8333",
				"pwa4uc7qfhbbxbdl.onion:8333",
				"qe2wfge53yb5htk2.onion:8333",
				"qj6irqn2i73edff5.onion:8333",
				"qjpejfzlwqqnzsol.onion:8333",
				"qpwocpl77ksa5ozs.onion:8333",
				"r4tt5s764go7jggd.onion:8333",
				"rqake6ppkvv7rr7o.onion:8333",
				"rszpctzclwfp5ufu.onion:8333",
				"s3fp6vs5mcjzf5ve.onion:8333",
				"s5xheii55elshwne.onion:8333",
				"seoskudzk6vn6mqz.onion:8333",
				"sj2w6ooanckftd3n.onion:8333",
				"slfsm3cykmizi7tm.onion:8333",
				"sslnjjhnmwllysv4.onion:8333",
				"sza5dkccqm5egqgq.onion:8333",
				"t7jlaj6ggyx7s5vy.onion:8333",
				"tcdq5utrqhg3ljsp.onion:8333",
				"tfvfqbkl4e53uzk2.onion:8333",
				"toci5qgahb66kz33.onion:8333",
				"trtbvoteihn353zp.onion:8333",
				"tuetyloi54t7gmpp.onion:8333",
				"tvzvvfsrw4rh4spl.onion:8333",
				"txem5meug24g2ezd.onion:8333",
				"txgbh5trbmrcp4md.onion:8333",
				"txtteiquvpfqjics.onion:8333",
				"u3w5cvn6jwqsbpd4.onion:8333",
				"ubnv7itpyvzo3tqu.onion:8333",
				"uccmw67l4kgl646y.onion:8333",
				"uftbw4zi5wlzcwho.onion:8333",
				"ul3rmazdin7ygr65.onion:8333",
				"urapznegw7zbbkg7.onion:8333",
				"us2rnztwn6zpbttm.onion:8333",
				"uturfbxtazsilwgm.onion:8333",
				"uz3pvdhie3372vxw.onion:8333",
				"uzbrujjemdvsyqxz.onion:8333",
				"v5jyiurhbbzdb3z4.onion:8333",
				"v6cmam2nyzltshuh.onion:8333",
				"vcscqpaw2ylak5yd.onion:8333",
				"vdhb3sy4ieswb76n.onion:8333",
				"vemurvtu32dgcrps.onion:8333",
				"vev3n5fxfrtqj6e5.onion:8333",
				"vhdoxqq63xr53ol7.onion:8333",
				"vidjqgmwtwwa4mdo.onion:8333",
				"vlf5i3grro3wux24.onion:8333",
				"vomeacttinx3mpml.onion:8333",
				"vp24piiyb4w2qk7k.onion:8333",
				"vpcwfeadh5sezxe3.onion:8333",
				"vqjhqcigexo4prin.onion:8333",
				"vsazvsm5zfrazhvi.onion:8333",
				"vuia3j4jnqtd6ng5.onion:8333",
				"w25o5aow4gt2lufk.onion:8333",
				"w4yimpywyhue75ww.onion:8333",
				"wgw5w4omdqv34sgy.onion:8333",
				"woatyth7nrsh7nj6.onion:8333",
				"wykoudmcnxbv3o2e.onion:8333",
				"x2anega6z42vv5ex.onion:8333",
				"x3eaxz5thuzihupo.onion:8333",
				"x4a2mk2uur2erufh.onion:8333",
				"x7woumstvlqgmipn.onion:8333",
				"xbl7n2uig4fxz2kn.onion:8333",
				"xhmice6ajwwcwfij.onion:8333",
				"xii2vuyhuyth56av.onion:8333",
				"xkaz5a45gekata4d.onion:8333",
				"xnuk3xxkc5vd4e2s.onion:8333",
				"xo5rjhgwjp4xs2o3.onion:8333",
				"xoxoxka3hgpokemn.onion:8333",
				"xu47bf37wttxnxd5.onion:8333",
				"xudkoztdfrsuyyou.onion:8333",
				"xuxtlrthafo6y42u.onion:8333",
				"xv7pt6etwxiygss6.onion:8444",
				"y4vw6zkukvvc7f4d.onion:8333",
				"y5lni7kvq4yjx2fc.onion:8333",
				"ydscw35avlov5j7h.onion:8333",
				"yuqf2zq22aubyxg3.onion:8333",
				"ywsr2jjwm6w2tkuq.onion:8333",
				"z33nukt7ngik3cpe.onion:8333",
				"z5x2wes6mhbml2t5.onion:8333",
				"zdphl22tbzcwuz7w.onion:8333",
				"zf4nfw47kgvrcrsl.onion:8333",
				"zkij3rtalexrzh5d.onion:8333",
				"zo5dklwelmdrpo5n.onion:8333",
				"zqjvtxskxonu4kzv.onion:8333",
				"zsenp64lsvvdroig.onion:8333",
				"zzwepx6slqzm6ndq.onion:8333",
			};

			onions.Shuffle();
			foreach(var onion in onions.Take(60))
			{
				if(NBitcoin.Utils.TryParseEndpoint(onion, 8333, out var endpoint))
				{
					await addressManager.AddAsync(endpoint);
				}
			}
		}

		private static CancellationTokenSource CancelWalletServiceInitialization = null;

		public static async Task InitializeWalletServiceAsync(KeyManager keyManager)
		{
			if (Config.UseTor.Value)
			{
				ChaumianClient = new CcjClient(Synchronizer, Network, keyManager, () => Config.GetCurrentBackendUri(), Config.GetTorSocks5EndPoint());
			}
			else
			{
				ChaumianClient = new CcjClient(Synchronizer, Network, keyManager, Config.GetFallbackBackendUri(), null);
			}
			WalletService = new WalletService(keyManager, Synchronizer, ChaumianClient, MemPoolService, Nodes, DataDir, Config.ServiceConfiguration);

			ChaumianClient.Start();
			Logger.LogInfo("Start Chaumian CoinJoin service...");

			using (CancelWalletServiceInitialization = new CancellationTokenSource())
			{
				Logger.LogInfo("Starting WalletService...");
				await WalletService.InitializeAsync(CancelWalletServiceInitialization.Token);
				Logger.LogInfo("WalletService started.");
			}
			CancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.
			WalletService.Coins.CollectionChanged += Coins_CollectionChanged;
		}

		public static string GetWalletFullPath(string walletName)
		{
			walletName = walletName.TrimEnd(".json", StringComparison.OrdinalIgnoreCase);
			return Path.Combine(WalletsDir, walletName + ".json");
		}

		public static string GetWalletBackupFullPath(string walletName)
		{
			walletName = walletName.TrimEnd(".json", StringComparison.OrdinalIgnoreCase);
			return Path.Combine(WalletBackupsDir, walletName + ".json");
		}

		public static KeyManager LoadKeyManager(string walletFullPath, string walletBackupFullPath)
		{
			try
			{
				return LoadKeyManager(walletFullPath);
			}
			catch (Exception ex)
			{
				if (!File.Exists(walletBackupFullPath))
				{
					throw;
				}

				Logger.LogWarning($"Wallet got corrupted.\n" +
					$"Wallet Filepath: {walletFullPath}\n" +
					$"Trying to recover it from backup.\n" +
					$"Backup path: {walletBackupFullPath}\n" +
					$"Exception: {ex.ToString()}");
				if (File.Exists(walletFullPath))
				{
					string corruptedWalletBackupPath = Path.Combine(WalletBackupsDir, $"{Path.GetFileName(walletFullPath)}_CorruptedBackup");
					if (File.Exists(corruptedWalletBackupPath))
					{
						File.Delete(corruptedWalletBackupPath);
						Logger.LogInfo($"Deleted previous corrupted wallet file backup from {corruptedWalletBackupPath}.");
					}
					File.Move(walletFullPath, corruptedWalletBackupPath);
					Logger.LogInfo($"Backed up corrupted wallet file to {corruptedWalletBackupPath}.");
				}
				File.Copy(walletBackupFullPath, walletFullPath);

				return LoadKeyManager(walletFullPath);
			}
		}

		public static KeyManager LoadKeyManager(string walletFullPath)
		{
			KeyManager keyManager;

			// Set the LastAccessTime.
			new FileInfo(walletFullPath)
			{
				LastAccessTime = DateTime.Now
			};

			keyManager = KeyManager.FromFile(walletFullPath);
			Logger.LogInfo($"Wallet loaded: {Path.GetFileNameWithoutExtension(keyManager.FilePath)}.");
			return keyManager;
		}

		private static void Coins_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					return;
				}

				if (e.Action == NotifyCollectionChangedAction.Add)
				{
					foreach (SmartCoin coin in e.NewItems)
					{
						//if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.OSDescription.StartsWith("Microsoft Windows 10"))
						//{
						//	// It's harder than you'd think. Maybe the best would be to wait for .NET Core 3 for WPF things on Windows?
						//}
						// else

						using (var process = Process.Start(new ProcessStartInfo
						{
							FileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osascript" : "notify-send",
							Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"-e \"display notification \\\"Received {coin.Amount.ToString(false, true)} BTC\\\" with title \\\"Wasabi\\\"\"" : $"--expire-time=3000 \"Wasabi\" \"Received {coin.Amount.ToString(false, true)} BTC\"",
							CreateNoWindow = true
						})) { };
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
		}

		public static async Task DisposeInWalletDependentServicesAsync()
		{
			if (WalletService != null)
			{
				WalletService.Coins.CollectionChanged -= Coins_CollectionChanged;
			}
			CancelWalletServiceInitialization?.Cancel();
			CancelWalletServiceInitialization = null;

			if (WalletService != null)
			{
				if (WalletService.KeyManager != null) // This should not ever happen.
				{
					string backupWalletFilePath = Path.Combine(WalletBackupsDir, Path.GetFileName(WalletService.KeyManager.FilePath));
					WalletService.KeyManager?.ToFile(backupWalletFilePath);
					Logger.LogInfo($"{nameof(KeyManager)} backup saved to {backupWalletFilePath}.", nameof(Global));
				}
				WalletService?.Dispose();
				WalletService = null;
				Logger.LogInfo($"{nameof(WalletService)} is stopped.", nameof(Global));
			}

			if (ChaumianClient != null)
			{
				await ChaumianClient.StopAsync();
				ChaumianClient = null;
				Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.", nameof(Global));
			}
		}

		public static async Task DisposeAsync()
		{
			try
			{
				await DisposeInWalletDependentServicesAsync();

				if (UpdateChecker != null)
				{
					UpdateChecker?.Dispose();
					Logger.LogInfo($"{nameof(UpdateChecker)} is stopped.", nameof(Global));
				}

				if (Synchronizer != null)
				{
					Synchronizer?.Dispose();
					Logger.LogInfo($"{nameof(Synchronizer)} is stopped.", nameof(Global));
				}

				if (AddressManagerFilePath != null)
				{
					IoHelpers.EnsureContainingDirectoryExists(AddressManagerFilePath);
					if (AddressManager != null)
					{
						AddressManager?.SavePeerFile(AddressManagerFilePath, Config.Network);
						Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.", nameof(Global));
					}
				}

				if (Nodes != null)
				{
					Nodes?.Dispose();
					Logger.LogInfo($"{nameof(Nodes)} are disposed.", nameof(Global));
				}

				if (RegTestMemPoolServingNode != null)
				{
					RegTestMemPoolServingNode.Disconnect();
					Logger.LogInfo($"{nameof(RegTestMemPoolServingNode)} is disposed.", nameof(Global));
				}

				if (TorManager != null)
				{
					TorManager?.Dispose();
					Logger.LogInfo($"{nameof(TorManager)} is stopped.", nameof(Global));
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Global));
			}
		}
	}
}
