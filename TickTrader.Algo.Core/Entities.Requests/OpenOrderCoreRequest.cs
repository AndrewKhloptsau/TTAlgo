﻿using System;
using TickTrader.Algo.Api;

namespace TickTrader.Algo.Core
{
    [Serializable]
    public class OpenOrderCoreRequest : OrderCoreRequest
    {
        public string Symbol { get; set; }
        public Domain.OrderInfo.Types.Type Type { get; set; }
        public Domain.OrderInfo.Types.Side Side { get; set; }
        public double? Price { get; set; }
        public double? StopPrice { get; set; }
        public double Volume { get; set; }
        public double VolumeLots { get; set; }
        public double? MaxVisibleVolume { get; set; }
        public double? MaxVisibleVolumeLots { get; set; }
        public double? StopLoss { get; set; }
        public double? TakeProfit { get; set; }
        public DateTime? Expiration { get; set; }
        public double? Slippage { get; set; }
        public string Comment { get; set; }
        public OrderExecOptions Options { get; set; }
        public string Tag { get; set; }

        internal bool IsOptionSet(OrderExecOptions option)
        {
            return Options.HasFlag(option);
        }
    }
}
