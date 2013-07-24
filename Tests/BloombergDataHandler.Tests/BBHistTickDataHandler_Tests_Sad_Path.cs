using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DataWrangler.Bloomberg;
using DataWrangler.Structures;
using BloombergDataHandler.Tests.Setups;

namespace BloombergDataHandler.Tests
{

    [TestClass]
    public class BBHistTickDataHandler_Tests_Sad_Path
    {    
         TickDataQuery_Test_Setups _setups;
         public BBHistTickDataHandler_Tests_Sad_Path()
        {
            _setups = new TickDataQuery_Test_Setups();
            Console.WriteLine("BBHistTickDataEventHandler_Tests");
        }

         [TestMethod]
         [ExpectedException(typeof(ArgumentNullException))]
         public void DataQueries_List_isNull_Or_Empty_Throws_ArgumentNullException()
         {
             var  BBHist = new BloombergHistTickDataHandler();
             BBHist.AddDataQueries(null);

         }

    }
}
