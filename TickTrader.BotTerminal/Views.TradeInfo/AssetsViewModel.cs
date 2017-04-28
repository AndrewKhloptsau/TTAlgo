﻿using Caliburn.Micro;
using SoftFX.Extended;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Machinarium.Qnil;

namespace TickTrader.BotTerminal
{
    class AssetsViewModel : AccountBasedViewModel
    {
        public AssetsViewModel(AccountModel model, IDictionary<string, CurrencyInfo> currencies) : base(model)
        {
            Assets = model.Assets
                .OrderBy((c, a) => c)
                .Select(a => new AssetViewModel(a, currencies.GetOrDefault(a.Currency)))
                .AsObservable();
        }

        protected override bool SupportsAccount(AccountType accType)
        {
            return accType == AccountType.Cash;
        }

        public IObservableListSource<AssetViewModel> Assets { get; private set; }
    }
}
