using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DataWrangler.Structures;
using DataWrangler.HistoricalData;
using DataWrangler.Bloomberg;
using BloombergDataHandler.Tests.Setups;
using Session = Bloomberglp.Blpapi.Session;

namespace BloombergDataHandler.Tests
{
    [TestClass]
    public class BBHistTickDataHandler_Tests
    {    
         TickDataQuery_Test_Setups _setups;
         public BBHistTickDataHandler_Tests()
        {
            _setups = new TickDataQuery_Test_Setups();
            Console.WriteLine("BBHistTickDataEventHandler_Tests");
        }

         [TestMethod]
         public void After_Instance_Creation_TickDataQueries_is_Not_Null()
         {
             var testParams = _setups.OneDayTestParams();
             var queryGenerator = new TickDataQueries();
             var response = queryGenerator.GetTickDataQueries(testParams);
             var BBHist = new BloombergHistTickDataHandler();
             BBHist.LoadHistoricalData(response);

             Assert.IsNotNull(BBHist.TickDataQueries);
         }

         [TestMethod]
         public void After_Instance_Creation_TickDataQueries_List_is_Not_Empty()
         {
             var testParams = _setups.OneDayTestParams();
             var queryGenerator = new TickDataQueries();
             var response = queryGenerator.GetTickDataQueries(testParams);
             var BBHist = new BloombergHistTickDataHandler();
             BBHist.LoadHistoricalData(response);

             Assert.AreNotEqual(0, BBHist.TickDataQueries.Count);
         }

         [TestMethod]
         public void Default_Mode_Is_Set_To_Asynchronous()
         {
             var testParams = _setups.OneDayTestParams();
             var queryGenerator = new TickDataQueries();
             var response = queryGenerator.GetTickDataQueries(testParams);
             var BBHist = new BloombergHistTickDataHandler();
             BBHist.LoadHistoricalData(response);

             Assert.IsTrue(BBHist.Asynchronous);

         }

         [TestMethod]
         public void Session_Host_Is_Set_To_Default()
         {
             var testParams = _setups.OneDayTestParams();
             var queryGenerator = new TickDataQueries();
             var response = queryGenerator.GetTickDataQueries(testParams);
             var BBHist = new BloombergHistTickDataHandler();
             BBHist.LoadHistoricalData(response);

             Assert.AreEqual(_setups.serverHost, BBHist.SessionOptions.ServerHost);
         }

         [TestMethod]
         public void Session_Port_Is_Set_To_Default()
         {
             var testParams = _setups.OneDayTestParams();
             var queryGenerator = new TickDataQueries();
             var response = queryGenerator.GetTickDataQueries(testParams);
             var BBHist = new BloombergHistTickDataHandler();
             BBHist.LoadHistoricalData(response);

             Assert.AreEqual(_setups.serverPort, BBHist.SessionOptions.ServerPort);
         }

         [TestMethod]
         public void t_Is_Set_To_Default()
         {
             var testParams = _setups.OneDayTestParams();
             var queryGenerator = new TickDataQueries();
             var response = queryGenerator.GetTickDataQueries(testParams);
             var BBHist = new BloombergHistTickDataHandler();
             BBHist.LoadHistoricalData(response);

             Assert.AreEqual(_setups.serverPort, BBHist.SessionOptions.ServerPort);
         }
    
    }
}
