using System;
using System.Windows.Forms;
using System.Collections.Generic;
using DataWrangler;
using DataWrangler.Structures;
using DataWrangler.HistoricalData;
using DataWrangler.Bloomberg;
using HistoricalData = DataWrangler.HistoricalData;

namespace Bloomberg_Tick_Data_Wrangler
{
    public partial class Main : Form
    {
        delegate void SetStatusBarCallback(string text);

        private MarketAggregator _markets;
        private HistoricalData.HistoricalDataHandler _histFeed;

        public Main()
        {
            InitializeComponent();
            initializeDataHandler();
        }

        private void initializeDataHandler()
        {
            _markets = new MarketAggregator();

            var queryGenerator = new TickDataQueries();

            var start = new DateTime(2013, 6, 3, 23, 59, 59);
            var end = new DateTime(2013, 6, 4, 19, 0, 10);

            var NKM3 = new DataFactory(new Security("NKM3 Index", 13, Security.SecurityType.IndexFuture));
            _markets.AddSecurity(NKM3);
            NKM3.AddReferenceToMarkets(_markets);
            //NKH3.LogEachTick = true;

            var NKH3Query = new TickDataQuery()
            {
                Security = NKM3.SecurityName,
                CorrelationIdObj = NKM3,
                StartDate = start,
                EndDate = end,
                IncludeConditionCode = true,
                IncludeExchangeCode = true
            };
            var NKH3Queries = queryGenerator.GetTickDataQueries(NKH3Query);

            var NOM3 = new DataFactory(new Security("NOM3 Index", 13, Security.SecurityType.IndexFuture));
            _markets.AddSecurity(NOM3);
            NOM3.AddReferenceToMarkets(_markets);

            var NOM3Query = new TickDataQuery()
            {
                Security = NOM3.SecurityName,
                CorrelationIdObj = NOM3,
                StartDate = start,
                EndDate = end,
                IncludeConditionCode = true,
                IncludeExchangeCode = true
            };
            var NMO3Queries = queryGenerator.GetTickDataQueries(NOM3Query);



            // Pull Historical Playback From Bloomberg             
            var bbHistTickData = new BloombergHistTickDataHandler();

            bbHistTickData.BBHTDUpdate += bbHistTickData_BBHTDUpdate;
            _histFeed = new HistoricalData.HistoricalDataHandler(bbHistTickData);
            bbHistTickData.LoadHistoricalData(NKH3Queries);
            //bbHistTickData.LoadHistoricalData(NMO3Queries);
            if (bbHistTickData.ConnectAndOpenSession())
            {
                bbHistTickData.SendHistTickDataRequest();
            }

            // pull historical data from SQL DB
            // const string dsPath = "TickData.qbd";
            // var histSqlDb = new HistoricalData.HistoricalAdapterSqlDB(dsPath);
            // histSqlDb.LoadHistoricalData(NKH3Queries);
            //_histFeed = new HistoricalData.HistoricalDataHandler(histSqlDb);           
            //_histFeed.PlayBackData();
            //const string filePath = @"C:\Users\Andre\Documents\BBDataSource\Market Aggregator OutPut\";
            //_markets.BatchWriteOutData(MarketAggregator.OutPutType.FlatFile, MarketAggregator.OutPutMktMode.BothMkts, filePath, 11);


        }

        void bbHistTickData_BBHTDUpdate(object sender, EventArgs e)
        {
            if (e is BloombergHistTickDataHandler.BBHTDEventArgs)
            {
                var eventArgs = e as BloombergHistTickDataHandler.BBHTDEventArgs;
                switch (eventArgs.MsgType)
                {
                    case BloombergHistTickDataHandler.EventType.DataInit:
                        break;
                    case BloombergHistTickDataHandler.EventType.DataMsg:

                        if (String.Equals(eventArgs.Msg, "COMPLETED"))
                        {
                            Console.WriteLine("Download Completed!");
                            appendToTextBox("Download Completed! Playback started ...");

                            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                            sw.Start();
                            _histFeed.PlayBackData();
                            sw.Stop();

                            appendToTextBox(String.Format("Playback Completed in {0} secs", sw.Elapsed.ToString()));
                            appendToTextBox("Writing to file");
                            const string filePath = @"C:\Users\Andre\Documents\BBDataSource\Market Aggregator OutPut\";
                            _markets.BatchWriteOutData(MarketAggregator.OutPutType.FlatFile, MarketAggregator.OutPutMktMode.BothMkts, filePath, 11);
                            appendToTextBox("Completed file writeout");
                        }

                        break;
                    case BloombergHistTickDataHandler.EventType.ErrorMsg:
                        break;
                    case BloombergHistTickDataHandler.EventType.StatusMsg:
                        Console.WriteLine(eventArgs.Msg);
                        appendToTextBox(eventArgs.Msg);
                        break;
                    default:
                        break;
                }
            }


        }

        private void appendToTextBox(string text)
        {
            if (this.textBox1.InvokeRequired)
            {
                SetStatusBarCallback d = new SetStatusBarCallback(appendToTextBox);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBox1.AppendText(text + System.Environment.NewLine);
            }
        }
    }
}
