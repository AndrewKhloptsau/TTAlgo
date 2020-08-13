﻿using System;
using TickTrader.Algo.Api.Ext;
using TickTrader.Algo.Core.Metadata;
using TickTrader.Algo.Domain;

namespace TickTrader.Algo.Core.Repository
{
    [Serializable]
    public class AskBarMapping : Mapping
    {
        internal AskBarMapping()
        {
            Key = new MappingKey(MappingCollection.AskBarReduction);
            DisplayName = "Ask";
        }


        public override void MapInput(IPluginSetupTarget target, string inputName, string symbol)
        {
            target.GetFeedStrategy<BarStrategy>().MapInput(inputName, symbol, Feed.Types.MarketSide.Ask);
        }
    }


    [Serializable]
    public class AskBarToDoubleMapping : Mapping
    {
        internal AskBarToDoubleMapping()
        {
            Key = new MappingKey(MappingCollection.AskBarReduction, MappingCollection.DefaultBarToDoubleReduction);
            DisplayName = "Ask.Close";
        }

        internal AskBarToDoubleMapping(ReductionKey doubleReductionKey, string doubleReductionDisplayName)
        {
            Key = new MappingKey(MappingCollection.AskBarReduction, doubleReductionKey);
            DisplayName = $"Ask.{doubleReductionDisplayName}";
        }


        public override void MapInput(IPluginSetupTarget target, string inputName, string symbol)
        {
            var doubleReduction = AlgoAssemblyInspector.GetReduction(Key.SecondaryReduction.DescriptorId);
            var doubleReductionInstance = doubleReduction?.CreateInstance<BarToDoubleReduction>() ?? new BarToCloseReduction();
            target.GetFeedStrategy<BarStrategy>().MapInput(inputName, symbol, Feed.Types.MarketSide.Ask, bar => doubleReductionInstance.Reduce(new BarEntity(bar)));
        }
    }
}
