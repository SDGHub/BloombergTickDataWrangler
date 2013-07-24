using System;
using System.Collections.Generic;
using System.Data;
using DataWrangler.Structures;

namespace DataWrangler.HistoricalData
{
    public delegate void HistTDEventHandler(object sender, EventArgs e);

    public class HistoricalAdapterSqlDB : IHistoricalAdapter
    {
        public enum EventType { StatusMsg, DataMsg, DataInit, ErrorMsg }
        public enum TickType { Bid, Ask, Trade, All, None }

        public event HistTDEventHandler HistTDUpdate;
        public void OnHistTDUpdate(HistTDEventArgs e)
        {
            if (HistTDUpdate != null)
                HistTDUpdate(this, e);
        }

        public class HistTDEventArgs : EventArgs
        {
            public string Msg { get; set; }
            public object cObj { get; set; }
            public TickData Trade { get; set; }
            public TickData Bid { get; set; }
            public TickData Ask { get; set; }
            public EventType MsgType;
            public TickType DataType;
            public HistTDEventArgs(EventType msgType, string message)
            {
                this.MsgType = msgType;
                this.Msg = message;
                this.cObj = null;
                this.Bid = null;
                this.Ask = null;
                this.Trade = null;
            }
            public HistTDEventArgs(EventType msgType, string message, object cObj)
            {
                this.MsgType = msgType;
                this.Msg = message;
                this.cObj = cObj;
                this.Bid = null;
                this.Ask = null;
                this.Trade = null;
            }
            public HistTDEventArgs(EventType msgType, TickType dataType, string message, object cObj)
            {
                this.MsgType = msgType;
                this.DataType = dataType;
                this.Msg = message;
                this.cObj = cObj;
                this.Bid = null;
                this.Ask = null;
                this.Trade = null;
            }
            public HistTDEventArgs(EventType msgType, TickType dataType, object cObj, TickData Bid, TickData Ask, TickData Trade)
            {
                this.MsgType = msgType;
                this.DataType = dataType;
                this.Msg = string.Empty;
                this.cObj = cObj;
                this.Bid = Bid;
                this.Ask = Ask;
                this.Trade = Trade;
            }
        }

        public List<IHistoricalAdapter> HistoricalAdapters = new List<IHistoricalAdapter>();       

        private readonly QRDataSource.QRDataSource _histDs = new QRDataSource.QRDataSource();
        public bool DsInitialized = false;
        public bool DsConnected = false;

        public HistoricalDataHandler DataHandler { get; set; }

        public List<ITickDataQuery> Queries { get { return _queries; } set { _queries = value; } }
        private List<ITickDataQuery> _queries = new List<ITickDataQuery>();

        private int requestPointer = 0;

        public HistoricalAdapterSqlDB(string dsPath)
        {
            _histDs.loadDataSource(dsPath);

            OnHistTDUpdate(new HistTDEventArgs(EventType.StatusMsg, String.Format("HistoricalDataHandler DSInitialized = {0}", DsInitialized)));
            OnHistTDUpdate(new HistTDEventArgs(EventType.StatusMsg, String.Format("HistoricalDataHandler DSConnected = {0}", DsInitialized)));
        }
        
        public bool ConnectAndOpenSession()
        {
            DsInitialized = _histDs.initialized;

            if (DsInitialized)
            {
                return DsConnected = _histDs.getSQLconnection();
            }

            return false;
        }

        public bool SendHistTickDataRequest()
        {

            if (_queries.Count < 1)
            {
                OnHistTDUpdate(new HistTDEventArgs(EventType.StatusMsg, "No queries loaded"));
                return false;
            }

            OnHistTDUpdate(new HistTDEventArgs(EventType.StatusMsg, String.Format("Beginning Requests {0}", _queries[0].Security.ToString())));

            sendRequest(_queries[0]);

            return true;
        }

        public bool SendNextRequest()
        {
            if (requestPointer <= _queries.Count - 1)
            {
                sendRequest(_queries[requestPointer]);
                return true;
            }

            OnHistTDUpdate(new HistTDEventArgs(EventType.DataMsg, "Completed all"));
            return false;
        }

