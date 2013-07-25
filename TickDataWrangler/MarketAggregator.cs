using System;
using System.Collections.Generic;
using System.Text;
using DataWrangler.Structures;

namespace DataWrangler
{
    public class MarketAggregator
    {
        public enum Mode { RealTime = 1, Historical = 0 }
        public Mode InputMode { get; set; }

        public enum OutPutType { FlatFile, Xml, Binary, SqlTable }

        public enum OutPutMktMode { SeperateMkts, AggregatedMkts, SeperateAndAggregated }

        public OutPutType ExportMode { get; set; }

        public string OutputPath { get; set; }

        private DateTime _allMktsStateTime = DateTime.MinValue;
        private bool _allMktsInitialized = false;
        public bool AllMktsInitialized
        {
            get
            {
                if (_allMktsInitialized) return true;

                foreach (DataFactory dataFactory in _securitites)
                {
                    Console.WriteLine("         {0} {1} FirstTS {2} HasCachedData ={3} MktInitialized ={4}", 
                        dataFactory.SecurityName, _allMktsStateTime.ToLongTimeString(), dataFactory.FirstTimeBin, dataFactory.HasCachedData, dataFactory.MktInitialized);

                    if (dataFactory.HasCachedData)
                        if (!dataFactory.MktInitialized) return false;
                }

                _allMktsInitialized = true;
                return _allMktsInitialized;
            }
        }

        // main data repository
        public SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>
            Markets = new SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>();

        private DateTime _lastState = DateTime.MinValue;

        private readonly List<DataFactory> _securitites = new List<DataFactory>();

        public void Reset()
        {
            _allMktsInitialized = false;

            foreach (var factory in _securitites)
            {
                factory.Reset();
                _lastState = DateTime.MinValue;
                Markets = new SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>();
            }
        }

        public MarketAggregator()
        {
            InputMode = Mode.RealTime;
        }

        public void AddSecurity(DataFactory factory)
        {
            _securitites.Add(factory);
        }

        public void AddTickData(DataFactory factory, SortedDictionary<uint, MarketState> state, DateTime stateTime)
        {
            _allMktsStateTime = stateTime;
            
            if (AllMktsInitialized)
            {
                if (!Markets.ContainsKey(stateTime))
                {
                    Markets.Add(stateTime, new Dictionary<Security, SortedDictionary<uint, MarketState>>());
                }

                lock (Markets[stateTime])
                {
                    Dictionary<Security, SortedDictionary<uint, MarketState>> allMarketsAtTime = Markets[stateTime];

                    foreach (DataFactory f in _securitites)
                    {
                        // no market data for this security, for this time stamp exists
                        if (!allMarketsAtTime.ContainsKey(f.SecurityObj))
                        {
                            SortedDictionary<uint, MarketState> mktData = factory.Equals(f) ? state : f.GetLatestOrBefore(stateTime);

                            if (mktData != null) 
                                allMarketsAtTime.Add(f.SecurityObj, mktData);
                        }
                        else // market data for this security, for this time stamp exists already
                        {
                            if (factory.Equals(f))
                            {
                                allMarketsAtTime[f.SecurityObj] = state;
                            }
                        }
                    }

                    if (_lastState < stateTime) _lastState = stateTime;

                }
            }
        }

        public void BatchWriteOutData(OutPutType outPutMode)
        {
            BatchWriteOutData(outPutMode, OutPutMktMode.AggregatedMkts, String.Empty, 0);
        }

        public void BatchWriteOutData(OutPutType outPutMode, OutPutMktMode mktMode, string filePath, int cutOffHour = 25, string fileNameSuffix = "")
        {
            switch (outPutMode)
            {
                case OutPutType.FlatFile:
                    WriteOutFlatFile(mktMode, filePath, cutOffHour, fileNameSuffix);
                    break;
                case OutPutType.Xml:
                    break;
                case OutPutType.Binary:
                    break;
                case OutPutType.SqlTable:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("outPutMode");
            }
        }

