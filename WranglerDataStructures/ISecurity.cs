using System;
namespace DataWrangler.Structures
{
    interface ISecurity
    {
        bool HasQuoteSize { get; }
        bool HasTradeSize { get; }
        uint Id { get; }
        string Name { get; }
        global::DataWrangler.Structures.Security.SecurityType SecType { get; }
    }
}
