using System;
using System.Collections.Generic;
using DataWrangler;
using DataWrangler.Structures;
using DataWrangler.HistoricalData;

namespace DataWrangler
{
    public interface ITickDataFeed
    {
        bool IsRealTime { get;  }
        Dictionary<DataFactory, bool> HasChachedData { get; }
    }
}
