// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace System.Net.WebSockets.Client.Tests
{
    [ConditionalClass(typeof(ClientWebSocketTestBase), nameof(WebSocketsSupported))]
    [SkipOnPlatform(TestPlatforms.Browser, "System.Net.Sockets are not supported on browser")]
    public abstract class AbortTest_LoopbackBase(ITestOutputHelper output) : AbortTestBase(output)
    {
        #region Common (Echo Server) tests

        [Theory, MemberData(nameof(UseSsl))]
        public Task Abort_ConnectAndAbort_ThrowsWebSocketExceptionWithMessage(bool useSsl) => RunEchoAsync(
            RunClient_Abort_ConnectAndAbort_ThrowsWebSocketExceptionWithMessage, useSsl);

        [Theory, MemberData(nameof(UseSsl))]
        public Task Abort_SendAndAbort_Success(bool useSsl) => RunEchoAsync(
            RunClient_Abort_SendAndAbort_Success, useSsl);

        [Theory, MemberData(nameof(UseSsl))]
        public Task Abort_ReceiveAndAbort_Success(bool useSsl) => RunEchoAsync(
            RunClient_Abort_ReceiveAndAbort_Success, useSsl);

        [Theory, MemberData(nameof(UseSsl))]
        public Task Abort_CloseAndAbort_Success(bool useSsl) => RunEchoAsync(
            RunClient_Abort_CloseAndAbort_Success, useSsl);

        [Theory, MemberData(nameof(UseSsl))]
        public Task ClientWebSocket_Abort_CloseOutputAsync(bool useSsl) => RunEchoAsync(
            RunClient_ClientWebSocket_Abort_CloseOutputAsync, useSsl);

        #endregion

        #region Loopback-only tests

        public static object[][] AbortTypeAndUseSslAndBoolean = ToMemberData(Enum.GetValues<AbortType>(), UseSsl_Values, Bool_Values);

        [Theory]
        [MemberData(nameof(AbortTypeAndUseSslAndBoolean))]
        public Task AbortClient_ServerGetsCorrectException(AbortType abortType, bool useSsl, bool verifySendReceive)
        {
            var clientMsg = new byte[] { 1, 2, 3, 4, 5, 6 };
            var serverMsg = new byte[] { 42 };
            var clientAckTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var serverAckTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var timeoutCts = new CancellationTokenSource(TimeOutMilliseconds);

            return LoopbackWebSocketServer.RunAsync(
                async uri =>
                {
                    ClientWebSocket clientWebSocket = await GetConnectedWebSocket(uri);

                    if (verifySendReceive)
                    {
                        await VerifySendReceiveAsync(clientWebSocket, clientMsg, serverMsg, clientAckTcs, serverAckTcs.Task, timeoutCts.Token);
                    }

                    switch (abortType)
                    {
                        case AbortType.Abort:
                            clientWebSocket.Abort();
                            break;
                        case AbortType.Dispose:
                            clientWebSocket.Dispose();
                            break;
                    }
                },
                async (serverWebSocket, token) =>
                {
                    if (verifySendReceive)
                    {
                        await VerifySendReceiveAsync(serverWebSocket, serverMsg, clientMsg, serverAckTcs, clientAckTcs.Task, token);
                    }

                    var readBuffer = new byte[1];
                    var exception = await Assert.ThrowsAsync<WebSocketException>(async () =>
                        await serverWebSocket.ReceiveAsync(readBuffer, token));

                    Assert.Equal(WebSocketError.ConnectionClosedPrematurely, exception.WebSocketErrorCode);
                    Assert.Equal(WebSocketState.Aborted, serverWebSocket.State);
                },
                new LoopbackWebSocketServer.Options(HttpVersion, useSsl) { DisposeServerWebSocket = true },
                timeoutCts.Token);
        }

        public static object[][] ServerEosTypeAndUseSsl = ToMemberData(Enum.GetValues<ServerEosType>(), UseSsl_Values);

        [Theory]
        [MemberData(nameof(ServerEosTypeAndUseSsl))]
        public Task ServerPrematureEos_ClientGetsCorrectException(ServerEosType serverEosType, bool useSsl)
        {
            var clientMsg = new byte[] { 1, 2, 3, 4, 5, 6 };
            var serverMsg = new byte[] { 42 };
            var clientAckTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var serverAckTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var timeoutCts = new CancellationTokenSource(TimeOutMilliseconds);

            var serverReceivedEosTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var clientReceivedEosTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            return LoopbackWebSocketServer.RunAsync(
                async uri =>
                {
                    var token = timeoutCts.Token;
                    ClientWebSocket clientWebSocket = await GetConnectedWebSocket(uri);

                    if (serverEosType == ServerEosType.AfterSomeData)
                    {
                        await VerifySendReceiveAsync(clientWebSocket, clientMsg, serverMsg, clientAckTcs, serverAckTcs.Task, token).ConfigureAwait(false);
                    }

                    // only one side of the stream was closed. the other should work
                    await clientWebSocket.SendAsync(clientMsg, WebSocketMessageType.Binary, endOfMessage: true, token).ConfigureAwait(false);

                    var exception = await Assert.ThrowsAsync<WebSocketException>(() => clientWebSocket.ReceiveAsync(new byte[1], token));
                    Assert.Equal(WebSocketError.ConnectionClosedPrematurely, exception.WebSocketErrorCode);

                    clientReceivedEosTcs.SetResult();
                    clientWebSocket.Dispose();
                },
                async (requestData, token) =>
                {
                    WebSocket serverWebSocket = null!;
                    await SendServerResponseAndEosAsync(
                        requestData,
                        serverEosType,
                        (wsData, ct) =>
                        {
                            var wsOptions = new WebSocketCreationOptions { IsServer = true };
                            serverWebSocket = WebSocket.CreateFromStream(wsData.TransportStream, wsOptions);

                            return serverEosType == ServerEosType.AfterSomeData
                                ? VerifySendReceiveAsync(serverWebSocket, serverMsg, clientMsg, serverAckTcs, clientAckTcs.Task, ct)
                                : Task.CompletedTask;
                        },
                        token);

                    Assert.NotNull(serverWebSocket);

                    // only one side of the stream was closed. the other should work
                    var readBuffer = new byte[clientMsg.Length];
                    var result = await serverWebSocket.ReceiveAsync(readBuffer, token);
                    Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
                    Assert.Equal(clientMsg.Length, result.Count);
                    Assert.True(result.EndOfMessage);
                    Assert.Equal(clientMsg, readBuffer);

                    await clientReceivedEosTcs.Task.WaitAsync(token).ConfigureAwait(false);

                    var exception = await Assert.ThrowsAsync<WebSocketException>(() => serverWebSocket.ReceiveAsync(readBuffer, token));
                    Assert.Equal(WebSocketError.ConnectionClosedPrematurely, exception.WebSocketErrorCode);

                    serverWebSocket.Dispose();
                },
                new LoopbackWebSocketServer.Options(HttpVersion, useSsl) { SkipServerHandshakeResponse = true },
                timeoutCts.Token);
        }

        protected abstract Task SendServerResponseAndEosAsync(WebSocketRequestData data, ServerEosType eos, Func<WebSocketRequestData, CancellationToken, Task> callback, CancellationToken ct);

        public enum AbortType
        {
            Abort,
            Dispose
        }

        public enum ServerEosType
        {
            WithHeaders,
            RightAfterHeaders,
            AfterSomeData
        }

        protected static async Task VerifySendReceiveAsync(WebSocket ws, byte[] localMsg, byte[] remoteMsg,
            TaskCompletionSource localAckTcs, Task remoteAck, CancellationToken cancellationToken)
        {
            var sendTask = ws.SendAsync(localMsg, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);

            var recvBuf = new byte[remoteMsg.Length * 2];
            var recvResult = await ws.ReceiveAsync(recvBuf, cancellationToken).ConfigureAwait(false);

            Assert.Equal(WebSocketMessageType.Binary, recvResult.MessageType);
            Assert.Equal(remoteMsg.Length, recvResult.Count);
            Assert.True(recvResult.EndOfMessage);
            Assert.Equal(remoteMsg, recvBuf[..recvResult.Count]);

            localAckTcs.SetResult();

            await sendTask.ConfigureAwait(false);
            await remoteAck.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }

    public abstract class AbortTest_Loopback(ITestOutputHelper output) : AbortTest_LoopbackBase(output)
    {
        protected override Task SendServerResponseAndEosAsync(WebSocketRequestData data, ServerEosType eos, Func<WebSocketRequestData, CancellationToken, Task> callback, CancellationToken ct)
            => WebSocketHandshakeHelper.SendHttp11ServerResponseAndEosAsync(data, callback, ct);
    }

    public abstract class AbortTest_Http2Loopback(ITestOutputHelper output) : AbortTest_LoopbackBase(output)
    {
        internal override Version HttpVersion => Net.HttpVersion.Version20;

        protected override Task SendServerResponseAndEosAsync(WebSocketRequestData data, ServerEosType eos, Func<WebSocketRequestData, CancellationToken, Task> callback, CancellationToken ct)
            => WebSocketHandshakeHelper.SendHttp2ServerResponseAndEosAsync(data, eosInHeadersFrame: eos == ServerEosType.WithHeaders, callback, ct);
    }

    #region Runnable test classes: HTTP/1.1 Loopback

    public sealed class AbortTest_SharedHandler_Loopback(ITestOutputHelper output) : AbortTest_Loopback(output) { }

    public sealed class AbortTest_Invoker_Loopback(ITestOutputHelper output) : AbortTest_Loopback(output)
    {
        protected override bool UseCustomInvoker => true;
    }

    public sealed class AbortTest_HttpClient_Loopback(ITestOutputHelper output) : AbortTest_Loopback(output)
    {
        protected override bool UseHttpClient => true;
    }

    #endregion

    #region Runnable test classes: HTTP/2 Loopback

    public sealed class AbortTest_Invoker_Http2Loopback(ITestOutputHelper output) : AbortTest_Http2Loopback(output)
    {
        protected override bool UseCustomInvoker => true;
    }

    public sealed class AbortTest_HttpClient_Http2Loopback(ITestOutputHelper output) : AbortTest_Http2Loopback(output)
    {
        protected override bool UseHttpClient => true;
    }

    #endregion
}
