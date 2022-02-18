﻿using ProtoBuf;
using System;
using TickTrader.Algo.Domain;

namespace TickTrader.FeedStorage
{
    [ProtoContract]
    internal sealed class CustomInfo : ISymbolInfo
    {
        [ProtoIgnore]
        internal Guid StorageId { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public string Description { get; set; }

        [ProtoMember(4)]
        public string MarginCurrency { get; set; }

        [ProtoMember(5)]
        public string ProfitCurrency { get; set; }

        [ProtoMember(6)]
        public int Digits { get; set; }

        [ProtoMember(7)]
        public double LotSize { get; set; }

        [ProtoMember(8)]
        public int Slippage { get; set; }



        [ProtoMember(9)]
        public double MinVolume { get; set; }

        [ProtoMember(10)]
        public double MaxVolume { get; set; }

        [ProtoMember(11)]
        public double VolumeStep { get; set; }


        [ProtoMember(12)]
        public bool SwapEnabled { get; set; }

        [ProtoMember(13)]
        public SwapInfo.Types.Type SwapType { get; set; }

        [ProtoMember(14)]
        public double SwapSizeShort { get; set; }

        [ProtoMember(15)]
        public double SwapSizeLong { get; set; }

        [ProtoMember(16)]
        public int TripleSwapDay { get; set; }


        [ProtoMember(17)]
        public MarginInfo.Types.CalculationMode MarginMode { get; set; }

        [ProtoMember(18)]
        public double MarginHedged { get; set; }

        [ProtoMember(19)]
        public double MarginFactor { get; set; }

        [ProtoMember(20)]
        public double StopOrderMarginReduction { get; set; }

        [ProtoMember(21)]
        public double HiddenLimitOrderMarginReduction { get; set; }


        [ProtoMember(22)]
        public double Commission { get; set; }

        [ProtoMember(23)]
        public CommissonInfo.Types.ValueType CommissionType { get; set; }

        [ProtoMember(24)]
        public double LimitsCommission { get; set; }

        [ProtoMember(25)]
        public double MinCommission { get; set; }

        [ProtoMember(26)]
        public string MinCommissionCurr { get; set; }


        int IBaseSymbolInfo.SortOrder => 1;

        int IBaseSymbolInfo.GroupSortOrder => 1;

        string ISymbolInfo.Security => string.Empty;

        public bool TradeAllowed => true;


        public static CustomInfo ToData(ISymbolInfo info)
        {
            return new CustomInfo
            {
                Name = info.Name,
                MarginCurrency = info.MarginCurrency,
                ProfitCurrency = info.ProfitCurrency,
                Digits = info.Digits,
                LotSize = info.LotSize,
                MinVolume = info.MinVolume,
                MaxVolume = info.MaxVolume,
                VolumeStep = info.VolumeStep,
                Slippage = info.Slippage,
                Description = info.Description,

                Commission = info.Commission,
                LimitsCommission = info.LimitsCommission,
                CommissionType = info.CommissionType,
                MinCommission = info.MinCommission,
                MinCommissionCurr = info.MinCommissionCurr,

                SwapEnabled = info.SwapEnabled,
                SwapType = info.SwapType,
                SwapSizeLong = info.SwapSizeLong,
                SwapSizeShort = info.SwapSizeShort,
                TripleSwapDay = info.TripleSwapDay,

                MarginMode = info.MarginMode,
                MarginHedged = info.MarginHedged,
                MarginFactor = info.MarginFactor,
                StopOrderMarginReduction = info.StopOrderMarginReduction,
                HiddenLimitOrderMarginReduction = info.HiddenLimitOrderMarginReduction,
            };
        }
    }
}