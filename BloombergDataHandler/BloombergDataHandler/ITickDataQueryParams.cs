using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataWrangler.Bloomberg
{
    public interface ITickDataQuery
    {
        string Security  { get; set; }
        DateTime StartDate { get; set; }
        DateTime EndDate { get; set; }
        bool IncludeConditionCode { get; set; }
        bool includeExchangeCode { get; set; }
        List<string> Fields { get; set; }
    }
}
