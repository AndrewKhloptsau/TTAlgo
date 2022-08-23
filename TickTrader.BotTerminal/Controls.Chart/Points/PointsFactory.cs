﻿using System;
using TickTrader.Algo.Domain;

namespace TickTrader.BotTerminal.Controls.Chart
{
    internal static class TradePointsFactory
    {
        internal static EventPoint GetOpenMarker(BaseTransactionModel report)
        {
            var price = report.OpenPrice;

            return new EventPoint
            {
                Price = price,
                Time = report.OpenTime.Ticks,
                ToolTip = $"Open #{report.OrderNum} {report.Side} {report.OpenQuantity} at price {price?.ToString(GetDoubleFormat(report))}"
            };
        }

        internal static EventPoint GetCloseMarker(BaseTransactionModel report)
        {
            var side = report.Side == BaseTransactionModel.TransactionSide.Sell ? "Buy" : "Sell";

            return new EventPoint
            {
                Price = report.ClosePrice,
                Time = report.CloseTime.Ticks,
                ToolTip = $"Close #{report.OrderNum} {side} {report.CloseQuantity} at price {report.ClosePrice?.ToString(GetDoubleFormat(report))}"
            };
        }

        internal static EventPoint GetFillMarker(BaseTransactionModel report)
        {
            var price = report.OpenPrice;

            return new EventPoint
            {
                Price = price,
                Time = report.OpenTime.Ticks,
                ToolTip = $"Fill #{report.OrderNum} {report.Side} {report.OpenQuantity} at price {price?.ToString(GetDoubleFormat(report))}"
            };
        }


        private static string GetDoubleFormat(BaseTransactionModel model) => $"F{model.PriceDigits}";
    }


    internal static class IndicatorPointsFactory
    {
        internal static IndicatorPoint GetDefaultPoint(OutputPoint point)
        {
            return new IndicatorPoint
            {
                Value = double.IsNaN(point.Value) ? 0.0 : point.Value,
                Time = new DateTime(point.Time.Value),
            };
        }
    }
}