﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Core.Calc.Conversion;
using TickTrader.BusinessObjects;

namespace TickTrader.Algo.Core.Calc
{
    public sealed class OrderCalculator
    {
        //private readonly ConversionManager conversionMap;
        private readonly Converter<int, int> _leverageProvider;

        internal OrderCalculator(SymbolMarketNode tracker, ConversionManager conversion, string accountCurrency)
        {
            RateTracker = tracker;

            PositiveProfitConversionRate = conversion.GetPositiveProfitFormula(tracker, accountCurrency);
            NegativeProfitConversionRate = conversion.GetNegativeProfitFormula(tracker, accountCurrency);
            MarginConversionRate = conversion.GetMarginFormula(tracker, accountCurrency);
            SymbolInfo = tracker.SymbolInfo;

            //if (this.SymbolInfo == null)
            //    throw new SymbolConfigException("Cannot find configuration for symbol " + this.symbol + ".");

            //if (this.SymbolInfo.ProfitCurrency == null && this.SymbolInfo.MarginCurrency == null)
            //    throw new SymbolConfigException("Currency configuration is missing for symbol " + this.SymbolInfo.Symbol + ".");

            if (this.SymbolInfo != null && SymbolInfo.MarginMode != MarginCalculationModes.Forex && SymbolInfo.MarginMode != MarginCalculationModes.CFD_Leverage)
                _leverageProvider = _ => 1;
            else
                _leverageProvider = n => n;

            InitMarginFactorCache();
        }

        public Api.RateUpdate CurrentRate => RateTracker.Rate;
        public SymbolAccessor SymbolInfo { get; }
        internal IConversionFormula PositiveProfitConversionRate { get; private set; }
        internal IConversionFormula NegativeProfitConversionRate { get; private set; }
        internal IConversionFormula MarginConversionRate { get; private set; }

        internal SymbolMarketNode RateTracker { get; }

        //public void Dispose()
        //{
        //}

        #region Margin

        private double _baseMarginFactor;
        private double _stopMarginFactor;
        private double _hiddenMarginFactor;

        public double CalculateMargin(IOrderCalcInfo order, int leverage, out CalcErrorCodes error)
        {
            return CalculateMargin(order.RemainingAmount, leverage, order.Type, order.Side, order.IsHidden, out error);
        }

        public double CalculateMargin(double orderVolume, int leverage, OrderTypes ordType, OrderSides side, bool isHidden, out CalcErrorCodes error)
        {
            error = MarginConversionRate.ErrorCode;

            if (error != CalcErrorCodes.None)
                return 0;

            double lFactor = _leverageProvider(leverage);
            double marginFactor = GetMarginFactor(ordType, isHidden);
            double marginRaw = orderVolume * marginFactor / lFactor;

            return marginRaw * MarginConversionRate.Value;
        }

        private double GetMarginFactor(OrderTypes ordType, bool isHidden)
        {
            if (ordType == OrderTypes.Stop || ordType == OrderTypes.StopLimit)
                return _stopMarginFactor;
            if (ordType == OrderTypes.Limit && isHidden)
                return _hiddenMarginFactor;
            return _baseMarginFactor;
        }

        private void InitMarginFactorCache()
        {
            _baseMarginFactor = SymbolInfo.MarginFactorFractional;
            _stopMarginFactor = _baseMarginFactor * SymbolInfo.StopOrderMarginReduction;
            _hiddenMarginFactor = _baseMarginFactor * SymbolInfo.HiddenLimitOrderMarginReduction;
        }

        #endregion

        #region Profit

        public double CalculateProfit(IOrderCalcInfo order, double amount, out double closePrice, out CalcErrorCodes error)
        {
            return CalculateProfit(order.Price.Value, amount, order.Side, out closePrice, out error);
        }

        public double CalculateProfit(IOrderCalcInfo order, out CalcErrorCodes error)
        {
            return CalculateProfit(order.Price.Value, order.RemainingAmount, order.Side, out error);
        }

        public double CalculateProfit(IOrderCalcInfo order, out double closePrice, out CalcErrorCodes error)
        {
            return CalculateProfit(order.Price.Value, order.RemainingAmount, order.Side, out closePrice, out error);
        }

        public double CalculateProfit(double openPrice, double volume, OrderSides side, out CalcErrorCodes error)
        {
            double closePrice;
            return CalculateProfit(openPrice, volume, side, out closePrice, out error);
        }

