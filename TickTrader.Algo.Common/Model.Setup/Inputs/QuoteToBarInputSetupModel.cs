﻿using TickTrader.Algo.Common.Model.Config;
using TickTrader.Algo.Core.Metadata;
using TickTrader.Algo.Core.Repository;

namespace TickTrader.Algo.Common.Model.Setup
{
    public class QuoteToBarInputSetupModel : MappedInputSetupModel
    {
        public override string EntityPrefix => "Quote";

        public QuoteToBarInputSetupModel(InputMetadata metadata, ISymbolInfo mainSymbol, IAlgoSetupMetadata setupMetadata, IAlgoSetupContext setupContext)
            : base(metadata, mainSymbol, setupMetadata, setupContext)
        {
        }

        public override void Load(Property srcProperty)
        {
            var input = srcProperty as QuoteToBarInput;
            if (input != null)
            {
                LoadConfig(input);
            }
        }

        public override Property Save()
        {
            var input = new QuoteToBarInput();
            SaveConfig(input);
            return input;
        }


        protected override Mapping GetMapping(MappingKey mappingKey)
        {
            return SetupMetadata.Mappings.GetQuoteToBarMappingOrDefault(mappingKey);
        }
    }
}