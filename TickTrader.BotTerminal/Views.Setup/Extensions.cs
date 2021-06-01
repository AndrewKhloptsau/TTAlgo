﻿using System.Collections.Generic;
using System.Linq;
using TickTrader.Algo.Core.Setup;
using TickTrader.Algo.Domain;

namespace TickTrader.BotTerminal
{
    public static class AccountMetadataInfoExtensions
    {
        public static IReadOnlyList<SymbolKey> GetAvaliableSymbols(this AccountMetadataInfo metadata, SymbolKey defaultSymbol)
        {
            var symbols = metadata?.Symbols.OrderBy(u => u.Name).Select(c => new SymbolKey(c.Name, c.Origin)).ToList();
            if ((symbols?.Count ?? 0) == 0)
                symbols = new List<SymbolKey> { defaultSymbol };

            if (!symbols.ContainMainToken())
                symbols.Insert(0, SpecialSymbols.MainSymbolPlaceholder.GetKey());

            return symbols;
        }

        public static SymbolKey GetSymbolOrDefault(this IReadOnlyList<SymbolKey> availableSymbols, SymbolKey config)
        {
            if (config != null)
                return availableSymbols.FirstOrDefault(s => s.Origin == config.Origin && s.Name == config.Name);
            return null;
        }

        public static SymbolKey GetSymbolOrAny(this IReadOnlyList<SymbolKey> availableSymbols, SymbolKey info)
        {
            return availableSymbols.FirstOrDefault(s => s.Origin == info.Origin && s.Name == info.Name)
                ?? availableSymbols.First();
        }

        public static bool ContainMainToken(this IReadOnlyList<SymbolKey> availableSymbols)
        {
            return availableSymbols.FirstOrDefault(u => u.Name == SpecialSymbols.MainSymbol && u.Origin == SymbolConfig.Types.SymbolOrigin.Token) != null;
        }
    }


    public static class SymbolInfoExtensions
    {
        public static SymbolConfig ToConfig(this SymbolKey info)
        {
            return new SymbolConfig { Name = info.Name, Origin = info.Origin };
        }
    }


    public static class SetupContextExtensions
    {
        public static SetupContextInfo GetSetupContextInfo(this IAlgoSetupContext setupContext)
        {
            return new SetupContextInfo(setupContext.DefaultTimeFrame, setupContext.DefaultSymbol.ToConfig(), setupContext.DefaultMapping);
        }
    }
}
