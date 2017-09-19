﻿using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using TickTrader.Algo.Api;
using TickTrader.Algo.Common.Model.Config;
using TickTrader.Algo.Core.Metadata;

namespace TickTrader.Algo.Common.Model.Setup
{
    public class BarBasedPluginSetup : PluginSetup
    {
        private string _mainSymbol;
        private BarPriceType _priceType;
        private IAlgoGuiMetadata _metadata;


        public string MainSymbol => _mainSymbol;

        public BarPriceType PriceType => _priceType;


        public BarBasedPluginSetup(AlgoPluginRef pRef)
            : base(pRef)
        {
            Init();
        }

        public BarBasedPluginSetup(AlgoPluginRef pRef, string mainSymbol, BarPriceType priceType, IAlgoGuiMetadata metadata)
            : base(pRef)
        {
            _mainSymbol = mainSymbol;
            _priceType = priceType;
            _metadata = metadata;

            Init();
        }


        protected override InputSetup CreateInput(InputDescriptor descriptor)
        {
            if (!descriptor.IsValid)
                return new InputSetup.Invalid(descriptor);

            switch (descriptor.DataSeriesBaseTypeFullName)
            {
                case "System.Double": return new BarToDoubleInputSetup(descriptor, _mainSymbol, _priceType, _metadata);
                case "TickTrader.Algo.Api.Bar": return new BarToBarInputSetup(descriptor, _mainSymbol, _priceType, _metadata);
                //case "TickTrader.Algo.Api.Quote": return new QuoteToQuoteInput(descriptor, mainSymbol, false);
                //case "TickTrader.Algo.Api.QuoteL2": return new QuoteToQuoteInput(descriptor, mainSymbol, true);
                default: return new InputSetup.Invalid(descriptor, "UnsupportedInputType");
            }
        }

        protected override OutputSetup CreateOuput(OutputDescriptor descriptor)
        {
            if (!descriptor.IsValid)
                return new ErrorOutputSetup(descriptor);
            if (descriptor.DataSeriesBaseTypeFullName == "System.Double")
                return new ColoredLineOutputSetup(descriptor);
            else if (descriptor.DataSeriesBaseTypeFullName == "TickTrader.Algo.Api.Marker")
                return new MarkerSeriesOutputSetup(descriptor);
            else
                return new ColoredLineOutputSetup(descriptor, MsgCodes.UnsupportedPropertyType);
        }

        public override void Load(PluginConfig cfg)
        {
            var barConfig = cfg as BarBasedConfig;
            if (barConfig != null)
            {
                _mainSymbol = barConfig.MainSymbol;
                _priceType = barConfig.PriceType;
            }

            base.Load(cfg);
        }

        protected override PluginConfig SaveToConfig()
        {
            var config = new BarBasedConfig()
            {
                MainSymbol = MainSymbol,
                PriceType = PriceType
            };
            return config;
        }

        public override object Clone()
        {
            var save = Save();
            var setup = new BarBasedPluginSetup(PluginRef);
            setup.Load(save);
            return setup;
        }
    }
}
