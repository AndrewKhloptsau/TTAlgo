﻿using System;

namespace TickTrader.Algo.Protocol
{
    public class BAException : Exception
    {
        public BAException(string message) : base(message) { }
    }


    public class UnauthorizedException : BAException
    {
        public UnauthorizedException(string message) : base(message)
        {
        }
    }
}