﻿using System;
using System.Globalization;
using System.Runtime.Serialization;
using TickTrader.Algo.Core.Lib;
using BO = TickTrader.BusinessObjects;

namespace TickTrader.Algo.Core
{
    [Serializable]
    public class CurrencyEntity : Api.Currency, BO.ICurrencyInfo
    {
        [NonSerialized]
        private NumberFormatInfo _format;

        public CurrencyEntity(string code, int? digits = null)
        {
            Name = code;
            Digits = digits ?? 2;
            InitFormat();
        }

        public string Name { get; private set; }
        public int Digits { get; private set; }
        public int SortOrder { get; set; }
        public string Type { get; set; }
        public bool IsNull { get; set; }

        public NumberFormatInfo Format => _format;

        int BO.ICurrencyInfo.Precision => Digits;

        int BO.ICurrencyInfo.SortOrder => SortOrder;


        [OnDeserialized]
        private void OnDeserialized(StreamingContext c)
        {
            InitFormat();
        }

        private void InitFormat()
        {
            _format = FormatExtentions.CreateTradeFormatInfo(Digits);
        }

        #region FDK compatibility

        BO.CurrencyType BO.ICurrencyInfo.Type => BO.CurrencyType.Default;

        #endregion

        public override string ToString() { return $"{Name} (Digits = {Digits})"; }


        public void Update(CurrencyEntity other)
        {
            if (other == null)
            {
                IsNull = true;
            }
            else
            {
                Name = other.Name;
                Digits = other.Digits;
                SortOrder = other.SortOrder;
                IsNull = other.IsNull;
                Type = other.Type;
                InitFormat();
            }
        }
    }
}