        public double CalculateProfit(double openPrice, double volume, OrderSides side, out double closePrice, out CalcErrorCodes error)
        {
            if (side == OrderSides.Buy)
            {
                if (!GetBid(out closePrice, out error))
                    return 0;
            }
            else
            {
                if (!GetAsk(out closePrice, out error))
                    return 0;
            }

            double conversionRate;
            return CalculateProfitInternal(openPrice, closePrice, volume, side, out conversionRate, out error);
        }

        public double CalculateProfitFixedPrice(IOrderCalcInfo order, double amount, double closePrice, out CalcErrorCodes error)
        {
            return CalculateProfitInternal(order.Price.Value, closePrice, amount, order.Side, out _, out error);
        }

        public double CalculateProfitFixedPrice(double openPrice, double volume, double closePrice, OrderSides side, out CalcErrorCodes error)
        {
            return CalculateProfitFixedPrice(openPrice, closePrice, volume, side, out _, out error);
        }

        public double CalculateProfitFixedPrice(double openPrice, double volume, double closePrice, OrderSides side, out double conversionRate, out CalcErrorCodes error)
        {
            return CalculateProfitInternal(openPrice, closePrice, volume, side, out conversionRate, out error);
        }

        private double CalculateProfitInternal(double openPrice, double closePrice, double volume, OrderSides side, out double conversionRate, out CalcErrorCodes error)
        {
            //this.VerifyInitialized();

            double nonConvProfit;

            if (side == OrderSides.Buy)
                nonConvProfit = (closePrice - openPrice) * volume;
            else
                nonConvProfit = (openPrice - closePrice) * volume;

            return ConvertProfitToAccountCurrency(nonConvProfit, out conversionRate, out error);
        }

        public double ConvertMarginToAccountCurrency(double margin, out CalcErrorCodes error)
        {
            error = MarginConversionRate.ErrorCode;
            if (error == CalcErrorCodes.None)
                return margin * MarginConversionRate.Value;
            return 0;
        }

        public double ConvertProfitToAccountCurrency(double profit, out CalcErrorCodes error)
        {
            return ConvertProfitToAccountCurrency(profit, out _, out error);
        }

        public double ConvertProfitToAccountCurrency(double profit, out double conversionRate, out CalcErrorCodes error)
        {
            if (profit >= 0)
            {
                error = PositiveProfitConversionRate.ErrorCode;
                conversionRate = PositiveProfitConversionRate.Value;
            }
            else
            {
                error = NegativeProfitConversionRate.ErrorCode;
                conversionRate = NegativeProfitConversionRate.Value;
            }

            if (error == CalcErrorCodes.None)
                return profit * conversionRate;
            return 0;
        }

        #endregion

        #region Commission

        public double CalculateCommission(double amount, double cValue, CommissionValueType vType, CommissionChargeType chType, out CalcErrorCodes error)
        {
            error = CalcErrorCodes.None;

            if (cValue == 0)
                return 0;

            //UL: all calculation for CommissionChargeType.PerLot
            if (vType == CommissionValueType.Money)
            {
                //if (chType == CommissionChargeType.PerDeal)
                //    return -cValue;
                //else if (chType == CommissionChargeType.PerLot)
                return -(amount / SymbolInfo.ContractSizeFractional * cValue);
            }
            else if (vType == CommissionValueType.Percentage)
            {
                //if (chType == CommissionChargeType.PerDeal || chType == CommissionChargeType.PerLot)
                error = MarginConversionRate.ErrorCode;
                if (error != CalcErrorCodes.None)
                    return 0;
                return -(amount * cValue * MarginConversionRate.Value) / 100;
            }
            else if (vType == CommissionValueType.Points)
            {
                double ptValue = cValue / Math.Pow(10, SymbolInfo.Precision);

                //if (chType == CommissionChargeType.PerDeal)
                //    return - (ptValue * MarginConversionRate.Value);
                //else if (chType == CommissionChargeType.PerLot)
                error = MarginConversionRate.ErrorCode;
                if (error != CalcErrorCodes.None)
                    return 0;
                return ConvertProfitToAccountCurrency(-(amount * ptValue * MarginConversionRate.Value), out _, out error);
            }

            throw new Exception("Invalid comission configuration: chType=" + chType + " vType= " + vType);
        }

        #endregion Commission

        #region Swap

