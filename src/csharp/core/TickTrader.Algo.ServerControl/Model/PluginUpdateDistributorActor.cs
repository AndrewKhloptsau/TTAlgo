﻿using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TickTrader.Algo.Async.Actors;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Server.PublicAPI;

namespace TickTrader.Algo.ServerControl.Model
{
    internal sealed class PluginUpdateDistributorActor : Actor
    {
        private readonly IAlgoServerProvider _server;
        private readonly ILogger _logger;
        private readonly Dictionary<string, PluginSubNode> _logSubs = new Dictionary<string, PluginSubNode>();
        private readonly Dictionary<string, PluginSubNode> _statusSubs = new Dictionary<string, PluginSubNode>();


        private PluginUpdateDistributorActor(IAlgoServerProvider server, ILogger logger)
        {
            _server = server;
            _logger = logger;

            Receive<PluginUpdateDistributor.AddPluginLogsSubRequest>(r => AddSession(_logSubs, r.PluginId, r.Session));
            Receive<PluginUpdateDistributor.RemovePluginLogsSubRequest>(r => RemoveSession(_logSubs, r.PluginId, r.SessionId));
            Receive<PluginUpdateDistributor.AddPluginStatusSubRequest>(r => AddSession(_statusSubs, r.PluginId, r.Session));
            Receive<PluginUpdateDistributor.RemovePluginStatusSubRequest>(r => RemoveSession(_statusSubs, r.PluginId, r.SessionId));
        }


        public static IActorRef Create(IAlgoServerProvider server, ILogger logger)
        {
            return ActorSystem.SpawnLocal(() => new PluginUpdateDistributorActor(server, logger), nameof(PluginUpdateDistributorActor));
        }


        protected override void ActorInit(object initMsg)
        {
            var _ = DispatchLoop();
        }


        private static void AddSession(Dictionary<string, PluginSubNode> map, string pluginId, SessionHandler session)
        {
            if (!map.TryGetValue(pluginId, out var subNode))
            {
                subNode = new PluginSubNode(pluginId);
                map.Add(pluginId, subNode);
            }
            subNode.AddSession(session);
        }

        private static void RemoveSession(Dictionary<string, PluginSubNode> map, string pluginId, string sessionId)
        {
            if (!map.TryGetValue(pluginId, out var subNode))
                return;

            subNode.RemoveSession(sessionId);
            if (subNode.IsEmpty)
                map.Remove(pluginId);
        }


        private async Task DispatchLoop()
        {
            while (!StopToken.IsCancellationRequested)
            {
                try
                {
                    var t1 = Task.WhenAll(_logSubs.Values.Select(sub => DispatchLogUpdate(sub)));
                    var t2 = Task.WhenAll(_statusSubs.Values.Select(sub => DispatchStatusUpdate(sub)));

                    await Task.WhenAll(t1, t2);

                    _logSubs.Where(node => node.Value.IsEmpty).ToArray().ForEach(node => _logSubs.Remove(node.Key));
                    _statusSubs.Where(node => node.Value.IsEmpty).ToArray().ForEach(node => _statusSubs.Remove(node.Key));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Dispatch loop iteration failed");
                }

                await Task.Delay(1000, StopToken);
            }
        }

        private async Task DispatchStatusUpdate(PluginSubNode node)
        {
            var id = node.PluginId;
            try
            {
                var statusRes = await _server.GetBotStatusAsync(new Domain.ServerControl.PluginStatusRequest { PluginId = id });

                if (string.IsNullOrEmpty(statusRes.PluginId))
                {
                    node.RemoveAllSessions();
                    return;
                }

                if (node.IsEmpty || StopToken.IsCancellationRequested)
                    return;

                var status = statusRes.Status;
                if (!string.IsNullOrEmpty(status))
                {
                    var update = new PluginStatusUpdate { PluginId = id, Message = status };
                    if (TryPackUpdate(update, out var packedUpdate, true))
                        node.DispatchUpdate(packedUpdate);
                }
                else
                {
                    node.CleanupSessions();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to dispatch status update for plugin {id}");
            }
        }

        private async Task DispatchLogUpdate(PluginSubNode node)
        {
            var id = node.PluginId;
            try
            {
                var logsRes = await _server.GetBotLogsAsync(new Domain.ServerControl.PluginLogsRequest { PluginId = id, MaxCount = 100, LastLogTimeUtc = node.LastRequestTime });

                if (string.IsNullOrEmpty(logsRes.PluginId))
                {
                    node.RemoveAllSessions();
                    return;
                }

                if (node.IsEmpty || StopToken.IsCancellationRequested)
                    return;

                var logs = logsRes.Logs;
                if (logs.Count > 0)
                {
                    node.LastRequestTime = logs[logs.Count - 1].TimeUtc;
                    var update = new PluginLogUpdate { PluginId = id };
                    update.Records.AddRange(logs.Select(lr => lr.ToApi()));
                    if (TryPackUpdate(update, out var packedUpdate, true))
                        node.DispatchUpdate(packedUpdate);
                }
                else
                {
                    node.CleanupSessions();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to dispatch log update for plugin {id}");
            }
        }

        private bool TryPackUpdate(IMessage update, out UpdateInfo packedUpdate, bool compress = false)
        {
            packedUpdate = null;

            try
            {
                if (!UpdateInfo.TryPack(update, out packedUpdate, compress))
                {
                    _logger.Error($"Failed to pack msg '{update.Descriptor.Name}'");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to pack msg '{update.Descriptor.Name}'");
                return false;
            }
        }


        private class PluginSubNode
        {
            private readonly LinkedList<SessionHandler> _sessions = new LinkedList<SessionHandler>();


            public string PluginId { get; }

            public Timestamp LastRequestTime { get; set; }

            public bool IsEmpty => _sessions.Count == 0;


            public PluginSubNode(string pluginId)
            {
                PluginId = pluginId;
                LastRequestTime = new Timestamp();
            }


            public void AddSession(SessionHandler session)
            {
                _sessions.AddLast(session);
            }

            public void RemoveSession(string sessionId)
            {
                var node = _sessions.First;
                while (node != null)
                {
                    if (node.Value.Id == sessionId)
                        break;

                    node = node.Next;
                }

                if (node != null)
                {
                    _sessions.Remove(node);
                }
            }

            public void DispatchUpdate(UpdateInfo update)
            {
                var node = _sessions.First;
                while (node != null)
                {
                    var session = node.Value;
                    var nextNode = node.Next;
                    if (!session.TryWrite(update))
                        _sessions.Remove(node);

                    node = nextNode;
                }
            }

            public void CleanupSessions()
            {
                var node = _sessions.First;
                while (node != null)
                {
                    var session = node.Value;
                    var nextNode = node.Next;
                    if (session.IsClosed)
                        _sessions.Remove(node);

                    node = nextNode;
                }
            }

            public void RemoveAllSessions() => _sessions.Clear();
        }
    }
}
