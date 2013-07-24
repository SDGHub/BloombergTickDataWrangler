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

        private MarketAggregator _markets = new MarketAggregator();
        private List<DataFactory> _factories = new List<DataFactory>();
        private Dictionary<string, bool> responded = new Dictionary<string, bool>();
        private HistoricalData.HistoricalDataHandler _histFeed = new HistoricalData.HistoricalDataHandler();
        private bool useBB = false;

        public Main()
        {
            InitializeComponent();
            initializeDataHandler();
        }

        private void initializeDataHandler()
        {      

            var queryGenerator = new TickDataQueries();

            var start = new DateTime(2013, 6, 4, 00, 00, 00);
            var end = new DateTime(2013, 6, 4, 23, 59, 59);

            setUpInsturment(start, end, "NKM3 Index", Security.SecurityType.IndexFuture);
            setUpInsturment(start, end, "NOM3 Index", Security.SecurityType.IndexFuture);
            setUpInsturment(start, end, "NIM3 Index", Security.SecurityType.IndexFuture);
            setUpInsturment(start, end, "JPY Curncy", Security.SecurityType.Curncy);


            foreach (var bbHist in _histFeed.HistoricalAdapters)
            {
                if (bbHist.ConnectAndOpenSession())
                {
                    bbHist.SendHistTickDataRequest();
                }
            }
        }

        private void setUpInsturment(DateTime start, DateTime end, string security, Security.SecurityType secType)
        {
            var sec = new DataFactory(new Security(security, 0, secType));

            _factories.Add(sec);
            _markets.AddSecurity(sec);
            sec.AddReferenceToMarkets(_markets);

            var query = new TickDataQuery()
            {
                Security = sec.SecurityName,
                CorrelationIdObj = sec,
                StartDate = start,
                EndDate = end,
                IncludeConditionCode = true,
                IncludeExchangeCode = true
            };

            var queryGenerator = new TickDataQueries();
            var queries = queryGenerator.GetTickDataQueries(query);
            var queriesPM = new List<ITickDataQuery>();

            foreach (var q in queries)
            {
                queriesPM.Add(new TickDataQuery()
                {
                    Security = q.Security,
                    StartDate = new DateTime(q.EndDate.Year, q.EndDate.Month, q.EndDate.Day, 7, 15, 00),
                    EndDate = new DateTime(q.EndDate.Year, q.EndDate.Month, q.EndDate.Day, 18, 20, 00),
                    Fields = q.Fields,
                    CorrelationIdObj = q.CorrelationIdObj,
                    IncludeConditionCode = q.IncludeConditionCode,
                    IncludeExchangeCode = q.IncludeExchangeCode,
                });

                q.StartDate = q.StartDate.AddHours(-1);
                q.EndDate = new DateTime(q.EndDate.Year, q.EndDate.Month, q.EndDate.Day, 6, 20, 00);               
            }


            if (useBB)
            {
                // Historical playback From Bloomberg             
                var histBBTickData = new BloombergHistTickDataHandler();
                histBBTickData.BBHTDUpdate += histTickData_Update;

                histBBTickData.AddDataQueries(queries);
                histBBTickData.AddDataQueries(queriesPM);

                histBBTickData.DataHandler = _histFeed;
                _histFeed.AddHistoricalAdapter(histBBTickData);

            }
            else
            {
                // Historical playback from SQL DB
                const string dsPath = "TickData.qbd";

                var histTickData = new HistoricalData.HistoricalAdapterSqlDB(dsPath);
                histTickData.HistTDUpdate += histTickData_Update;

                histTickData.AddDataQueries(queries);
                //histTickData.AddDataQueries(queriesPM);

                histTickData.DataHandler = _histFeed;
                _histFeed.AddHistoricalAdapter(histTickData);
            }


         

            responded.Add(security, false);

        }
        
        private void histTickData_Update(object sender, EventArgs e)
        {
            if (e is BloombergHistTickDataHandler.BBHTDEventArgs)
            {
                bbHistTickDataUpdate(sender, e);
            }
            else
            {

                if (e is HistoricalData.HistoricalAdapterSqlDB.HistTDEventArgs)
                {
                    sqlHistTickDataUpdate(sender, e);
                }
            }
        }

        private void sqlHistTickDataUpdate(object sender, EventArgs e)
        {
            if (e is HistoricalAdapterSqlDB.HistTDEventArgs)
            {
                var eventArgs = e as HistoricalData.HistoricalAdapterSqlDB.HistTDEventArgs;
                switch (eventArgs.MsgType)
                {
                    case HistoricalAdapterSqlDB.EventType.DataInit:
                        break;
                    case HistoricalAdapterSqlDB.EventType.DataMsg:

                        if (eventArgs.Msg.ToLower().Contains("completed"))
                        {
                            if (eventArgs.cObj is ITickDataQuery)
                            {
                                var query = (ITickDataQuery)eventArgs.cObj;
                                playBackAndWriteOut(query);
                            }
                        }

                        break;
                    case HistoricalAdapterSqlDB.EventType.ErrorMsg:
                        break;
                    case HistoricalAdapterSqlDB.EventType.StatusMsg:
                        Console.WriteLine(eventArgs.Msg);
                        appendToTextBox(eventArgs.Msg);
                        break;
                    default:
                        break;
                }
            }
        }

        private void bbHistTickDataUpdate(object sender, EventArgs e)
        {
            if (e is BloombergHistTickDataHandler.BBHTDEventArgs)
            {
                var eventArgs = e as BloombergHistTickDataHandler.BBHTDEventArgs;
                switch (eventArgs.MsgType)
                {
                    case BloombergHistTickDataHandler.EventType.DataInit:
                        break;
                    case BloombergHistTickDataHandler.EventType.DataMsg:

                        if (eventArgs.Msg.ToLower().Contains("completed"))
                        {
                            var query = (ITickDataQuery)eventArgs.cObj;
                            playBackAndWriteOut(query);
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

        private void playBackAndWriteOut(ITickDataQuery query)
        {
            responded[query.Security] = true;

            bool allDone = true;
            foreach (var received in responded)
            {
                if (!received.Value) allDone = false;
            }

            if (allDone)
            {
                //query.Security
                Console.WriteLine("Download Completed");
                appendToTextBox("Download Completed. Playback started ...");

                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                _histFeed.PlayBackData();
                sw.Stop();

                appendToTextBox(String.Format("Playback Completed in {0} secs", sw.Elapsed.ToString()));
                appendToTextBox("Writing to file");
                const string filePath = @"C:\Users\Andre\Documents\BBDataSource\Market Aggregator OutPut\";

                // use PM suffix on evening session files
                string fileNameSuffix = (query.EndDate.Hour < 7) ? fileNameSuffix = "" : fileNameSuffix = "_PM";
                sw.Reset();
                sw.Start();
                _markets.BatchWriteOutData(MarketAggregator.OutPutType.FlatFile, MarketAggregator.OutPutMktMode.SeperateAndAggregated, filePath, 25, fileNameSuffix);
                sw.Stop();

                appendToTextBox(String.Format("Completed file writeout in {0} secs", sw.Elapsed.ToString()));

                _markets.Reset();
                _histFeed.Reset();

                foreach (var factory in _factories)
                    responded[factory.SecurityName] = false;

                foreach (var bbHist in _histFeed.HistoricalAdapters)
                {
                    if (bbHist.ConnectAndOpenSession())
                    {
                        bbHist.SendNextRequest();
                    }
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
                Console.WriteLine(text);
            }
        }
    }
}
