using System;
using System.Collections.Generic;
using System.Data;
using DataWrangler;
using DataWrangler.Structures;

namespace DataWrangler.HistoricalData
{

    public class HistoricalDataHandler: ITickDataFeed
    {
        public bool IsRealTime { get { return false; } }

        public Dictionary<DataFactory, bool> HasChachedData { get { return _hasChachedData; } }
        private Dictionary<DataFactory, bool> _hasChachedData = new Dictionary<DataFactory, bool>(); 

        public List<IHistoricalAdapter> HistoricalAdapters = new List<IHistoricalAdapter>();

        // cached historical data
        public SortedDictionary<DateTime, Dictionary<DataFactory, List<TickData>>> CachedTickData
            = new SortedDictionary<DateTime, Dictionary<DataFactory, List<TickData>>>();

        private readonly Dictionary<DataFactory, MktSummaryEvent>
            _mktSummaryEvents = new Dictionary<DataFactory, MktSummaryEvent>();
 
        public HistoricalDataHandler()
        {
        }

        public void AddHistoricalAdapter(IHistoricalAdapter historicalAdapter)
        {
            HistoricalAdapters.Add(historicalAdapter);
        }

        public void Reset()
        {
            CachedTickData.Clear();
            _hasChachedData.Clear();
            _mktSummaryEvents.Clear();
        }

        #region Historical Data Caching

        public void ParseTickDataList(object dataObject, List<TickData> dt)
        {

            if (dataObject is DataFactory)
            {
                var factory = (DataFactory)dataObject;

                Console.WriteLine("Parsing {0} DataTable({1} rows)", factory.SecurityName, dt.Count.ToString());

                if (!_mktSummaryEvents.ContainsKey(factory))
                    _mktSummaryEvents.Add(factory, new MktSummaryEvent { Complete = false });
                MktSummaryEvent mktSummary = _mktSummaryEvents[factory];


                if (!_hasChachedData.ContainsKey(factory))
                    _hasChachedData.Add(factory, false);

                if (dt.Count > 0)
                    _hasChachedData[factory] = true;

                foreach (var tick in dt)
                {
                    if (tick != null)
                    {
                        if (!mktSummary.Complete)
                        {
                            mktSummary = PrepareMktSummaryEvent(factory, mktSummary, tick);
                            _mktSummaryEvents[factory] = mktSummary;
                        }
                        else
                        {
                            AddHistDataToCache(factory, tick);
                        }
                    }
                }
            }
        }

        private static TickData DataRowToTickData(DataFactory factory, DataRow row)
        {
            DataWrangler.Structures.Type type;
            DateTime timeStamp;
            Double price;
            uint size;
            Dictionary<string, string> codes = null;
            TickData tick = null;

            // try parse dataRow for tick data values
            if (Enum.TryParse(row[0].ToString(), out type))
                if (DateTime.TryParse(row[1].ToString(), out timeStamp))
                    if (Double.TryParse(row[2].ToString(), out price))
                        if (uint.TryParse(row[3].ToString(), out size))
                        {
                            if ((price > 0) || (price < 0))
                            {
                                // if there are any codes, add to the tickData event
                                if ((row[4].ToString() != String.Empty) || (row[5].ToString() != String.Empty))
                                {
                                    codes = GetCodes(row[4].ToString(), row[5].ToString(), type);
                                }

                                // create a new tick data event
                                tick = new TickData
                                {
                                    Type = type,
                                    TimeStamp = timeStamp,
                                    Price = price,
                                    Size = size,
                                    Codes = codes,
                                    Security = factory.SecurityObj.Name,
                                    SecurityObj = factory.SecurityObj,
                                    SecurityID = factory.SecurityObj.Id
                                };

                                //Console.WriteLine(tick.ToString());
                            }
                        }

            return tick;
        }

