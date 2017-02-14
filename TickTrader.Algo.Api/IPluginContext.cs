﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TickTrader.Algo.Api
{
    internal interface IPluginContext
    {
        FeedProvider Feed { get; }
        AccountDataProvider AccountData { get; }
        SymbolProvider Symbols { get; }
        IPluginMonitor Logger { get; }
        TradeCommands TradeApi { get; }
        StatusApi StatusApi { get; }
        EnvironmentInfo Environment { get; }
        IHelperApi Helper { get; }
        DiagnosticInfo Diagnostics { get; }
        bool IsStopped { get; }
        void OnExit();
        void OnPluginThread(Action action);
        void BeginOnPluginThread(Action action);
        Task OnPluginThreadAsync(Action action);
    }

    internal interface IHelperApi
    {
        string FormatPrice(double price, int digits);
        string FormatPrice(double price, Symbol symbolInfo);

        double RoundVolumeDown(double volume, Symbol symbolInfo);
        double RoundVolumeUp(double volume, Symbol symbolInfo);

        Quote CreateQuote(string symbol, DateTime time, IEnumerable<BookEntry> bids, IEnumerable<BookEntry> asks);
        BookEntry CreateBookEntry(double price, double volume);
        IEnumerable<BookEntry> CreateBook(IEnumerable<double> prices, IEnumerable<double> volumes);
    }

    internal interface IPluginMonitor
    {
        void Print(string entry, object[] parameters);
        void PrintError(string entry, object[] parameters);
    }

    internal interface IPluginActivator
    {
        IPluginContext Activate(AlgoPlugin instance);
    }
}