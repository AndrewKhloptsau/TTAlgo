﻿using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Machinarium.Qnil;
using Machinarium.Var;
using TickTrader.Algo.Common.Model;

namespace TickTrader.BotTerminal
{
    internal class SymbolListViewModel : EntityBase
    {
        private IVarList<SymbolViewModel> viewModelCollection;

        public SymbolListViewModel(IVarSet<string, SymbolModel> symbolCollection, QuoteDistributor distributor, IShell shell)
        {
            viewModelCollection = symbolCollection.Select((k, v) => new SymbolViewModel((SymbolModel)v, distributor, shell)).OrderBy((k, v) => k);

            Symbols = viewModelCollection.AsObservable();
            SelectedSymbol = AddProperty<SymbolViewModel>();

            TriggerOnChange(SelectedSymbol.Var, a =>
            {
                if (a.Old != null) a.Old.IsSelected = false;
                if (a.New != null) a.New.IsSelected = true;
            });
        }

        public IObservableList<SymbolViewModel> Symbols { get; }
        public Property<SymbolViewModel> SelectedSymbol { get; }
    }
}
