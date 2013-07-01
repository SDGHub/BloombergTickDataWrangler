using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DataWrangler.HistoricalData;
using DataWrangler.Bloomberg;
using BloombergDataHandler.Tests.Setups;

namespace BloombergDataHandler.Tests
{
    [TestClass]
    public class TickDataQuery_Tests_Sad_Path
    {
        TickDataQuery_Test_Setups _setups;
        public TickDataQuery_Tests_Sad_Path()
        {
            _setups = new TickDataQuery_Test_Setups();
            Console.WriteLine("TickDataQuery_Tests");
        }

        [TestMethod]
        public void EndDate_Before_StartDate_Returns_List_With_Count_of_0()
        {
            var testParams = _setups.TestParams_End_Before_Start();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            Assert.AreEqual(0, response.Count);
        }

        [TestMethod]
        public void EndDate_Equals_StartDate_Returns_List_With_Count_of_0()
        {
            var testParams = _setups.TestParams_End_Equal_Start();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
            Assert.AreEqual(0, response.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Fields_List_Which_Contains_Nulls_Throws_ArgumentNullException()
        {
            var testParams = _setups.TwoDayTestParamsWithNullFieldItems();
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Throw_ArgumentNullException_When_StartDate_Is_Null()
        {
            var testParams = _setups.TestParamsMissingDates();
            testParams.EndDate = new DateTime(2012, 1, 4, 12, 0, 0);
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Throw_ArgumentNullException_When_EndDate_Is_Null()
        {
            var testParams = _setups.TestParamsMissingDates();
            testParams.StartDate = new DateTime(2012, 1, 4, 12, 0, 0);
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Throw_ArgumentNullException_When_Security_Is_Null()
        {
            var testParams = _setups.TwoDayTestParams();
            testParams.Security = default(string);
            var queryGenerator = new TickDataQueries();
            var response = queryGenerator.GetTickDataQueries(testParams);
        }   

        
    }
}
