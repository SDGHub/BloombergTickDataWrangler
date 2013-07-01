using System;
using System.Collections.Generic;
using DataWrangler;
using DataWrangler.HistoricalData;

namespace DataWrangler.HistoricalData
{
    public interface IHistoricalAdapter
    {
        HistoricalDataHandler DataHandler { get; set; }

        List<ITickDataQuery> Queries { get; set; }

        void AddSecurity(DataFactory dataFactoryObject);

        void AddDataInterval(DateTime start, DateTime end);

        List<TickData> LoadHistoricalData();

    }
}
