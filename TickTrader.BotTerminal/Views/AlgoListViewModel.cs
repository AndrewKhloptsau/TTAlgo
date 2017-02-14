﻿using Caliburn.Micro;
using Machinarium.Qnil;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Core.Metadata;

namespace TickTrader.BotTerminal
{
    internal class AlgoListViewModel : PropertyChangedBase
    {
        public IObservableListSource<AlgoItemViewModel> Plugins { get; private set; }

        public AlgoListViewModel(PluginCatalog catalog)
        {
            Plugins = catalog.AllPlugins
                .Where((k, p) => !string.IsNullOrEmpty(k.FileName))
                .Select((k, p) => new AlgoItemViewModel(p))
                .OrderBy((k, p) => p.Name)
                .AsObservable();
        }
    }

    public class AlgoItemViewModel
    {
        public AlgoItemViewModel(PluginCatalogItem item)
        {
            PluginItem = item;
            Name = item.DisplayName;
            var type = item.Ref.Descriptor.AlgoLogicType;
            if (type == Algo.Core.Metadata.AlgoTypes.Indicator)
                Group = "Indicators";
            else if (type == Algo.Core.Metadata.AlgoTypes.Robot)
                Group = "Bot Traders";
            else
                Group = "Unknown type";
        }

        public PluginCatalogItem PluginItem { get; private set; }
        public string Name { get; private set; }
        public string Group { get; private set; }
    }
}