using System;
namespace WranglerDataStructures
{
    interface ISecurity
    {
        bool HasQuoteSize { get; }
        bool HasTradeSize { get; }
        uint Id { get; }
        string Name { get; }
        global::DataWrangler.Security.SecurityType SecType { get; }
    }
}
