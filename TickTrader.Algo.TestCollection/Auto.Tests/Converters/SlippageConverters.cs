﻿using System;
using TickTrader.Algo.Api;

namespace TickTrader.Algo.TestCollection.Auto.Tests
{
    public static class SlippageConverters
    {
        public static double SlippagePipsToFractions(double slippage, double price, Symbol symbol)
        {
            switch (symbol.SlippageType)
            {
                case SlippageType.Percent:
                    return slippage;
                case SlippageType.Pips:
                    return SlippagePipsToPercent(slippage, price, symbol) / 100;
                default:
                    throw new ArgumentException("Unsupported");
            }
        }

        public static double SlippagePipsToPercent(double slippage, double price, Symbol symbol)
        {
            switch (symbol.SlippageType)
            {
                case SlippageType.Percent:
                    return slippage;
                case SlippageType.Pips:
                    return slippage * Math.Pow(10, -symbol.Digits) * 100 / price;
                default:
                    throw new ArgumentException("Unsupported");
            }
        }
    }
}
