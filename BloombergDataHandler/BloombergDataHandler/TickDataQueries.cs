using System;
using System.Collections.Generic;

namespace DataWrangler.Bloomberg
{
    public class TickDataQueries
    {
        public List<TickDataQuery> GetTickDataQueries(ITickDataQuery queryParms)
        {
            paramsChecks(queryParms);

            var queries = new List<TickDataQuery>();

            if (queryParms.EndDate <= queryParms.StartDate)
                return queries;

            for (var dt = queryParms.StartDate; dt.Date <= queryParms.EndDate; dt = dt.AddDays(1))
            {
                var start = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0); // first tick of the day
                var end = start.AddDays(1).AddTicks(-1); // last tick of the day      
                queries.Add(GenerateSingleQuery(queryParms, start, end));
            }

            return trimFirstAndLastQueries(queryParms, queries);
        }

        private List<TickDataQuery> trimFirstAndLastQueries(ITickDataQuery queryParms, List<TickDataQuery> queries)
        {

            if (queries[0].StartDate < queryParms.StartDate)
                queries[0].StartDate = queryParms.StartDate;

            if (queries[queries.Count - 1].EndDate > queryParms.EndDate)
                queries[queries.Count - 1].EndDate = queryParms.EndDate;

            return queries;
        }

        private TickDataQuery GenerateSingleQuery(ITickDataQuery queryParms, DateTime start, DateTime end)
        {
            return new TickDataQuery()
            {
                Security = queryParms.Security,
                StartDate = start,
                EndDate = end,
                IncludeConditionCode = queryParms.IncludeConditionCode,
                includeExchangeCode = queryParms.includeExchangeCode,
                Fields = queryParms.Fields
            };        
        }

        private List<string> GetDefaultFields()
        {
            return new List<string>() { "TRADE", "BID", "ASK" };
        }

        private void paramsChecks(ITickDataQuery queryParms)
        {
            if (queryParms.Fields == null || queryParms.Fields.Count == 0)
                queryParms.Fields = GetDefaultFields();

            if (queryParms.Fields.Contains(null))
                throw new ArgumentNullException("ITickDataQuery.FieldList", "FieldList contains nulls");

            if (queryParms.StartDate == default(DateTime))
                throw new ArgumentNullException("StartDate", "StartDate Property cannot be default (DateTime.MinValue)");

            if (queryParms.EndDate == default(DateTime))
                throw new ArgumentNullException("EndDate", "EndDate Property cannot be default (DateTime.MinValue)");

            if (queryParms.Security == default(string))
                throw new ArgumentNullException("Security", "Security Property cannot Empty");

        }
    }

    public class TickDataQueryParams : ITickDataQuery
    {
        public string Security { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IncludeConditionCode { get; set; }
        public bool includeExchangeCode { get; set; }
        public List<string> Fields { get; set; }
    }

    public class TickDataQuery : ITickDataQuery
    {
        public string Security { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IncludeConditionCode { get; set; }
        public bool includeExchangeCode { get; set; }
        public List<string> Fields { get; set; }
        public object correlationID { get; set; }
    }

}
