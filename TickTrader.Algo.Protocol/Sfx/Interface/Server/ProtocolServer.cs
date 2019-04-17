﻿using NLog;
using System;
using System.Collections.Generic;
using TickTrader.Algo.Protocol.Sfx.Lib;
using SfxProtocol = SoftFX.Net.BotAgent;

namespace TickTrader.Algo.Protocol.Sfx
{
    public enum ServerStates { Started, Stopped, Faulted }


    public class ProtocolServer
    {
        private readonly ILogger _logger;
        private List<int> _subscriptionList;


        internal SfxProtocol.Server Server { get; set; }

        internal BotAgentServerListener Listener { get; set; }


        public ServerStates State { get; private set; }

        public IBotAgentServer AgentServer { get; }

        public IServerSettings Settings { get; }

        public VersionSpec VersionSpec { get; private set; }


        public ProtocolServer(IBotAgentServer agentServer, IServerSettings settings)
        {
            AgentServer = agentServer;
            Settings = settings;

            _logger = LoggerHelper.GetLogger("Protocol.Server", Settings.ProtocolSettings.LogDirectoryName, Settings.ServerName);
            _subscriptionList = new List<int>();

            State = ServerStates.Stopped;
        }


        public void Start()
        {
            try
            {
                if (State != ServerStates.Stopped)
                    throw new Exception($"Server is already {State}");

                VersionSpec = new VersionSpec();
                _logger.Info($"Server current version: {VersionSpec.CurrentVersionStr}");

                Listener = new BotAgentServerListener(AgentServer, _logger);

                Server = new SfxProtocol.Server(Settings.ServerName, Settings.CreateServerOptions())
                {
                    Listener = Listener,
                };

                Server.Start();

                State = ServerStates.Started;

                Listener.SessionSubscribed += OnSessionSubscribed;
                Listener.SessionUnsubscribed += OnSessionUnsubscribed;

                AgentServer.AccountUpdated += OnAccountUpdated;
                AgentServer.BotUpdated += OnBotUpdated;
                AgentServer.PackageUpdated += OnPackageUpdated;
                AgentServer.BotStateUpdated += OnBotStateUpdated;
                AgentServer.AccountStateUpdated += OnAccountStateUpdated;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to start protocol server: {ex.Message}");
                State = ServerStates.Faulted;
            }
        }

        public void Stop()
        {
            try
            {
                if (State == ServerStates.Started)
                {
                    Listener.SessionSubscribed -= OnSessionSubscribed;
                    Listener.SessionUnsubscribed -= OnSessionUnsubscribed;

                    AgentServer.AccountUpdated -= OnAccountUpdated;
                    AgentServer.BotUpdated -= OnBotUpdated;
                    AgentServer.PackageUpdated -= OnPackageUpdated;
                    AgentServer.BotStateUpdated -= OnBotStateUpdated;
                    AgentServer.AccountStateUpdated -= OnAccountStateUpdated;

                    State = ServerStates.Stopped;

                    Server.Stop();
                    Server.Join();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to stop protocol server: {ex.Message}");
            }
        }


        #region Update listeners

        private void OnSessionSubscribed(int sessionId)
        {
            if (State != ServerStates.Started)
                return;

            lock (_subscriptionList)
            {
                if (_subscriptionList.Contains(sessionId))
                    throw new ArgumentException($"Session {sessionId} already subscribed");

                _subscriptionList.Add(sessionId);
            }
        }

        private void OnSessionUnsubscribed(int sessionId)
        {
            if (State != ServerStates.Started)
                return;

            lock (_subscriptionList)
            {
                _subscriptionList.Remove(sessionId);
            }
        }

        private void OnAccountUpdated(AccountModelUpdateEntity update)
        {
            SendUpdate(update.ToMessage());
        }

        private void OnBotUpdated(BotModelUpdateEntity update)
        {
            SendUpdate(update.ToMessage());
        }

        private void OnPackageUpdated(PackageModelUpdateEntity update)
        {
            SendUpdate(update.ToMessage());
        }

        private void OnBotStateUpdated(BotStateUpdateEntity update)
        {
            SendUpdate(update.ToMessage());
        }

        private void OnAccountStateUpdated(AccountStateUpdateEntity update)
        {
            SendUpdate(update.ToMessage());
        }

        private void SendUpdate(SoftFX.Net.Core.Message update)
        {
            if (State != ServerStates.Started)
                return;

            try
            {
                lock (_subscriptionList)
                {
                    foreach (var sessionId in _subscriptionList)
                    {
                        Server.Send(sessionId, update);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to send update: {ex.Message}");
            }
        }

        #endregion Update listeners
    }
}