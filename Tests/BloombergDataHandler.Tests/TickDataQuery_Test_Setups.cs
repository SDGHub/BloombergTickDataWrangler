using System;
using System.Collections.Generic;
using DataWrangler.Bloomberg;

namespace BloombergDataHandler.Tests.Setups
{
    class TickDataQuery_Test_Setups
    {
        public string serverHost = "localhost";
        public int serverPort = 8194;

        public TickDataQueryParams OneDayTestParams()
        {
            return new TickDataQueryParams()
            {
                Security = "NKM3 Index",
                StartDate = new DateTime(2012, 1, 12, 1, 0, 0),
                EndDate = new DateTime(2012, 1, 12, 2, 0, 0),
                Fields = getDefaultFields(),
                IncludeConditionCode = true,
                includeExchangeCode = true
            };
        }

        public TickDataQueryParams TwoDayTestParams()
        {
            return new TickDataQueryParams()
            {
                Security = "NKM3 Index",
                StartDate = new DateTime(2012, 1, 5, 5, 0, 0),
                EndDate = new DateTime(2012, 1, 6, 0, 0, 0),
                Fields = getDefaultFields(),
                IncludeConditionCode = true,
                includeExchangeCode = true
            };
        }

        public TickDataQueryParams TwoDayTestParamsWithoutFields()
        {
            var twoDayTestParamsWithoutFields = TwoDayTestParams();
            twoDayTestParamsWithoutFields.Fields = null;
            return twoDayTestParamsWithoutFields;
        }

        public TickDataQueryParams TestParams_End_Before_Start()
        {
            return new TickDataQueryParams()
            {
                Security = "NKM3 Index",
                StartDate = new DateTime(2012, 1, 5, 3, 0, 0),
                EndDate = new DateTime(2012, 1, 4, 12, 0, 0),
                Fields = getDefaultFields(),
                IncludeConditionCode = true,
                includeExchangeCode = true
            };
        }

        public TickDataQueryParams TestParams_End_Equal_Start()
        {
            return new TickDataQueryParams()
            {
                Security = "NKM3 Index",
                StartDate = new DateTime(2012, 1, 5, 3, 0, 0),
                EndDate = new DateTime(2012, 1, 5, 3, 0, 0),
                Fields = getDefaultFields(),
                IncludeConditionCode = true,
                includeExchangeCode = true
            };
        }

        public TickDataQueryParams TestParamsMissingDates()
        {
            return new TickDataQueryParams()
            {
                Security = "NKM3 Index",
                Fields = getDefaultFields(),
                IncludeConditionCode = true,
                includeExchangeCode = true
            };
        }

        public TickDataQueryParams TwoDayTestParamsWithNullFieldItems()
        {
            var twoDayTestParamsWithoutFields = TwoDayTestParams();
            twoDayTestParamsWithoutFields.Fields[1] = null;
            return twoDayTestParamsWithoutFields;
        }

        public List<string> getDefaultFields()
        {
            List<string> fields = new List<string>();
            fields.Add("TRADE");
            fields.Add("BID");
            fields.Add("ASK");
            return fields;
        }
    }
}
