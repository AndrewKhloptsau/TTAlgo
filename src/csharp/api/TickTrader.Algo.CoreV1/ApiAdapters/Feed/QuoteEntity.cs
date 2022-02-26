﻿using System;
using System.Runtime.InteropServices;
using TickTrader.Algo.Api;
using TickTrader.Algo.Domain;

namespace TickTrader.Algo.CoreV1
{
    public class QuoteEntity : Quote
    {
        public static readonly BookEntry[] EmptyBook = new BookEntry[0];

        private readonly QuoteInfo _quote;

        private BookEntry[] _bidBook, _askBook;


        public QuoteEntity(QuoteInfo quote)
        {
            _quote = quote;
        }

        private static BookEntry[] Convert(byte[] bandBytes)
        {
            var bandList = new BookEntry[bandBytes.Length / QuoteBand.Size];
            // memory layout is the same so we can simply copy bytes
            bandBytes.AsSpan().CopyTo(MemoryMarshal.Cast<BookEntry, byte>(bandList));
            return bandList;
        }

        public string Symbol => _quote.Symbol;
        public DateTime Time => _quote.Time;
        public bool HasAsk => _quote.HasAsk;
        public bool HasBid => _quote.HasBid;
        public double Ask => _quote.Ask;
        public double Bid => _quote.Bid;
        public bool IsAskIndicative => _quote.IsAskIndicative;
        public bool IsBidIndicative => _quote.IsBidIndicative;

        public BookEntry[] BidBook
        {
            get
            {
                if (_bidBook == null)
                    _bidBook = _quote.HasBid ? Convert(_quote.BidBytes) : EmptyBook;

                return _bidBook;

            }
        }

        public BookEntry[] AskBook
        {
            get
            {
                if (_askBook == null)
                    _askBook = _quote.HasAsk ? Convert(_quote.AskBytes) : EmptyBook;

                return _askBook;
            }
        }

        public ReadOnlySpan<BookEntry> BidSpan => MemoryMarshal.Cast<byte, BookEntry>(_quote.BidBytes);
        public ReadOnlySpan<BookEntry> AskSpan => MemoryMarshal.Cast<byte, BookEntry>(_quote.AskBytes);

        public override string ToString()
        {
            var bookDepth = Math.Max(BidSpan.Length, AskSpan.Length);
            return $"{{{Bid}{(IsBidIndicative ? "i" : "")}/{Ask}{(IsAskIndicative ? "i" : "")} {Time} d{bookDepth}}}";
        }
    }
}
