﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Api;

namespace TickTrader.Algo.Core
{
    [Serializable]
    public class TradeReportEntity : TradeReport
    {
        public TradeReportEntity(string id)
        {
            ReportId = id;
        }

        public string ReportId { get; private set; }
        public string OrderId { get; set; }
        public DateTime ReportTime { get; set; }
        public DateTime OpenTime { get; set; }
        public TradeRecordTypes Type { get; set; }
        public TradeExecActions ActionType { get; set; }
        public string Symbol { get; set; }
        public double OpenQuantity { get; set; }
        public double OpenPrice { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public DateTime CloseTime { get; set; }
        public double CloseQuantity { get; set; }
        public double ClosePrice { get; set; }
        public double RemainingQuantity { get; set; }
        public double Commission { get; set; }
        public string CommissionCurrency { get; set; }
        public double Swap { get; set; }
        public double Balance { get; set; }
        public string Comment { get; set; }
        public double GrossProfitLoss { get; set; }
        public double NetProfitLoss { get; set; }

        #region FDK compatibility

        public TradeExecActions TradeTransactionReportType { get; set; }
        public double? OpenConversionRate { get; set; }
        public double? OrderLastFillAmount { get; set; }
        public double? OrderFillPrice { get; set; }
        public DateTime TransactionTime { get; set; }
        public string NextStreamPositionId { get; set; }
        public double? CloseConversionRate { get; set; }
        public double AgentCommission { get; set; }
        public double? PosRemainingPrice { get; set; }
        public OrderSide? PosRemainingSide { get; set; }
        public DateTime PositionModified { get; set; }
        public string CommCurrency { get; set; }
        public int ActionId { get; set; }
        public DateTime? Expiration { get; set; }
        public string SrcAssetCurrency { get; set; }
        public double? UsdToDstAssetConversionRate { get; set; }
        public double? DstAssetToUsdConversionRate { get; set; }
        public double? UsdToSrcAssetConversionRate { get; set; }
        public double? SrcAssetToUsdConversionRate { get; set; }
        public string ProfitCurrency { get; set; }
        public double? UsdToProfitCurrencyConversionRate { get; set; }
        public double? ProfitCurrencyToUsdConversionRate { get; set; }
        public string MarginCurrency { get; set; }
        public double? UsdToMarginCurrencyConversionRate { get; set; }
        public double? MarginCurrencyToUsdConversionRate { get; set; }
        public double? DstAssetMovement { get; set; }
        public double? DstAssetAmount { get; set; }
        public string DstAssetCurrency { get; set; }
        public double? SrcAssetMovement { get; set; }
        public double? SrcAssetAmount { get; set; }
        public DateTime PositionClosed { get; set; }
        public double PositionClosePrice { get; set; }
        public double PositionCloseRequestedPrice { get; set; }
        public double PositionLeavesQuantity { get; set; }
        public OrderSide TradeRecordSide { get; set; }
        public OrderType TradeRecordType { get; set; }
        public double StopPrice { get; set; }
        public double Price { get; set; }
        public double LeavesQuantity { get; set; }
        public double? MaxVisibleQuantity { get; set; }
        public double Quantity { get; set; }
        public string ClientId { get; set; }
        public string Id { get; set; }
        public string TransactionCurrency { get; set; }
        public double TransactionAmount { get; set; }
        public double AccountBalance { get; set; }
        //public TradeTransactionReason TradeTransactionReason { get; set; }
        public string Tag { get; set; }
        public string MinCommissionCurrency { get; set; }
        public int? Magic { get; set; }
        public bool IsReducedCloseCommission { get; set; }
        public double PositionLastQuantity { get; set; }
        public double PositionQuantity { get; set; }
        public double PosOpenPrice { get; set; }
        public double PosOpenReqPrice { get; set; }
        public DateTime PositionOpened { get; set; }
        public string PositionById { get; set; }
        public string PositionId { get; set; }
        public double? ReqCloseQuantity { get; set; }
        public double? ReqClosePrice { get; set; }
        public double? ReqOpenQuantity { get; set; }
        public double? ReqOpenPrice { get; set; }
        public DateTime OrderModified { get; set; }
        public DateTime OrderCreated { get; set; }
        public bool MarketWithSlippage { get; set; }
        public bool ImmediateOrCancel { get; set; }
        public bool IsReducedOpenCommission { get; set; }
        public double? MinCommissionConversionRate { get; set; }

        #endregion
    }
}