        private static MktSummaryEvent PrepareMktSummaryEvent(DataFactory factory, MktSummaryEvent mktSummary, TickData tick)
        {
            switch (tick.Type)
            {
                case DataWrangler.Structures.Type.Ask:
                        mktSummary.Ask = tick;
                        mktSummary = CheckForSyntheticTradeCondition(factory, mktSummary);
                    break;
                case DataWrangler.Structures.Type.Bid:
                        mktSummary.Bid = tick;
                        mktSummary = CheckForSyntheticTradeCondition(factory, mktSummary);
                    break;
                case DataWrangler.Structures.Type.Trade:
                        mktSummary.Trade = tick;
                    break;
            }

            if (tick.TimeStamp > mktSummary.EventTime) mktSummary.EventTime = tick.TimeStamp;

            if ((mktSummary.Ask != null) && (mktSummary.Bid != null) && mktSummary.Trade != null)
            {
                mktSummary.Complete = true;
                Console.WriteLine("Mkt summary {0} {1} ask {2} bid {3} trade {4}", tick.Security,
                        mktSummary.EventTime.ToLongTimeString(),
                        mktSummary.Ask.Price, mktSummary.Bid.Price, mktSummary.Trade.Price);
            }

            return mktSummary;

        }

        private static MktSummaryEvent CheckForSyntheticTradeCondition(DataFactory factory, MktSummaryEvent mktSummary)
        {
            if ((mktSummary.Ask != null) && (mktSummary.Bid != null))
            {
                mktSummary.Trade = new TickData
                {
                    Type = DataWrangler.Structures.Type.Trade,
                    TimeStamp = mktSummary.EventTime,
                    Price = (mktSummary.Bid.Price + mktSummary.Ask.Price) / 2,
                    Size = 0,
                    Codes = null,
                    Security = factory.SecurityObj.Name,
                    SecurityObj = factory.SecurityObj,
                    SecurityID = factory.SecurityObj.Id
                };
            }

            return mktSummary;
        }

        private static Dictionary<string, string> GetCodes(string condCode, string exchCode, DataWrangler.Structures.Type type)
        {
            var codes = new Dictionary<string, string>();

            if (exchCode != String.Empty)
            {
                switch (type)
                {
                    case DataWrangler.Structures.Type.Ask:
                        codes.Add("EXCH_CODE_LAST", exchCode);
                        break;
                    case DataWrangler.Structures.Type.Bid:
                        codes.Add("EXCH_CODE_BID", exchCode);
                        break;
                    case DataWrangler.Structures.Type.Trade:
                        codes.Add("EXCH_CODE_ASK", exchCode);
                        break;
                }
            }
            else
            {
                if (condCode != String.Empty)
                {
                    codes.Add("COND_CODE", condCode);
                }
            }

            if (codes.Count == 0) codes = null;

            return codes;
        }

        private void AddHistDataToCache(DataFactory factory, TickData tick)
        {
            if (!CachedTickData.ContainsKey(tick.TimeStamp))
                CachedTickData.Add(tick.TimeStamp, new Dictionary<DataFactory, List<TickData>>());

            Dictionary<DataFactory, List<TickData>> timeInterval = CachedTickData[tick.TimeStamp];

            if (!timeInterval.ContainsKey(factory))
                timeInterval.Add(factory, new List<TickData>());
            List<TickData> tickData = timeInterval[factory];

            tickData.Add(tick);
        }
        
        private struct MktSummaryEvent
        {
            public DateTime EventTime;
            public TickData Bid;
            public TickData Ask;
            public TickData Trade;
            public bool Complete;
        }

        #endregion

        #region Historical Data Playback

        public void PlayBackData()
        {
            foreach (var secondsBin in CachedTickData)
            {
                List<DataFactory> removeLst = new List<DataFactory>();
                foreach (var kvpSummaryEvent in _mktSummaryEvents)
                {
                    var factory = kvpSummaryEvent.Key;
                    var mktSummaryEvent = kvpSummaryEvent.Value;                   

                     if (mktSummaryEvent.EventTime <= secondsBin.Key)
                    {
                        factory.FirstTick(mktSummaryEvent.Bid, mktSummaryEvent.Ask, mktSummaryEvent.Trade);
                        removeLst.Add(factory);
                    }
                }

                foreach (var factory in removeLst)
                    _mktSummaryEvents.Remove(factory);

                foreach (var security in secondsBin.Value)
                {
                    DataFactory factory = security.Key;

                    // begin play back only after we have a summary event for each security
                    if (_mktSummaryEvents.Count < 1)
                    {
                        foreach (TickData tickData in security.Value)
                        {
                            factory.NewTick(tickData);
                        }
                    }
                }                                
            }
        }

        #endregion
    }
}