        private void WriteOutFlatFile(OutPutMktMode mktMode, string filePath, int cutOffHour, string fileNameSuffix)
        {
            bool headerCreated = false;

            Dictionary<Security, MktOutput> MktsOutPut = new Dictionary<Security, MktOutput>();

            foreach (var dataFactory in _securitites)
            {
                MktsOutPut.Add(dataFactory.SecurityObj, new MktOutput()
                {
                    basePath = filePath,
                    baseExtension = fileNameSuffix + ".csv",
                    security = dataFactory.SecurityObj
                });
            }

            DateTime date = DateTime.MinValue;
            List<string> dataCacheAll = new List<string>();
            Dictionary<Security, string> dataCacheByMkt = new Dictionary<Security, string>();
            StringBuilder fileName = new StringBuilder();
            StringBuilder allMktsHeader = new StringBuilder();

            foreach (var timeStamp in Markets)
            {
                // calculate the header using the tickdata's built-in funnction
                if (!headerCreated)
                {
                    foreach (var security in timeStamp.Value)
                    {
                        MarketState marketState = security.Value[0];
                        string allmktHeaderString = marketState.GetHeadersString(true) + marketState.GetTradesHeaderString(5, true);
                        allMktsHeader.Append(allmktHeaderString);

                        string mktHeaderString = marketState.GetHeadersString() + marketState.GetTradesHeaderString(5);
                        MktsOutPut[security.Key].header = mktHeaderString;
                    }

                    //Console.WriteLine(allMktsHeader.ToString());
                    dataCacheAll.Add(allMktsHeader.ToString());
                    headerCreated = true;
                }

                StringBuilder data = new StringBuilder();
                bool resetDate = false;
                foreach (var security in timeStamp.Value)
                {
                    MktOutput mktOutPut = MktsOutPut[security.Key];

                    // Output a new file for each day. The end of each day is defined by a cutOffHour
                    DateTime current = timeStamp.Key;
                    if (date == DateTime.MinValue || ((current.Day != date.Day) && (current.Hour >= cutOffHour)))
                    {
                        if (resetDate == false)
                        {
                            resetDate = true;
                            if (mktMode == OutPutMktMode.AggregatedMkts)
                                if (dataCacheAll.Count > 0)
                                {
                                    writeCacheToFile(fileName.ToString(), dataCacheAll);
                                    dataCacheAll.Add(allMktsHeader.ToString());
                                }
                        }

                        if ((mktMode == OutPutMktMode.SeperateMkts) || (mktMode == OutPutMktMode.SeperateAndAggregated))
                        {
                            // output each of the individual markets data
                            if (mktOutPut.dataCache.Count > 0)
                                writeCacheToFile(mktOutPut.filePath.ToString(), mktOutPut.dataCache);
                            mktOutPut.dataCache.Add(mktOutPut.header);
                        }


                        // construct the new file name
                        fileName.Clear();
                        fileName.Append(filePath);
                        DateTime currentAdj = current.AddHours(9).AddMinutes(1);

                        string dateStr = currentAdj.Year.ToString() +
                            currentAdj.Month.ToString("00") +
                            currentAdj.Day.ToString("00");

                        switch (mktMode)
                        {
                            case OutPutMktMode.SeperateMkts:
                                mktOutPut.SetFilePath(dateStr);
                                break;
                            case OutPutMktMode.SeperateAndAggregated:
                                mktOutPut.SetFilePath(dateStr);
                                fileName.Append("All_Mkts_");
                                break;
                            case OutPutMktMode.AggregatedMkts:
                            default:
                                fileName.Append("All_Mkts_");
                                break;
                        }

                        fileName.Append(dateStr);
                        fileName.Append(fileNameSuffix + ".csv");
                    }

                    if ((mktMode == OutPutMktMode.AggregatedMkts) || (mktMode == OutPutMktMode.SeperateAndAggregated))
                    {
                        MarketState lastTick = security.Value[(uint)(security.Value.Count - 1)];
                        data.Append(MarketStateToString(lastTick) + ",");
                    }

                    if ((mktMode == OutPutMktMode.SeperateMkts) || (mktMode == OutPutMktMode.SeperateAndAggregated))
                    {
                        foreach (var mktStates in security.Value)
                        {
                            mktOutPut.dataCache.Add(MarketStateToString(mktStates.Value) + ",");
                        }
                    }
                }

                if (resetDate) date = timeStamp.Key; // reset the date if we moved passed the cut off for a new day

                if ((mktMode == OutPutMktMode.AggregatedMkts) || (mktMode == OutPutMktMode.SeperateAndAggregated))
                {
                    dataCacheAll.Add(data.ToString());
                }
            }


            if ((mktMode == OutPutMktMode.AggregatedMkts) || (mktMode == OutPutMktMode.SeperateAndAggregated))
            {
                if (dataCacheAll.Count > 0)
                    writeCacheToFile(fileName.ToString(), dataCacheAll);
            }


            if ((mktMode == OutPutMktMode.SeperateMkts) || (mktMode == OutPutMktMode.SeperateAndAggregated))
            {
                // output each of the individual markets final data set
                foreach (var mktOutPut in MktsOutPut.Values)
                {
                    if (mktOutPut.dataCache.Count > 0)
                        writeCacheToFile(mktOutPut.filePath.ToString(), mktOutPut.dataCache);
                }
            }
        }

        private void writeCacheToFile(string path, List<string> dataCache)
        {
            System.IO.File.WriteAllLines(path, dataCache);
            dataCache.Clear();
        }

        private string MarketStateToString(MarketState lastTick)
        {
            string output = lastTick.ToFlatFileStringAllData() + lastTick.ToFlatFileStringAllTrades(5);

            return output;
        }

        protected class MktOutput
        {
            public string basePath;
            public string baseExtension;
            public Security security;
            public StringBuilder filePath = new StringBuilder();
            public string header;
            public List<string> dataCache = new List<string>();

            public void SetFilePath(string fileTimeStamp)
            {
                filePath.Clear();
                filePath.Append(basePath);
                filePath.Append(security.Name);
                filePath.Append("_");
                filePath.Append(fileTimeStamp);
                filePath.Append(baseExtension);
            }

        }
    }
}