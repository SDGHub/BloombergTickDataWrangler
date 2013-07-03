using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DataWrangler;
using DataWrangler.Structures;
using DataWrangler.HistoricalData;
using DataWrangler.Bloomberg;
using BloombergDataHandler.Tests.Setups;

namespace BloombergDataHandler.Tests
{
    [TestClass]
    public class TickDataQuery_Tests
    {
        TickDataQuery_Test_Setups _setups;
        public TickDataQuery_Tests()
        {
            _setups = new TickDataQuery_Test_Setups();
            Console.WriteLine("TickDataQuery_Tests");
        }

        [TestMethod]
        public void Query_Returns_List_of_Type_ITickDataQuery()
        {
            var testParams = _setups.TestParams_End_Before_Start();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            Assert.IsInstanceOfType(response,typeof(List<ITickDataQuery>));
        }
        
        [TestMethod]
        public void Single_Day_Query_Returns_List_With_Count_of_1()
        {
            var testParams = _setups.OneDayTestParams();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            Assert.AreEqual(1,response.Count);
        }

        [TestMethod]
        public void Single_Day_Query_Returns_Query_With_Correct_Start_DateTime()
        {
            var testParams = _setups.OneDayTestParams();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            var firstQuery = (ITickDataQuery)response[0];

            Assert.AreEqual(testParams.StartDate, firstQuery.StartDate);
        }

        [TestMethod]
        public void Single_Day_Query_Returns_Query_With_Corrct_End_DateTime()
        {
            var testParams = _setups.OneDayTestParams();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            var lastQuery = (ITickDataQuery)response[response.Count-1];

            Assert.AreEqual(testParams.EndDate, lastQuery.EndDate);
        }

        [TestMethod]
        public void Two_Day_Query_Less_than_24hrs_Returns_Single_Query()
        {
            var testParams = _setups.TwoDayTestParams();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            Assert.AreEqual(1, response.Count);
        }

        [TestMethod]
        public void Two_Day_Query_25hrs_over_3_Days_Returns_List_With_Count_of_3()
        {
            var testParams = _setups.TwoDayTestParams25hrs();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            Assert.AreEqual(3, response.Count);
        }

        [TestMethod]
        public void Two_Day_Query_Returns_Query_With_Corrct_Start_DateTime()
        {
            var testParams = _setups.TwoDayTestParams();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            var firstQuery = (ITickDataQuery)response[0];

            Assert.AreEqual(testParams.StartDate, firstQuery.StartDate);
        }

        [TestMethod]
        public void Two_Day_Query_Returns_Query_With_Corrct_End_DateTime()
        {
            var testParams = _setups.TwoDayTestParams();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            var lastQuery = (ITickDataQuery)response[response.Count - 1];

            Assert.AreEqual(testParams.EndDate, lastQuery.EndDate);
        }

        [TestMethod]
        public void When_Query_Parmas_Fields_List_is_Null_Or_Empty_Populate_with_Defaults()
        {
            var testParams = _setups.TwoDayTestParams();
            var testParamsNoFields = _setups.TwoDayTestParamsWithoutFields();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParamsNoFields);
            var lastQuery = (ITickDataQuery)response[response.Count - 1];
            CollectionAssert.AreEqual(testParams.Fields, lastQuery.Fields);
        }
    }
}
