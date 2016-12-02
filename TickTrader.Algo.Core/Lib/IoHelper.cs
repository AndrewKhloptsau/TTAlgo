﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TickTrader.Algo.Core.Lib
{
    public static class IoHelper
    {
        const long ERROR_SHARING_VIOLATION = 0x20;
        const long ERROR_LOCK_VIOLATION = 0x21;

        public static bool IsLockExcpetion(this IOException ex)
        {
            long win32ErrorCode = ex.HResult & 0xFFFF;
            return win32ErrorCode == ERROR_SHARING_VIOLATION || win32ErrorCode == ERROR_LOCK_VIOLATION;
        }
    }
}
