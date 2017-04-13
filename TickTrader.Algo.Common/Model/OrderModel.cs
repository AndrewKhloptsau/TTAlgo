﻿using SoftFX.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Common.Lib;
using TickTrader.Algo.Core;
using TickTrader.BusinessLogic;
using TickTrader.BusinessObjects;
using Fdk = SoftFX.Extended;

namespace TickTrader.Algo.Common.Model
{
    public class OrderModel : ObservableObject, TickTrader.BusinessLogic.IOrderModel
    {
        private string clientOrderId;
        private TradeRecordType orderType;
        private decimal amount;
        private decimal amountRemaining;
        public TradeRecordSide side;
        private decimal? price;
        private decimal? swap;
        private decimal? commission;
        private DateTime? created;
        private DateTime? expiration;
        private string comment;
        private string tag;
        private double? stopLoss;
        private double? takeProfit;
        private decimal? profit;
        private decimal? margin;
        private decimal? currentPrice;
        private SymbolModel symbolModel;
        private OrderError error;
        private double? execPrice;
        private double? execAmount;
        private double? lastFillPrice;
        private double? lastFillAmount;

        public OrderModel(TradeRecord record, IOrderDependenciesResolver resolver)
        {
            this.Id = record.OrderId;
            this.clientOrderId = record.ClientOrderId;
            this.OrderId = long.Parse(Id);
            this.Symbol = record.Symbol;
            this.symbolModel = resolver.GetSymbolOrNull(record.Symbol);
            Update(record);
        }

        public OrderModel(ExecutionReport report, IOrderDependenciesResolver resolver)
        {
            this.Id = report.OrderId;
            this.clientOrderId = report.ClientOrderId;
            this.OrderId = long.Parse(Id);
            this.Symbol = report.Symbol;
            this.symbolModel = resolver.GetSymbolOrNull(report.Symbol);
            Update(report);
        }

        event Action<IOrderModel> IOrderModel.EssentialParametersChanged { add { } remove { } }

        #region Order Properties

        public string Id { get; private set; }
        public long OrderId { get; private set; }
        public string Symbol { get; private set; }
        public decimal Amount
        {
            get { return amount; }
            set
            {
                if (this.amount != value)
                {
                    this.amount = value;
                    this.AmountLots = AmountToLots(value);
                    NotifyOfPropertyChange(nameof(Amount));
                    NotifyOfPropertyChange(nameof(AmountLots));
                }
            }
        }
        public decimal? AmountLots { get; private set; } = 0;
        public decimal RemainingAmount
        {
            get { return amountRemaining; }
            set
            {
                if (this.amountRemaining != value)
                {
                    this.amountRemaining = value;
                    this.RemainingAmountLots = AmountToLots(value);
                    NotifyOfPropertyChange(nameof(RemainingAmount));
                    NotifyOfPropertyChange(nameof(RemainingAmountLots));
                }
            }
        }
        public decimal? RemainingAmountLots { get; private set; } = 0;
        public TradeRecordType OrderType
        {
            get { return orderType; }
            private set
            {
                if (orderType != value)
                {
                    this.orderType = value;
                    NotifyOfPropertyChange(nameof(OrderType));
                }
            }
        }
        public TradeRecordSide Side
        {
            get { return side; }
            private set
            {
                if (side != value)
                {
                    side = value;
                    NotifyOfPropertyChange(nameof(Side));
                }
            }
        }
        public decimal? Price
        {
            get { return price; }
            set
            {
                if (price != value)
                {
                    price = value;
                    NotifyOfPropertyChange(nameof(Price));
                }
            }
        }
        public decimal? Swap
        {
            get { return swap; }
            private set
            {
                if (swap != value)
                {
                    swap = value;
                    NotifyOfPropertyChange(nameof(Swap));
                }
            }
        }
        public decimal? Commission
        {
            get { return commission; }
            private set
            {
                if (commission != value)
                {
                    commission = value;
                    NotifyOfPropertyChange(nameof(Commission));
                }
            }
        }
        public DateTime? Created
        {
            get { return created; }
            private set
            {
                if (created != value)
                {
                    created = value;
                    NotifyOfPropertyChange(nameof(Created));
                }
            }
        }
        public DateTime? Expiration
        {
            get { return expiration; }
            private set
            {
                if (expiration != value)
                {
                    expiration = value;
                    NotifyOfPropertyChange(nameof(Expiration));
                }
            }
        }
        public string Comment
        {
            get { return comment; }
            private set
            {
                if (comment != value)
                {
                    comment = value;
                    NotifyOfPropertyChange(nameof(Comment));
                }
            }
        }
        public string Tag
        {
            get { return tag; }
            private set
            {
                if (tag != value)
                {
                    tag = value;
                    NotifyOfPropertyChange(nameof(Tag));
                }
            }
        }
        public double? TakeProfit
        {
            get { return takeProfit; }
            private set
            {
                if (takeProfit != value)
                {
                    takeProfit = value;
                    NotifyOfPropertyChange(nameof(TakeProfit));
                }
            }
        }
        public double? StopLoss
        {
            get { return stopLoss; }
            private set
            {
                if (stopLoss != value)
                {
                    stopLoss = value;
                    NotifyOfPropertyChange(nameof(StopLoss));
                }
            }
        }
        public decimal? Profit
        {
            get { return profit; }
            set
            {
                if (profit != value)
                {
                    profit = value;
                    NotifyOfPropertyChange(nameof(Profit));
                    NetProfit = profit + commission + swap;
                    NotifyOfPropertyChange(nameof(NetProfit));
                }
            }
        }
        public decimal? Margin
        {
            get { return margin; }
            set
            {
                if (margin != value)
                {
                    margin = value;
                    NotifyOfPropertyChange(nameof(Margin));
                }
            }
        }
        public decimal? NetProfit { get; private set; }
        public decimal? CurrentPrice
        {
            get { return currentPrice; }
            set
            {
                if (currentPrice != value)
                {
                    currentPrice = value;
                    NotifyOfPropertyChange(nameof(CurrentPrice));
                }
            }
        }

