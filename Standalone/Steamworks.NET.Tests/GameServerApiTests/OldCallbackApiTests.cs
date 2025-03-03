﻿using NUnit.Framework.Constraints;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Steamworks.NET.Tests.GameServerApiTests;

[TestFixture]
public class OldCallbackApiTests
{
	private ulong modFAGE_id = 1579583001;
	private CancellationTokenSource dispatchThreadStop;
	private Thread dispatchThread = null!;
	private CallResult<SteamUGCQueryCompleted_t> callresult;
	private Callback<GameWebCallback_t> callback;

	[OneTimeSetUp]
	public void SetupOnce()
	{
		// dispatcher = ValueTaskDispatcher.Singleton;
		dispatchThreadStop = new();
		dispatchThread = new(() =>
		{
			while (!dispatchThreadStop.IsCancellationRequested)
			{
				GameServer.RunCallbacks();
				Thread.Sleep(100);
			}
		})
		{
			Name = "Steamworks.NET dispatcher thread",
			IsBackground = true
		};
		dispatchThread.Start();
	}

	[OneTimeTearDown]
	public void TeardownOnce()
	{
		dispatchThreadStop.Cancel();
		dispatchThreadStop.Dispose();
	}


	[SetUp]
	public void Setup()
	{

	}

	[TearDown]
	public void Teardown()
	{
		callresult?.Dispose();
	}

	[Test]
	public void TestCallResult()
	{
		var queryHandle = SteamGameServerUGC.CreateQueryUGCDetailsRequest([new(modFAGE_id)], 1);
		callresult = new CallResult<SteamUGCQueryCompleted_t>();
		Barrier barrier = new(2);

		ExceptionDispatchInfo? testEDI = null;

		var handle = SteamGameServerUGC.SendQueryUGCRequest(queryHandle);
		callresult.Set(handle, (queryResult, fail) =>
		{
			try
			{
				if (fail)
				{
					Assert.Pass();
				}

				Assert.That(queryResult.m_eResult, Is.EqualTo(EResult.k_EResultFail));
				
			}
			catch (Exception e)
			{
				testEDI = ExceptionDispatchInfo.Capture(e);
			}
			barrier.SignalAndWait(2);
		});

		barrier.SignalAndWait();
		testEDI?.Throw();
	}

	[Test]
	public void TestCallback()
	{
		Assert.Multiple(() =>
		{
			CallbackTest cbTest = new();

			callback = new Callback<GameWebCallback_t>((w) => { 
				if (w.m_szURL == CallbackTest.TestSteamUrl)
				{
					cbTest.CalledTimes++;
				}
				callback.Unregister();
			}, true);
			ProcessStartInfo si = new(CallbackTest.TestSteamUrl)
			{
				UseShellExecute = true
			};
			Process.Start(si);
			Thread.Sleep(200);
			Process.Start(si);
			
		});
	}


	private class CallbackTest
	{
		public int CalledTimes = 0;
		public const string TestSteamUrl = "steam://snet-automated-test/callback-dispatch";
	}
}
