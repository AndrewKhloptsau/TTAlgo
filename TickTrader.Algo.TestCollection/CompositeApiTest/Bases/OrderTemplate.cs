﻿using System;
using System.Collections.Generic;
using System.Linq;
using TickTrader.Algo.Api;
using TickTrader.Algo.Api.Math;

namespace TickTrader.Algo.TestCollection.CompositeApiTest
{
    internal abstract class OrderTemplate : OrderBaseSet
    {
        private readonly double _slippagePrecision;
        private DateTime? _expiration;


        public bool IsNull => Volume.E(0.0);

        public OrderType InitType { get; }


        public string Id { get; protected set; } = string.Empty;

        public double? Price { get; set; }

        public double? StopPrice { get; set; }

        public double? MaxVisibleVolume { get; set; }

        public double? SL { get; set; }

        public double? TP { get; set; }

        public double ReqVolume { get; set; }

        public double RemVolume { get; set; }

        public double Volume { get; set; }

        public double? Slippage { get; set; }

        public string Comment { get; set; }

        public string Tag { get; set; }

        public DateTime? Expiration
        {
            get => _expiration;

            set
            {
                _expiration = value?.AddMilliseconds(-value.Value.Millisecond); //TTS reset milliseconds
            }
        }

        public string OcoRelatedOrderId { get; set; }

        public bool OcoEqualVolume { get; set; }

        internal OrderStateTemplate RelatedOcoTemplate { get; private set;}

        internal List<OrderStateTemplate> LinkedOrders { get; set; }


        internal OrderTemplate() { }

        internal OrderTemplate(OrderBaseSet set, double volume) : base(set.Type, set.Side)
        {
            _slippagePrecision = Math.Pow(10, Math.Max(Symbol.Digits, 4));

            LinkedOrders = new List<OrderStateTemplate>();

            InitType = Type;
            Options = set.Options;

            SetVolume(volume);
        }


        internal double? CalculatePrice(int coef)
        {
            var delta = coef * PriceDelta * Symbol.Point;

            return Side.IsBuy() ? Symbol.Ask - delta : Symbol.Bid + delta;
        }

        protected void SetVolume(double volume)
        {
            ReqVolume = volume;
            RemVolume = volume;
            Volume = volume;
        }

        internal OrderStateTemplate WithOCO(OrderStateTemplate mainOrder, bool equalVolume = false)
        {
            Options |= OrderExecOptions.OneCancelsTheOther;
            OcoRelatedOrderId = mainOrder.Id;
            OcoEqualVolume = equalVolume;
            RelatedOcoTemplate = mainOrder;

            return (OrderStateTemplate)this;
        }

        internal OrderStateTemplate WithLinkedOCO(OrderStateTemplate mainOrder)
        {
            Options |= OrderExecOptions.OneCancelsTheOther;
            LinkedOrders.Add(mainOrder);
            RelatedOcoTemplate = mainOrder;

            return (OrderStateTemplate)this;
        }

        internal OrderStateTemplate WithRemovedOCO()
        {
            Options &= ~OrderExecOptions.OneCancelsTheOther;
            OcoRelatedOrderId = null;
            RelatedOcoTemplate = null;

            return (OrderStateTemplate)this;
        }

        internal OpenOrderRequest GetOpenRequest()
        {
            return OpenOrderRequest.Template.Create()
                   .WithParams(Symbol.Name, Side, Type, Volume, Price, StopPrice)
                   .WithSubOpenRequests(LinkedOrders.Select(u => u.GetOpenRequest())?.ToArray())
                   .WithMaxVisibleVolume(MaxVisibleVolume)
                   .WithSlippage(GetSlippageInPercent())
                   .WithOCORelatedOrderId(OcoRelatedOrderId)
                   .WithOCOEqualVolume(OcoEqualVolume)
                   .WithExpiration(Expiration)
                   .WithComment(Comment)
                   .WithOptions(Options)
                   .WithTakeProfit(TP)
                   .WithStopLoss(SL)
                   .WithTag(Tag)
                   .MakeRequest();
        }

        internal ModifyOrderRequest GetModifyRequest()
        {
            return ModifyOrderRequest.Template.Create()
                   .WithParams(Id, Price)
                   .WithMaxVisibleVolume(MaxVisibleVolume)
                   .WithSlippage(GetSlippageInPercent())
                   .WithOCORelatedOrderId(OcoRelatedOrderId)
                   .WithOCOEqualVolume(OcoEqualVolume)
                   .WithExpiration(Expiration)
                   .WithStopPrice(StopPrice)
                   .WithComment(Comment)
                   .WithOptions(Options)
                   .WithVolume(Volume)
                   .WithTakeProfit(TP)
                   .WithStopLoss(SL)
                   .WithTag(Tag)
                   .MakeRequest();
        }

        internal CloseOrderRequest GetCloseRequest(double? volume = null)
        {
            return CloseOrderRequest.Template.Create()
                   .WithParams(Id, volume)
                   .WithSlippage(GetSlippageInPercent())
                   .MakeRequest();
        }

        private double? GetSlippageInPercent()
        {
            if (Slippage != null)
                Slippage = SlippageConverters.SlippagePipsToFractions(Slippage.Value, (IsSupportedStopPrice ? StopPrice : Price).Value, Symbol);

            return Slippage;
        }
    }
}