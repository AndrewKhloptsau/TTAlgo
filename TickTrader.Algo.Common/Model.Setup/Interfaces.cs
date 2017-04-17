﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Core.Repository;

namespace TickTrader.Algo.Common.Model.Setup
{
    public interface IAlgoGuiMetadata
    {
        IReadOnlyList<ISymbolInfo> Symbols { get; }
        ExtCollection Extentions { get; }
    }

    public interface ISymbolInfo
    {
        string Name { get; }
    }
}