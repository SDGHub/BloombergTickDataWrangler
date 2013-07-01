using System;
using System.Collections.Generic;

namespace DataWrangler
{
    public interface ITickDataQuery
    {
        string Security  { get; set; }
        DateTime StartDate { get; set; }
        DateTime EndDate { get; set; }
        bool IncludeConditionCode { get; set; }
        bool IncludeExchangeCode { get; set; }
        List<string> Fields { get; set; }
        object correlationIdObj { get; set; }
    }
}