        private void sendRequest(ITickDataQuery tdQuery, bool useBatching = true)
        {
            requestPointer++;

            List<ITickDataQuery> batchedQueries = new List<ITickDataQuery>();


            batchedQueries.Add(tdQuery);
            if (useBatching)
            {
                batchedQueries.Clear();
                TimeSpan timeInterval = new TimeSpan(0, 60, 0);
                batchedQueries = generateBatchedQueries(getBatchedTimes(tdQuery.StartDate, tdQuery.EndDate, timeInterval), tdQuery, timeInterval);
            }

            int partialCnt = 1; 
            foreach (var query in batchedQueries)
            {
                DataTable data = _histDs.getTickDataSeries(query.Security, query.StartDate, query.EndDate);

                OnHistTDUpdate(new HistTDEventArgs(EventType.StatusMsg,
                    String.Format("{0}: SQL Partial Response ({1} of {2})", query.Security, partialCnt.ToString(), batchedQueries.Count.ToString())));

                if (data.Rows.Count > 0)
                {
                    List<TickData> tickData = ConvertToTickData(tdQuery, data);
                    DataHandler.ParseTickDataList(tdQuery.CorrelationIdObj, tickData);
                }

                partialCnt++;

            }

            OnHistTDUpdate(new HistTDEventArgs(EventType.StatusMsg,
                String.Format("{0}: SQL Response ({1} of {2})  received", tdQuery.Security, requestPointer.ToString(), _queries.Count.ToString())));

            
            OnHistTDUpdate(new HistTDEventArgs(EventType.DataMsg,
             String.Format("Completed ({0} of {1})", requestPointer.ToString(), _queries.Count.ToString()), tdQuery));

        }

        private List<DateTime> getBatchedTimes(DateTime start, DateTime end, TimeSpan timeInterval)
        {
            var batchedTimes = new List<DateTime>();

            var nextStart = start;
            while (nextStart < end)
            {
                batchedTimes.Add(nextStart);
                nextStart += timeInterval;
            }

            return batchedTimes;
        }

        private List<ITickDataQuery> generateBatchedQueries(List<DateTime> batchedTimes, ITickDataQuery baseQuery, TimeSpan timeInterval)
        {
            List<ITickDataQuery> queries = new List<ITickDataQuery>();
            foreach (var nextstart in batchedTimes)
            {

                queries.Add(new TickDataQuery()
                {
                    Security = baseQuery.Security,
                    StartDate = nextstart,
                    EndDate = nextstart.AddTicks(-1) + timeInterval,
                    Fields = baseQuery.Fields,
                    CorrelationIdObj = baseQuery.CorrelationIdObj,
                    IncludeConditionCode = baseQuery.IncludeConditionCode,
                    IncludeExchangeCode = baseQuery.IncludeExchangeCode,
                });
            }

            return queries;

        }

        public void Reset()
        {
            _queries.Clear();
            requestPointer = 0;
        }

        public void AddDataQueries(List<ITickDataQuery> queries)
        {
            foreach (ITickDataQuery tdQuery in queries)
                _queries.Add(tdQuery);
        }

        private List<TickData> ConvertToTickData(ITickDataQuery tdQuery, DataTable dt)
        {
            OnHistTDUpdate(new HistTDEventArgs(EventType.StatusMsg,
                String.Format("{0}: Converting SQL to DataTable({1} rows)", tdQuery.Security, dt.Rows.Count.ToString())));

            var tickData = new List<TickData>();

            foreach (DataRow row in dt.Rows)
            {
                TickData tick = DataRowToTickData(tdQuery, row);
                if (tick != null)
                {
                    tickData.Add(tick);
                }
            }

            return tickData;
        }

        private static TickData DataRowToTickData(ITickDataQuery tdQuery, DataRow row)
        {
            DataWrangler.Structures.Type type;
            DateTime timeStamp;
            Double price;
            uint size;
            Dictionary<string, string> codes = null;
            TickData tick = null;

            bool successfulParse = false;

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
                                    Security = tdQuery.Security,
                                    SecurityObj = tdQuery.CorrelationIdObj,
                                };

                                successfulParse = true;
                            }
                        }

            if (!successfulParse)
                Console.WriteLine("Bad data row: type = {0}, time {1}, price {2}, size {3}, CCs = {4}, ECs = {5}", 
                    row[0].ToString(), row[1].ToString(), row[2].ToString(), row[3].ToString(), row[4].ToString(), row[5].ToString());


            return tick;
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

    }
}
