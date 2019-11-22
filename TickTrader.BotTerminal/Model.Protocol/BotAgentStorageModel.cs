﻿using Machinarium.Qnil;
using System.Linq;
using System.Runtime.Serialization;
using TickTrader.Algo.Common.Lib;

namespace TickTrader.BotTerminal
{
    [DataContract(Namespace = "")]
    internal class BotAgentStorageModel : StorageModelBase<BotAgentStorageModel>
    {
        [DataMember(Name = "BotAgents")]
        private VarList<BotAgentStorageEntry> _botAgents;


        public VarList<BotAgentStorageEntry> BotAgents => _botAgents;


        public BotAgentStorageModel()
        {
            _botAgents = new VarList<BotAgentStorageEntry>();
        }


        public override BotAgentStorageModel Clone()
        {
            return new BotAgentStorageModel()
            {
                _botAgents = new VarList<BotAgentStorageEntry>(_botAgents.Values.Select(b => b.Clone())),
            };
        }


        public void Remove(string server)
        {
            var index = _botAgents.Values.IndexOf(b => b.ServerAddress == server);
            if (index != -1)
            {
                _botAgents.RemoveAt(index);
            }
        }

        public BotAgentStorageEntry Update(string login, string password, string server, int port, string displayName)
        {
            var index = _botAgents.Values.IndexOf(b => b.ServerAddress == server);
            if (index == -1)
            {
                _botAgents.Add(new BotAgentStorageEntry { ServerAddress = server });
                index = _botAgents.Count - 1;
            }
            var botAgent = _botAgents[index];
            botAgent.Login = login;
            botAgent.Password = password;
            botAgent.Port = port;
            botAgent.DisplayName = displayName;

            return botAgent;
        }

        public void UpdateConnect(string server, bool connect)
        {
            var index = _botAgents.Values.IndexOf(b => b.ServerAddress == server);
            if (index != -1)
            {
                _botAgents[index].Connect = connect;
            }
        }
    }
}