        public double CalculateSwap(double amount, OrderSides side, DateTime now, out CalcErrorCodes error)
        {
            error = CalcErrorCodes.None;

            double swapAmount = GetSwapModifier(side) * amount;
            double swap = 0;

            if (SymbolInfo.SwapType == SwapType.Points)
                swap = ConvertProfitToAccountCurrency(swapAmount, out error);
            else if (SymbolInfo.SwapType == SwapType.PercentPerYear)
                swap = ConvertMarginToAccountCurrency(swapAmount, out error);

            if (SymbolInfo.TripleSwapDay > 0)
            {
                //var now = DateTime.UtcNow;
                DayOfWeek swapDayOfWeek = now.DayOfWeek == DayOfWeek.Sunday ? DayOfWeek.Saturday : (int)now.DayOfWeek - DayOfWeek.Monday;
                if (SymbolInfo.TripleSwapDay == (int)swapDayOfWeek)
                    swap *= 3;
                else if (swapDayOfWeek == DayOfWeek.Saturday || swapDayOfWeek == DayOfWeek.Sunday)
                    swap = 0;
            }

            return swap;
        }

        private double GetSwapModifier(OrderSides side)
        {
            if (SymbolInfo.SwapEnabled)
            {
                if (SymbolInfo.SwapType == SwapType.Points)
                {
                    if (side == OrderSides.Buy)
                        return SymbolInfo.SwapSizeLong / Math.Pow(10, SymbolInfo.Precision);
                    if (side == OrderSides.Sell)
                        return SymbolInfo.SwapSizeShort / Math.Pow(10, SymbolInfo.Precision);
                }
                else if (SymbolInfo.SwapType == SwapType.PercentPerYear)
                {
                    const double power = 1.0 / 365.0;
                    double factor = 0.0;
                    if (side == OrderSides.Buy)
                        factor = Math.Sign(SymbolInfo.SwapSizeLong) * (Math.Pow(1 + Math.Abs(SymbolInfo.SwapSizeLong), power) - 1);
                    if (side == OrderSides.Sell)
                        factor = Math.Sign(SymbolInfo.SwapSizeShort) * (Math.Pow(1 + Math.Abs(SymbolInfo.SwapSizeShort), power) - 1);

                    //if (double.IsInfinity(factor) || double.IsNaN(factor))
                    //    throw new MarketConfigurationException($"Can not calculate swap: side={side} symbol={SymbolInfo.Symbol} swaptype={SymbolInfo.SwapType} sizelong={SymbolInfo.SwapSizeLong} sizeshort={SymbolInfo.SwapSizeShort}");

                    return factor;
                }
            }

            return 0;
        }

        #endregion

        private bool GetBid(out double bid, out CalcErrorCodes error)
        {
            var rate = RateTracker.Rate;

            if (!rate.HasBid)
            {
                error = CalcErrorCodes.OffQuote;
                bid = 0;
                return false;
            }

            error = CalcErrorCodes.None;
            bid = rate.Bid;
            return true;
        }

        private bool GetAsk(out double ask, out CalcErrorCodes error)
        {
            var rate = RateTracker.Rate;

            if (!rate.HasAsk)
            {
                error = CalcErrorCodes.OffQuote;
                ask = 0;
                return false;
            }

            error = CalcErrorCodes.None;
            ask = rate.Ask;
            return true;
        }

        #region Usage Management

        internal int UsageCount { get; private set; }

        public UsageToken UsageScope()
        {
            return new UsageToken(this);
        }

        internal void AddUsage()
        {
            if (UsageCount == 0)
                Attach();
            UsageCount++;
        }

        internal void RemoveUsage()
        {
            UsageCount--;
            if (UsageCount == 0)
                Deattach();
        }

        private void Attach()
        {
            PositiveProfitConversionRate.AddUsage();
            NegativeProfitConversionRate.AddUsage();
            MarginConversionRate.AddUsage();
        }

        private void Deattach()
        {
            PositiveProfitConversionRate.RemoveUsage();
            NegativeProfitConversionRate.RemoveUsage();
            MarginConversionRate.RemoveUsage();
        }

        public struct UsageToken : IDisposable
        {
            public UsageToken(OrderCalculator calc)
            {
                Calculator = calc;
                calc.AddUsage();
            }

            public OrderCalculator Calculator { get; }

            public void Dispose()
            {
                Calculator.RemoveUsage();
            }
        }

        #endregion
    }
}