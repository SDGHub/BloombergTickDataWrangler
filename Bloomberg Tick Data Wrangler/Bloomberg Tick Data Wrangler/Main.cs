using System;
using System.Windows.Forms;
using System.Collections.Generic;
using DataWrangler;
using DataWrangler.Bloomberg;
using HistoricalData = DataWrangler.HistoricalData;

namespace Bloomberg_Tick_Data_Wrangler
{
    public partial class Main : Form
    {

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

            #region Historical Playback From SQL DB  

            const string dsPath = "TickData.qbd";
            var histSqlDb = new HistoricalData.HistoricalAdapterSqlDB(dsPath);
            _histFeed = new HistoricalData.HistoricalDataHandler(histSqlDb);

            histSqlDb.AddDataInterval(new DateTime(2013, 3, 4, 23, 59, 59), new DateTime(2013, 3, 5, 0, 1, 10));
            //histSqlDb.AddDataInterval(new DateTime(2013, 3, 5, 23, 59, 44), new DateTime(2013, 3, 6, 0, 1, 0));

            var NKH3 = new DataFactory(new Security("NKM3 Index", 13, Security.SecurityType.IndexFuture));
            histSqlDb.AddSecurity(NKH3);

            _markets.AddSecurity(NKH3);
            NKH3.AddReferenceToMarkets(_markets);
            NKH3.LogEachTick = true;

            histSqlDb.LoadHistoricalData();

            _histFeed.PlayBackData();

            #endregion
            
            
            //var queryGenerator = new TickDataQueries();

            //var NKA = new Security("NKM3 Index", 13, Security.SecurityType.IndexFuture);
            //var nkaQuery = new TickDataQuery()
            //    {
            //        Security = NKA.Name,
            //        StartDate = new DateTime(2013, 3, 4, 23, 59, 50),
            //        EndDate = new DateTime(2013, 3, 5, 0, 3, 0),
            //        IncludeConditionCode = true,
            //        IncludeExchangeCode = true
            //    };

            //var bbHistTickData = new BloombergHistTickDataHandler(queryGenerator.GetTickDataQueries(nkaQuery));
            //if (bbHistTickData.ConnectAndOpenSession())
            //{
            //    bbHistTickData.SendHistTickDataRequest();
            //}
        }

    }
}