        public DateTime? Modified { get; private set; }

        public double? ExecPrice
        {
            get { return execPrice; }
            set
            {
                if (execPrice != value)
                {
                    execPrice = value;
                    NotifyOfPropertyChange(nameof(ExecPrice));
                }
            }
        }

        public double? ExecAmount
        {
            get { return execAmount; }
            set
            {
                if (execAmount != value)
                {
                    execAmount = value;
                    ExecAmountLots = AmountToLots(value);
                    NotifyOfPropertyChange(nameof(ExecAmount));
                    NotifyOfPropertyChange(nameof(ExecAmountLots));
                }
            }
        }
        public double? ExecAmountLots { get; private set; }

        public double? LastFillPrice
        {
            get { return lastFillPrice; }
            set
            {
                if (lastFillPrice != value)
                {
                    lastFillPrice = value;
                    NotifyOfPropertyChange(nameof(LastFillPrice));
                }
            }
        }

        public double? LastFillAmount
        {
            get { return lastFillAmount; }
            set
            {
                if (lastFillAmount != value)
                {
                    lastFillAmount = value;
                    LastFillAmountLots = AmountToLots(value);
                    NotifyOfPropertyChange(nameof(LastFillAmount));
                    NotifyOfPropertyChange(nameof(LastFillAmountLots));
                }
            }
        }
        public double? LastFillAmountLots { get; private set; }

        #endregion

        #region IOrderModel

        public bool HasError { get { return CalculationError != null; } }
        public decimal? AgentCommision { get { return 0; } }
        public OrderError CalculationError
        {
            get { return error; }
            set
            {
                error = value;
                NotifyOfPropertyChange(nameof(CalculationError));
                NotifyOfPropertyChange(nameof(HasError));
            }
        }
        public OrderCalculator Calculator { get; set; }
        bool IOrderModel.IsCalculated { get { return CalculationError == null; } }
        decimal? IOrderModel.MarginRateCurrent { get; set; }

