﻿using System;

namespace TickTrader.Algo.Domain
{
    public partial class BarData
    {
        public BarData(long openTime, long closeTime, double price, double realVolume)
            : this(openTime, closeTime, price, realVolume, 1)
        {
        }

        public BarData(long openTime, long closeTime, double price, double realVolume, long tickVolume)
        {
            OpenTime = openTime;
            CloseTime = closeTime;
            Open = High = Close = Low = price;
            RealVolume = realVolume;
            TickVolume = tickVolume;
        }


        public static BarData CreateEmpty()
        {
            return new BarData() { Open = double.NaN, Close = double.NaN, High = double.NaN, Low = double.NaN, RealVolume = double.NaN, TickVolume = 0 };
        }

        public static BarData CreateBlank(long openTime, long closeTime)
        {
            return new BarData(openTime, closeTime, 0.0, 0.0, 0);
        }


        public void Init(double price, double realVolume = 0.0)
        {
            Open = High = Close = Low = price;
            RealVolume = realVolume;
            TickVolume = 1;
        }

        public void Append(double price, double realVolume = 0.0)
        {
            Close = price;
            High = Math.Max(High, price);
            Low = Math.Min(Low, price);
            RealVolume += realVolume;
            TickVolume++;
        }

        public void AppendPart(BarData barPart)
        {
            Close = barPart.Close;
            High = Math.Max(High, barPart.High);
            Low = Math.Min(Low, barPart.Low);
            RealVolume += barPart.RealVolume;
            TickVolume += barPart.TickVolume;
        }
    }


    public partial class BarInfo
    {
        public BarInfo(string symbol, Feed.Types.MarketSide marketSide, Feed.Types.Timeframe timeframe, BarData data)
        {
            Symbol = symbol;
            MarketSide = marketSide;
            Timeframe = timeframe;
            Data = data;
        }


        public static BarInfo BidBar(string symbol, Feed.Types.Timeframe timeframe, BarData data)
        {
            return new BarInfo(symbol, Feed.Types.MarketSide.Bid, timeframe, data);
        }

        public static BarInfo AskBar(string symbol, Feed.Types.Timeframe timeframe, BarData data)
        {
            return new BarInfo(symbol, Feed.Types.MarketSide.Ask, timeframe, data);
        }
    }
}
