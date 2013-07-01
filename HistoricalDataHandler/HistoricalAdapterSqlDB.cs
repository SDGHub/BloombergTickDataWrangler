using System;
using System.Collections.Generic;
using System.Data;

namespace DataWrangler.HistoricalData
{
    public class HistoricalAdapterSqlDB : IHistoricalAdapter
    {
        private readonly QRDataSource.QRDataSource _histDs = new QRDataSource.QRDataSource();
        public bool DsInitialized = false;
        public bool DsConnected = false;

        public HistoricalDataHandler DataHandler { get; set; }
        
        public List<ITickDataQuery> Queries { get; set; }

        private readonly Dictionary<string, DataFactory> _securities = new Dictionary<string, DataFactory>();
        
        private readonly List<DataInterval> _intervals = new List<DataInterval>();

        public HistoricalAdapterSqlDB(string dsPath)
        {
            _histDs.loadDataSource(dsPath);
            DsInitialized = _histDs.initialized;

            if (DsInitialized)
            {
                DsConnected = _histDs.getSQLconnection();
            }

            Console.WriteLine("HistoricalDataHandler DSInitialized = {0}", DsInitialized);
            Console.WriteLine("HistoricalDataHandler DSConnected = {0}", DsConnected);
            Console.WriteLine(" ");
        }

        public void AddSecurity(DataFactory dataFactoryObject)
        {
            _securities.Add(dataFactoryObject.SecurityName, dataFactoryObject);
        }

        public void AddDataInterval(DateTime start, DateTime end)
        {
            if (end > start)
            {
                _intervals.Add(new DataInterval { Start = start, End = end });
            }
            else
            {
                Console.WriteLine("Bad interval! end {0} <= start {1}! ", end, start);
            }
        }

        public List<TickData> LoadHistoricalData()
        {
            foreach (DataInterval interval in _intervals)
            {

                Console.WriteLine("Requesting data from {0} to {1}", interval.Start.ToLongTimeString(), interval.End.ToLongTimeString());

                foreach (var sec in _securities)
                {

                    Console.WriteLine(" ");
                    Console.WriteLine("Requesting {0} historical data", sec.Key);
                    DataTable data = _histDs.getTickDataSeries(sec.Key, interval.Start, interval.End);
                    if (data.Rows.Count > 0)
                    {
                        List<TickData> tickData = ConvertToTickData(sec.Value, data);
                        DataHandler.ParseTickDataList(sec.Value, tickData);
                    }
                }
            }
            return new List<TickData>(); 
        }

        private List<TickData> ConvertToTickData(DataFactory factory, DataTable dt)
        {
            Console.WriteLine("Parsing {0} DataTable({1} rows)", factory.SecurityName, dt.Rows.Count.ToString());

            var tickData = new List<TickData>();

            foreach (DataRow row in dt.Rows)
            {
                TickData tick = DataRowToTickData(factory, row);
                if (tick != null)
                {
                    tickData.Add(tick);
                }
            }

            return tickData;
        }

        private static TickData DataRowToTickData(DataFactory factory, DataRow row)
        {
            Type type;
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

        private static Dictionary<string, string> GetCodes(string condCode, string exchCode, Type type)
        {
            var codes = new Dictionary<string, string>();

            if (exchCode != String.Empty)
            {
                switch (type)
                {
                    case Type.Ask:
                        codes.Add("EXCH_CODE_LAST", exchCode);
                        break;
                    case Type.Bid:
                        codes.Add("EXCH_CODE_BID", exchCode);
                        break;
                    case Type.Trade:
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

        public struct DataInterval
        {
            public DateTime Start;
            public DateTime End;
        }
    }
}
