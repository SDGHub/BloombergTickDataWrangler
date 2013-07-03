using System;
using System.Collections.Generic;
using System.Data;
using DataWrangler.Structures;

namespace DataWrangler.HistoricalData
{
    public class HistoricalAdapterSqlDB : IHistoricalAdapter
    {
        private readonly QRDataSource.QRDataSource _histDs = new QRDataSource.QRDataSource();
        public bool DsInitialized = false;
        public bool DsConnected = false;

        public HistoricalDataHandler DataHandler { get; set; }
        
        public List<ITickDataQuery> Queries { get; set; }

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
        
        public void LoadHistoricalData(List<ITickDataQuery> queries)
        {
            foreach (ITickDataQuery tdQuery in queries)
            {

                Console.WriteLine("Requesting {0} historical data from {1} to {2}",tdQuery.Security, tdQuery.StartDate.ToLongTimeString(), tdQuery.EndDate.ToLongTimeString());
                
                DataTable data = _histDs.getTickDataSeries(tdQuery.Security, tdQuery.StartDate, tdQuery.EndDate);
                if (data.Rows.Count > 0)
                {
                    List<TickData> tickData = ConvertToTickData(tdQuery, data);
                    DataHandler.ParseTickDataList(tdQuery.CorrelationIdObj, tickData);
                }
            }
        }

        private List<TickData> ConvertToTickData(ITickDataQuery tdQuery, DataTable dt)
        {
            Console.WriteLine("Parsing {0} DataTable({1} rows)", tdQuery.Security, dt.Rows.Count.ToString());

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
