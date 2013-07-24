using System;
using System.Collections.Generic;
using DataWrangler;
using DataWrangler.Structures;
using DataWrangler.HistoricalData;

namespace DataWrangler.HistoricalData
{
    public interface IHistoricalAdapter
    {
        HistoricalDataHandler DataHandler { get; set; }
        
        List<ITickDataQuery> Queries { get; set; }

        void AddDataQueries(List<ITickDataQuery> queries);

        bool ConnectAndOpenSession();

        bool SendHistTickDataRequest();

        bool SendNextRequest();

    }
}
