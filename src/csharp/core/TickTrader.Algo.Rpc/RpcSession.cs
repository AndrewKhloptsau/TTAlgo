﻿using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TickTrader.Algo.Util;

namespace TickTrader.Algo.Rpc
{
    public enum RpcSessionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3,
    }

    internal enum RpcSessionEvent
    {
        ConnectionRequest = 0,
        ConnectionSuccess = 1,
        ConnectionError = 2,
        HeartbeatMismatch = 3,
        DisconnectRequest = 4,
        DisconnectNotification = 5,
    }


    public readonly struct RpcSessionStateChangedArgs
    {
        public RpcSession Session { get; }

        public RpcSessionState OldState { get; }

        public RpcSessionState NewState { get; }


        public RpcSessionStateChangedArgs(RpcSession session, RpcSessionState oldState, RpcSessionState newState)
        {
            Session = session;
            OldState = oldState;
            NewState = newState;
        }
    }


    public class RpcSession
    {
        private static readonly RpcMessage HeartbeatMessage = RpcMessage.Notification(new Heartbeat());
        private static readonly IAlgoLogger _logger = AlgoLoggerFactory.GetLogger<RpcSession>();

        private readonly ITransportProxy _transport;
        private readonly IRpcHost _rpcHost;
        private readonly ConcurrentDictionary<string, IRpcResponseContext> _pendingRequests = new ConcurrentDictionary<string, IRpcResponseContext>();
        private readonly Subject<RpcSessionStateChangedArgs> _sessionStateSubject = new Subject<RpcSessionStateChangedArgs>();
        private readonly Channel<RpcSessionEvent> _eventBus = Channel.CreateUnbounded<RpcSessionEvent>();

        private IRpcHandler _rpcHandler;
        private Task _heartbeatTask;
        private CancellationTokenSource _heartbeatCancelTokenSrc;
        private int _inHeartbeatCnt, _outHeartbeatCnt;
        //private TaskCompletionSource<bool> _initTaskSrc;
        private TaskCompletionSource<bool> _connectTaskSrc;
        private TaskCompletionSource<bool> _disconnectTaskSrc;
        private ProtocolSpec _protocol;
        private string _disconnectReason;
        private string _sessionId;


        public RpcSessionState State { get; private set; }

        public IObservable<RpcSessionStateChangedArgs> ObserveStates => _sessionStateSubject;


        public RpcSession(ITransportProxy transport, IRpcHost rpcHost)
        {
            _transport = transport;
            _rpcHost = rpcHost;

            var _ = HandleEvents();
        }


        public Task Connect(ProtocolSpec protocol = null)
        {
            if (State != RpcSessionState.Disconnected)
                return _connectTaskSrc?.Task ?? Task.CompletedTask;

            _protocol = protocol;
            _connectTaskSrc = new TaskCompletionSource<bool>();
            _sessionId = Guid.NewGuid().ToString("N");

            PushEvent(RpcSessionEvent.ConnectionRequest);

            return _connectTaskSrc.Task;
        }

        public Task Disconnect(string reason)
        {
            if (State != RpcSessionState.Connected)
                return _disconnectTaskSrc?.Task ?? Task.CompletedTask;

            _disconnectTaskSrc = new TaskCompletionSource<bool>();

            _disconnectReason = reason;
            PushEvent(RpcSessionEvent.DisconnectRequest);

            return _disconnectTaskSrc.Task;
        }

        public void Tell(RpcMessage msg)
        {
            if (State != RpcSessionState.Connected)
                throw RpcStateException.NotConnected();

            SendMessage(msg);
        }

        public void Ask(RpcMessage msg, IRpcResponseContext responseContext)
        {
            if (State != RpcSessionState.Connected)
                throw RpcStateException.NotConnected();

            if (!_pendingRequests.TryAdd(msg.CallId, responseContext))
                throw RpcStateException.DuplicateCallId();

            SendMessage(msg);
        }


        internal void SendMessage(RpcMessage msg)
        {
            //_logger.Debug("Send msg: {msg}", new { msg.Flags, msg.CallId, msg.ProxyId, Payload = new { msg.Payload.TypeUrl, msg.Payload.Value } });
            _transport.WriteChannel.TryWrite(msg);
        }

        internal Task Close()
        {
            ChangeState(RpcSessionState.Disconnected);
            _sessionStateSubject.OnCompleted();
            _sessionStateSubject.Dispose();
            return _transport.Close();
        }


        private async Task HandleEvents()
        {
            await Task.Yield();

            var reader = _eventBus.Reader;

            while(!reader.Completion.IsCompleted)
            {
                var canRead = await reader.WaitToReadAsync().ConfigureAwait(false);
                if (!canRead)
                    return;

                while (reader.TryRead(out var sessionEvent))
                    await HandleEvent(sessionEvent);
            }
        }

        private async Task HandleEvent(RpcSessionEvent sessionEvent)
        {
            if (State == RpcSessionState.Disconnected && sessionEvent == RpcSessionEvent.ConnectionRequest)
            {
                ChangeState(RpcSessionState.Connecting);

                var _ = HandleMessages();

                if (_protocol != null)
                {
                    SendMessage(RpcMessage.Request(_sessionId, new ConnectRequest { Protocol = _protocol }));
                }
            }
            else if (State == RpcSessionState.Connecting && sessionEvent == RpcSessionEvent.ConnectionSuccess)
            {
                ChangeState(RpcSessionState.Connected);
                _connectTaskSrc.TrySetResult(true);
                _heartbeatCancelTokenSrc = new CancellationTokenSource();
                _heartbeatTask = HeartbeatLoop(_heartbeatCancelTokenSrc.Token);
            }
            else if (State == RpcSessionState.Connecting && sessionEvent == RpcSessionEvent.ConnectionError)
            {
                ChangeState(RpcSessionState.Disconnected);
                _connectTaskSrc.TrySetResult(false);
            }
            else if (State == RpcSessionState.Connected && sessionEvent == RpcSessionEvent.ConnectionError)
            {
                await DisconnectRoutine(false, true);
            }
            else if (State == RpcSessionState.Connected && sessionEvent == RpcSessionEvent.HeartbeatMismatch)
            {
                SendMessage(RpcMessage.Notification(_sessionId, new DisconnectMsg { Reason = _disconnectReason }));
                await DisconnectRoutine(false, true);
            }
            else if (State == RpcSessionState.Connected && sessionEvent == RpcSessionEvent.DisconnectRequest)
            {
                SendMessage(RpcMessage.Notification(_sessionId, new DisconnectMsg { Reason = _disconnectReason }));
                await DisconnectRoutine(true, false);
            }
            else if (State == RpcSessionState.Connected && sessionEvent == RpcSessionEvent.DisconnectNotification)
            {
                await DisconnectRoutine(true, false);
            }
        }

        private void PushEvent(RpcSessionEvent sessionEvent)
        {
            _eventBus.Writer.TryWrite(sessionEvent);
        }
        
        private async Task HandleMessages()
        {
            await Task.Yield();

            var readChannel = _transport.ReadChannel;

            while (!readChannel.Completion.IsCompleted)
            {
                var canRead = await readChannel.WaitToReadAsync().ConfigureAwait(false);
                if (!canRead)
                    break;

                while (readChannel.TryRead(out var msg))
                    HandleMessage(msg);
            }
            PushEvent(RpcSessionEvent.ConnectionError);
        }
        
        private void ChangeState(RpcSessionState newState)
        {
            var changeArgs = new RpcSessionStateChangedArgs(this, State, newState);
            State = newState;
            _sessionStateSubject.OnNext(changeArgs);
        }

        //private void SendConnect(ProtocolSpec protocol)
        //{
        //    if (State != RpcSessionState.Disconnected)
        //        return;

        //    ChangeState(RpcSessionState.Connecting);
        //    SendMessage(RpcMessage.Request(new ConnectRequest { Protocol = protocol }));
        //}

        private async Task DisconnectRoutine(bool isExpected, bool fast)
        {
            ChangeState(RpcSessionState.Disconnecting);
            if (_heartbeatTask != null)
            {
                _heartbeatCancelTokenSrc?.Cancel();
                await _heartbeatTask;
            }
            await _transport.Close();
            _disconnectTaskSrc?.TrySetResult(isExpected);
            ChangeState(RpcSessionState.Disconnected);
        }

        private void ConnectRequestHandler(RpcMessage msg)
        {
            var connectSuссessful = false;

            try
            {
                if (State != RpcSessionState.Connecting)
                    throw new RpcStateException($"Session in '{State}' state");

                _sessionId = msg.ProxyId;
                var request = msg.Payload.Unpack<ConnectRequest>();
                var response = ExecuteConnectRequest(request.Protocol);

                SendMessage(RpcMessage.Response(msg.CallId, response));

                connectSuссessful = response is ConnectResponse; //Must be last, SendMessage throw Exception protection
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"ConnectRequest failed");

                SendMessage(RpcMessage.Response(msg.CallId, new ErrorResponse
                {
                    Message = "Internal error: Failed to process ConnectRequest",
                    Details = ex.ToString(),
                }));
            }
            finally
            {
                PushEvent(connectSuссessful ? RpcSessionEvent.ConnectionSuccess : RpcSessionEvent.ConnectionError);
            }
        }

        private IMessage ExecuteConnectRequest(ProtocolSpec protocol)
        {
            protocol = _rpcHost.Resolve(protocol, out var resolveError);
            if (!string.IsNullOrEmpty(resolveError))
            {
                _logger.Error($"Failed to resolve protocol for spec '{protocol}': {resolveError}");

                return new ErrorResponse
                {
                    Message = $"Failed to resolve protocol. Url={protocol.Url}, Version={protocol.MajorVerion}.{protocol.MinorVerion}",
                    Details = resolveError,
                };
            }

            var initError = InitRpcHandler(protocol);
            if (initError != null)
                return initError;

            return new ConnectResponse { Protocol = protocol };
        }

        private void ConnectResponseHandler(ProtocolSpec protocol)
        {
            var initError = InitRpcHandler(protocol);
            if (initError != null)
            {
                SendMessage(RpcMessage.Notification(initError));
                _transport.Close();
                _connectTaskSrc.TrySetResult(false);
                return;
            }

            PushEvent(RpcSessionEvent.ConnectionSuccess);
        }

        private ErrorResponse InitRpcHandler(ProtocolSpec protocol)
        {
            try
            {
                _rpcHandler = _rpcHost.GetRpcHandler(protocol);
                _rpcHandler?.SetSession(this);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get protocol handler for spec '{protocol}'");

                return new ErrorResponse
                {
                    Message = $"Failed to get handler for protocol. Url={protocol.Url}, Version={protocol.MajorVerion}.{protocol.MinorVerion}",
                    Details = ex.ToString(),
                };
            }

            if (_rpcHandler == null)
            {
                _logger.Error($"Rpc handler not found for spec '{protocol}'");

                return new ErrorResponse
                {
                    Message = $"Internal error: Protocol handler not found. Url={protocol.Url}, Version={protocol.MajorVerion}.{protocol.MinorVerion}",
                };
            }

            return null;
        }

        private async Task HeartbeatLoop(CancellationToken cancelToken)
        {
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    _outHeartbeatCnt++;
                    SendMessage(HeartbeatMessage);
                    if (Math.Abs(_inHeartbeatCnt - _outHeartbeatCnt) > RpcConstants.HeartbeatCntThreshold)
                    {
                        _disconnectReason = $"Heartbeat count mismatch. Connection is out of sync (In: {_inHeartbeatCnt} / Out: {_outHeartbeatCnt}).";
                        PushEvent(RpcSessionEvent.HeartbeatMismatch);
                        return;
                    }
                    await Task.Delay(RpcConstants.HeartbeatTimeout, cancelToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) { }
        }

        private void HandleMessage(RpcMessage msg)
        {
            //_logger.Debug("Handle msg: {msg}", new { msg.Flags, msg.CallId, msg.ProxyId, Payload = new { msg.Payload.TypeUrl, msg.Payload.Value } });

            if (msg.Payload.Is(Heartbeat.Descriptor))
            {
                _inHeartbeatCnt++;
            }
            else if (msg.Payload.Is(ConnectRequest.Descriptor))
            {
                ConnectRequestHandler(msg);
            }
            else if (msg.Payload.Is(ConnectResponse.Descriptor))
            {
                var response = msg.Payload.Unpack<ConnectResponse>();
                ConnectResponseHandler(response.Protocol);
            }
            else if (msg.Payload.Is(DisconnectMsg.Descriptor))
            {
                _disconnectReason = msg.Payload.Unpack<DisconnectMsg>().Reason;
                PushEvent(RpcSessionEvent.DisconnectNotification);
            }
            else
            {
                if (msg.Flags == RpcFlags.Response)
                {
                    if (_pendingRequests.TryGetValue(msg.CallId, out var context))
                    {
                        var callFinished = context.OnNext(msg.Payload);
                        if (callFinished)
                            _pendingRequests.TryRemove(msg.CallId, out _);

                        return;
                    }
                }

                if (msg.Flags == RpcFlags.Request)
                {
                    _rpcHandler.HandleRequest(msg.ProxyId, msg.CallId, msg.Payload)
                        .ContinueWith(t =>
                        {
                            switch (t.Status)
                            {
                                case TaskStatus.RanToCompletion:
                                    var response = t.Result;
                                    if (response != null)
                                        SendMessage(RpcMessage.Response(msg.CallId, msg.ProxyId, response));
                                    break;
                                case TaskStatus.Faulted:
                                    _logger.Error(t.Exception, $"Failed to process request: {msg}");

                                    SendMessage(RpcMessage.Response(msg.CallId, msg.ProxyId, new ErrorResponse
                                    {
                                        Message = "Internal error: Failed to process request",
                                        Details = t.Exception.ToString(),
                                    }));
                                    break;
                                case TaskStatus.Canceled:
                                    SendMessage(RpcMessage.Response(msg.CallId, msg.ProxyId, new ErrorResponse
                                    {
                                        Message = "Request processing has been canceled",
                                    }));
                                    break;
                            }
                        });
                }

                if (msg.Flags == RpcFlags.Notification)
                {
                    if (msg.Payload.Is(ErrorResponse.Descriptor))
                    {
                        var error = msg.Payload.Unpack<ErrorResponse>();
                        _logger.Error($"Rpc error notification: {error}");
                    }
                    else
                    {
                        try
                        {
                            _rpcHandler.HandleNotification(msg.ProxyId, msg.CallId, msg.Payload);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, $"Failed to process notification: {msg}");
                        }
                    }
                }
            }
        }
    }
}
