﻿using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TickTrader.BotTerminal
{
    class TradeInfoViewModel: PropertyChangedBase
    {
        public TradeInfoViewModel(TraderClientModel clientModel)
        {
            var netPositions = new NetPositionListViewModel(clientModel.Account, clientModel.Symbols);
            var grossPositions = new GrossPositionListViewModel(clientModel.Account, clientModel.Symbols);
            Positions = new PositionListViewModel(netPositions, grossPositions);
            Orders = new OrderListViewModel(clientModel.Account, clientModel.Symbols);
            Assets = new AssetsViewModel(clientModel.Account, clientModel.Currencies);
            AccountStats = new AccountStatsViewModel(clientModel);
        }

        public OrderListViewModel Orders { get; }
        public AssetsViewModel Assets { get; }
        public PositionListViewModel Positions { get; }
        public AccountStatsViewModel AccountStats { get; }
    }
}