        OrderTypes ICommonOrder.Type
        {
            get
            {
                switch (orderType)
                {
                    case TradeRecordType.Limit: return OrderTypes.Limit;
                    case TradeRecordType.StopLimit: return OrderTypes.StopLimit;
                    case TradeRecordType.Market: return OrderTypes.Market;
                    case TradeRecordType.Stop: return OrderTypes.Stop;
                    case TradeRecordType.Position: return OrderTypes.Position;
                    default: throw new NotImplementedException();
                }
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        OrderSides ICommonOrder.Side
        {
            get
            {
                switch (side)
                {
                    case TradeRecordSide.Buy: return OrderSides.Buy;
                    case TradeRecordSide.Sell: return OrderSides.Sell;
                    default: throw new NotImplementedException();
                }
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        //public AggregatedOrderType AggregatedType
        //{
        //    get
        //    {
        //        return side.Aggregate(orderType);
        //    }
        //}

        #endregion

        public OrderEntity ToAlgoOrder()
        {
            return new OrderEntity(Id)
            {
                ClientOrderId = this.clientOrderId,
                RemainingVolume = (double?)RemainingAmountLots ?? double.NaN,
                RequestedVolume = (double?)AmountLots ?? double.NaN,
                Symbol = Symbol,
                Type = FdkToAlgo.Convert(orderType),
                Side = FdkToAlgo.Convert(Side),
                Price = (double?)Price ?? double.NaN,
                StopLoss = stopLoss ?? double.NaN,
                TakeProfit = takeProfit ?? double.NaN,
                Comment = this.Comment,
                Tag = this.Tag,
                Created = this.Created ?? DateTime.MinValue,
                Modified = this.Modified ?? DateTime.MinValue,
                ExecPrice = ExecPrice ?? double.NaN,
                ExecVolume = ExecAmountLots ?? double.NaN,
                LastFillPrice = LastFillPrice ?? double.NaN,
                LastFillVolume = LastFillAmountLots ?? double.NaN
            };
        }

        private void Update(TradeRecord record)
        {
            this.Amount = (decimal)record.InitialVolume;
            this.RemainingAmount = (decimal)record.Volume;
            this.OrderType = record.Type;
            this.Side = record.Side;
            this.Price = (decimal?)record.Price ?? 0M;
            this.Created = record.Created;
            this.Modified = record.Modified;
            this.Expiration = record.Expiration;
            this.Comment = record.Comment;
            this.Tag = record.Tag;
            this.StopLoss = record.StopLoss;
            this.TakeProfit = record.TakeProfit;
            this.Swap = (decimal)record.Swap;
            this.Commission = (decimal)record.Commission;
            if (record.ImmediateOrCancel)
            {
                this.RemainingAmount = (decimal)(record.InitialVolume - record.Volume);
                this.ExecPrice = record.Price;
                this.ExecAmount = record.Volume;
                this.LastFillPrice = record.Price;
                this.LastFillAmount = record.Volume;
            }
        }

        private void Update(ExecutionReport report)
        {
            this.Amount = (decimal?)report.InitialVolume ?? 0M;
            this.RemainingAmount = (decimal)report.LeavesVolume;
            this.OrderType = report.OrderType;
            this.Side = report.OrderSide;
            this.Price = (decimal?)(report.Price ?? report.StopPrice) ?? 0;
            this.Created = report.Created;
            this.Modified = report.Modified;
            this.Expiration = report.Expiration;
            this.Comment = report.Comment;
            this.Tag = report.Tag;
            this.StopLoss = report.StopLoss;
            this.TakeProfit = report.TakeProfit;
            this.Swap = (decimal)report.Swap;
            this.Commission = (decimal)report.Commission;
            this.ExecPrice = report.AveragePrice;
            this.ExecAmount = report.ExecutedVolume;
            this.LastFillPrice = report.TradePrice;
            this.LastFillAmount = report.TradeAmount;
        }

        private decimal? AmountToLots(decimal? volume)
        {
            if (volume == null || symbolModel == null)
                return null;

            return volume / (decimal)symbolModel.LotSize;
        }

        private double? AmountToLots(double? volume)
        {
            if (volume == null || symbolModel == null)
                return null;

            return volume / symbolModel.LotSize;
        }
    }

    public interface IOrderDependenciesResolver
    {
        SymbolModel GetSymbolOrNull(string name);
    }
}
