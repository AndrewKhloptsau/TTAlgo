﻿using System;
using System.Collections.Generic;
using System.Linq;
using TickTrader.Algo.Api;
using TickTrader.Algo.Common.Info;
using TickTrader.Algo.Common.Model.Setup;
using TickTrader.Algo.Core.Metadata;
using TickTrader.Algo.Core.Repository;

namespace TickTrader.BotAgent.BA.Models
{
    public class IdProviderStub : IPluginIdProvider
    {
        public string GeneratePluginId(PluginDescriptor descriptor)
        {
            return descriptor.DisplayName;
        }

        public bool IsValidPluginId(AlgoTypes pluginType, string pluginId)
        {
            return true;
        }

        public void RegisterPluginId(string pluginId)
        {
            return;
        }
    }


    public class StubSymbolInfo : ISetupSymbolInfo
    {
        public string Name { get; set; }

        public SymbolOrigin Origin { get; set; }

        public string Id => Name;


        public StubSymbolInfo(SymbolKey symbolInfo)
        {
            Name = symbolInfo.Name;
            Origin = SymbolOrigin.Online;
        }
    }


    // stub
    public class SetupMetadata : IAlgoSetupMetadata
    {
        public IReadOnlyList<ISetupSymbolInfo> Symbols { get; }

        public MappingCollection Mappings { get; }

        public IPluginIdProvider IdProvider { get; }

        public SetupMetadata(IEnumerable<SymbolKey> symbols)
        {
            //Extentions = new ExtCollection();
            Symbols = symbols.Select(s => new StubSymbolInfo(s)).ToList();
            Mappings = new MappingCollection(null);
            IdProvider = new IdProviderStub();
        }
    }


    // stub
    public class SetupContext : IAlgoSetupContext
    {
        private static MappingKey _defaultMapping = new MappingKey(MappingCollection.BidBarReduction, MappingCollection.DefaultBarToDoubleReduction);
        private static SymbolToken _defaultSymbol = new SymbolToken("none");


        public TimeFrames DefaultTimeFrame => TimeFrames.M1;

        public ISetupSymbolInfo DefaultSymbol => _defaultSymbol;

        public MappingKey DefaultMapping => _defaultMapping;
    }
}
