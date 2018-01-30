﻿using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Api;
using TickTrader.Algo.Common;
using TickTrader.Algo.Common.Model;

namespace TickTrader.BotTerminal
{
    class OrderViewModel : PropertyChangedBase, IDisposable
    {
        private SymbolModel symbol;

        public OrderViewModel(OrderModel order, SymbolModel symbol)
        {
            this.symbol = symbol;

            Order = order;
            PriceDigits = symbol?.PriceDigits ?? 5;
            ProfitDigits = symbol?.QuoteCurrencyDigits ?? 2;
        }

        public OrderModel Order { get; private set; }
        public int PriceDigits { get; private set; }
        public int ProfitDigits { get; private set; }
        public decimal? Price => Order.OrderType == OrderType.StopLimit || Order.OrderType == OrderType.Stop ? Order.StopPrice : Order.LimitPrice;

        public RateDirectionTracker CurrentPrice => Order.OrderType != OrderType.Position ?
                                                    Order.Side == OrderSide.Buy ? symbol?.AskTracker : symbol?.BidTracker :
                                                    Order.Side == OrderSide.Buy ? symbol?.BidTracker : symbol?.AskTracker;

        public void Dispose()
        {
        }
    }
}
