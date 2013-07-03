using System;
using System.Collections.Generic;

namespace DataWrangler.Structures
{
    public interface ITickDataQuery
    {
        string Security  { get; set; }
        DateTime StartDate { get; set; }
        DateTime EndDate { get; set; }
        bool IncludeConditionCode { get; set; }
        bool IncludeExchangeCode { get; set; }
        List<string> Fields { get; set; }
        object CorrelationIdObj { get; set; }
    }
}
