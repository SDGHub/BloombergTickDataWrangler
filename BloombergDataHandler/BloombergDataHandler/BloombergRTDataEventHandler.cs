using System;
using System.Collections.Generic;
using DataType = DataWrangler.Structures.Type;
using CorrelationID = Bloomberglp.Blpapi.CorrelationID;
using Element = Bloomberglp.Blpapi.Element;
using Event = Bloomberglp.Blpapi.Event;
using EventHandler = Bloomberglp.Blpapi.EventHandler;
using Message = Bloomberglp.Blpapi.Message;
using Name = Bloomberglp.Blpapi.Name;
using Service = Bloomberglp.Blpapi.Service;
using Session = Bloomberglp.Blpapi.Session;
using SessionOptions = Bloomberglp.Blpapi.SessionOptions;
using Subscription = Bloomberglp.Blpapi.Subscription;
using DataWrangler.Structures;

namespace DataWrangler
{
    public delegate void BloombergRTDataEventHandler(object sender, EventArgs e);

    public class BloombergRTDataProvider
    {
        private static readonly Name EXCEPTIONS = new Name("exceptions");
        private static readonly Name FIELD_ID = new Name("fieldId");
        private static readonly Name REASON = new Name("reason");
        private static readonly Name CATEGORY = new Name("category");
        private static readonly Name DESCRIPTION = new Name("description");
        private static readonly Name ERROR_CODE = new Name("errorCode");
        private static readonly Name SOURCE = new Name("source");

        private SessionOptions d_sessionOptions;
        private Session d_session;
        private List<Subscription> d_subscriptions;
        private Boolean d_isSubscribed = false;

        private List<string> fields = new List<string>();
        private Dictionary<string, object> securities = new Dictionary<string, object>();

        public enum EventType { StatusMsg, DataMsg, DataInit, ErrorMsg }
        public enum TickType { Bid, Ask, Trade, All, None }

        # region Bloomberg R/T data events handlers
        public event BloombergRTDataEventHandler BBRTDUpdate;
        public void OnBBRTDUpdate(BBRTDEventArgs e)
        {
            if (BBRTDUpdate != null)
                BBRTDUpdate(this, e);
        }

        public class BBRTDEventArgs : EventArgs
        {
            public string Msg { get; set; }
            public object cObj { get; set; }
            public TickData Trade { get; set; }
            public TickData Bid { get; set; }
            public TickData Ask { get; set; }
            public EventType MsgType;
            public TickType DataType;
            public BBRTDEventArgs(EventType msgType, string message)
            {
                this.MsgType = msgType;
                this.Msg = message;
                this.cObj = null;
                this.Bid = null;
                this.Ask = null;
                this.Trade = null; 
            }
            public BBRTDEventArgs(EventType msgType, TickType dataType, string message, object cObj)
            {
                this.MsgType = msgType;
                this.DataType = dataType;
                this.Msg = message;
                this.cObj = cObj;
                this.Bid = null;
                this.Ask = null;
                this.Trade = null; 
            }
            public BBRTDEventArgs(EventType msgType, TickType dataType, object cObj, TickData Bid, TickData Ask, TickData Trade)
            {
                this.MsgType = msgType;
                this.DataType = dataType;
                this.Msg = string.Empty;
                this.cObj = cObj;
                this.Bid = Bid;
                this.Ask = Ask;
                this.Trade = Trade;
            }
            
        }

        #endregion

        public BloombergRTDataProvider()
        {
            string serverHost = "localhost";
            int serverPort = 8194;

            // set sesson options
            d_sessionOptions = new SessionOptions();
            d_sessionOptions.ServerHost = serverHost;
            d_sessionOptions.ServerPort = serverPort;

            populateDefaultFields();
        }

        #region methods

        public void AddSecurity(object dataFactoryObject, string security)
        {
            if (security.Trim().Length > 0)
            {
                securities.Add(security, dataFactoryObject);
            }
        }

        public void addField(string field)
        {
            if (field.Trim().Length > 0)
            {
                fields.Add(field);
            }
        }

        private void populateDefaultFields()
        {
            fields.Add("LAST_PRICE");
            fields.Add("BID");
            fields.Add("ASK");
            fields.Add("EXCH_CODE_LAST");
            fields.Add("EXCH_CODE_BID");
            fields.Add("EXCH_CODE_ASK");
            fields.Add("ALL_PRICE_COND_CODE");
        }

        private bool createSession()
        {
            if (d_session == null)
            {

                OnBBRTDUpdate(new BBRTDEventArgs(EventType.StatusMsg, "Connecting"));
                // create new session
                d_session = new Session(d_sessionOptions, new EventHandler(processEvent));
            }
            return d_session.Start();
        }

        public void Stop()
        {
            if (d_subscriptions != null && d_isSubscribed)
            {
                d_session.Unsubscribe(d_subscriptions);
                OnBBRTDUpdate(new BBRTDEventArgs(EventType.StatusMsg, "Stopped"));
            }
            d_isSubscribed = false;
        }

        public void Subscribe()
        {
            // create session
            if (!createSession())
            {
                OnBBRTDUpdate(new BBRTDEventArgs(EventType.StatusMsg, "Failed to start session."));
                return;
            }
            // open market data service
            if (!d_session.OpenService("//blp/mktdata"))
            {
                OnBBRTDUpdate(new BBRTDEventArgs(EventType.StatusMsg, "Failed to open //blp/mktdata"));
                return;
            }

            Service refDataService = d_session.GetService("//blp/mktdata");

            List<string> options = new List<string>();
            options.Add("USEUTC=1");

            d_subscriptions = new List<Subscription>();
            foreach (KeyValuePair<string, object> security in securities)
            {
                d_subscriptions.Add(new Subscription(security.Key, fields, options, new CorrelationID(security.Value)));
            }

            foreach (var s in d_subscriptions)
            {
                Console.WriteLine(s.SubscriptionString);
            }

            // subscribe to securities
            d_session.Subscribe(d_subscriptions);
            d_isSubscribed = true;
            OnBBRTDUpdate(new BBRTDEventArgs(EventType.StatusMsg, "Subscribed to securities."));
        }

        #endregion

        #region Bloomberg API Events

        private void processEvent(Event eventObj, Session session)
        {
                switch (eventObj.Type)
                {
                    case Event.EventType.SUBSCRIPTION_DATA:
                        // process subscription data
                        processRequestDataEvent(eventObj, session);
                        break;
                    case Event.EventType.SUBSCRIPTION_STATUS:
                        // process subscription status
                        processRequestStatusEvent(eventObj, session);
                        break;
                    default:
                        processMiscEvents(eventObj, session);
                        break;
                }
        }

        private void processRequestDataEvent(Event eventObj, Session session)
        {
            // process message
            foreach (Message msg in eventObj)
            {
                // get correlation id
                object securityObj = msg.CorrelationID.Object;

                // process market data
                if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("MarketDataEvents")))
                {
                    // check for initialilzation
                    if (msg.HasElement("SES_START"))
                    {
                        // session start
                    }

                    // process tick data
                    switch (msg.GetElementAsString("MKTDATA_EVENT_TYPE"))
                    {
                        case "QUOTE":
                            processQuoteEvent(msg, securityObj);
                            break;
                        case "TRADE":
                            processTradeEvent(msg, securityObj);
                            break;
                        case "SUMMARY":
                            processSummaryEvent(msg, securityObj);
                            break;
                        default:
                            processQuoteEvent(msg, securityObj);
                            break;
                    }                  

                }
            }
        }

        private Dictionary<string, string> getCodes(Message msg)
        {
            Dictionary<string, string> codes = new Dictionary<string, string>();

            if (msg.HasElement("EXCH_CODE_LAST"))
                if (!msg.GetElement("EXCH_CODE_LAST").IsNull)
                    codes.Add("EXCH_CODE_LAST", msg.GetElementAsString("EXCH_CODE_LAST"));

            if (msg.HasElement("EXCH_CODE_BID"))
                if (!msg.GetElement("EXCH_CODE_BID").IsNull)
                    codes.Add("EXCH_CODE_BID", msg.GetElementAsString("EXCH_CODE_BID"));

            if (msg.HasElement("EXCH_CODE_ASK"))
                if (!msg.GetElement("EXCH_CODE_ASK").IsNull)
                    codes.Add("EXCH_CODE_ASK", msg.GetElementAsString("EXCH_CODE_ASK"));

            if (msg.HasElement("ALL_PRICE_COND_CODE"))
                if (!msg.GetElement("ALL_PRICE_COND_CODE").IsNull)
                    codes.Add("COND_CODE", msg.GetElementAsString("ALL_PRICE_COND_CODE"));

            if (codes.Count == 0) codes = null;

            return codes;
        }

        private void processQuoteEvent(Message msg, object securityObj)
        {
             Dictionary<string, string> codes = getCodes(msg);

            switch (msg.GetElementAsString("MKTDATA_EVENT_SUBTYPE"))
            {
                case "BID":
                    if (msg.HasElement("BID"))
                    {
                        TickData bid = new TickData()
                        {
                            Type = DataType.Bid,
                            SecurityObj = securityObj,
                            Price = msg.GetElementAsFloat64("BID"),
                            Codes = codes,
                            TimeStamp = Convert.ToDateTime(msg.GetElementAsString("BID_UPDATE_STAMP_RT")).ToUniversalTime()
                            
                        };

                        try { bid.Size = (msg.HasElement("BID_SIZE")) ? (uint)msg.GetElementAsInt32("BID_SIZE") : 0; }
                        catch (Bloomberglp.Blpapi.NotFoundException) { bid.Size = 0; }

                        // publish the bid event
                        if (bid.Price != 0) // check! could be problem for spreads from BB
                            OnBBRTDUpdate(new BBRTDEventArgs(EventType.DataMsg, TickType.Bid, securityObj, bid, null, null));

                    }
                    break;
                case "ASK":
                    if (msg.HasElement("ASK"))
                    {
                        TickData ask = new TickData()
                        {
                            Type = DataType.Ask,
                            SecurityObj = securityObj,
                            Price = msg.GetElementAsFloat64("ASK"),
                            Codes = codes,
                            TimeStamp = Convert.ToDateTime(msg.GetElementAsString("ASK_UPDATE_STAMP_RT")).ToUniversalTime()
                        };

                        try { ask.Size = (msg.HasElement("ASK_SIZE")) ? (uint)msg.GetElementAsInt32("ASK_SIZE") : 0; }
                        catch (Bloomberglp.Blpapi.NotFoundException) { ask.Size = 0; }

                        // publish the ask event
                        if (ask.Price != 0) // check! could be problem for spreads from BB
                            OnBBRTDUpdate(new BBRTDEventArgs(EventType.DataMsg, TickType.Ask, securityObj, null, ask, null));
                    }
                    break;
                default:
                    Element e = msg.GetElement("MKTDATA_EVENT_SUBTYPE");
                    Console.WriteLine(String.Concat(e.Name, ", ", e.GetValueAsString(), ", ", e.ElementDefinition.Description));
                    break;
            }

        }

        private void processTradeEvent(Message msg, object securityObj)
        {

            if (String.Equals(msg.GetElementAsString("MKTDATA_EVENT_SUBTYPE"), "NEW"))
            {
                Dictionary<string, string> codes = getCodes(msg);

                if (msg.HasElement("LAST_PRICE"))
                {
                    TickData trade = new TickData()
                    {
                        Type = DataType.Trade,
                        SecurityObj = securityObj,
                        Price = msg.GetElementAsFloat64("LAST_PRICE"),
                        Size = (msg.HasElement("TRADE_SIZE_ALL_SESSIONS_RT")) ? (uint)msg.GetElementAsInt32("TRADE_SIZE_ALL_SESSIONS_RT") : 0,
                        Codes = codes,
                        TimeStamp = Convert.ToDateTime(msg.GetElementAsString("TRADE_UPDATE_STAMP_RT")).ToUniversalTime()
                    };

                    // publish the trade event   
                    if (trade.Price != 0) // check! could be problem for spreads from BB
                        OnBBRTDUpdate(new BBRTDEventArgs(EventType.DataMsg, TickType.Trade, securityObj, null, null, trade));
                }
            }
        }

        private void processSummaryEvent(Message msg, object securityObj)
        {
            if (String.Equals(msg.GetElementAsString("MKTDATA_EVENT_SUBTYPE"), "INITPAINT"))
            {
                TickData bid = null;
                TickData ask = null;
                TickData trade = null;
                DateTime now = DateTime.UtcNow;

                try
                {
                    foreach (var e in msg.Elements)
                    {
                        Console.WriteLine(String.Concat(e.Name, ", ", e.GetValueAsString(), ", ", e.ElementDefinition.Description));
                    }
                    Console.WriteLine("");
                }
                catch { }

                Dictionary<string, string> codes = getCodes(msg);

                if (msg.HasElement("BID"))
                {
                    bid = new TickData()
                    {
                        Type = DataType.Bid,
                        SecurityObj = securityObj,
                        Price = msg.GetElementAsFloat64("BID"),
                        Size = (msg.HasElement("BID_SIZE_ALL_SESSIONS_RT")) ? (uint)msg.GetElementAsInt32("BID_SIZE_ALL_SESSIONS_RT") : 0,
                        Codes = codes,
                        TimeStamp = (msg.HasElement("BID_UPDATE_STAMP_RT")) ? Convert.ToDateTime(msg.GetElementAsString("BID_UPDATE_STAMP_RT")).ToUniversalTime(): now
                    };
                }

                if (msg.HasElement("ASK"))
                {
                    ask = new TickData()
                    {
                        Type = DataType.Ask,
                        SecurityObj = securityObj,
                        Price = msg.GetElementAsFloat64("ASK"),
                        Size = (msg.HasElement("ASK_SIZE_ALL_SESSIONS_RT")) ? (uint)msg.GetElementAsInt32("ASK_SIZE_ALL_SESSIONS_RT") : 0,
                        Codes = codes,
                        TimeStamp = (msg.HasElement("ASK_UPDATE_STAMP_RT")) ? Convert.ToDateTime(msg.GetElementAsString("ASK_UPDATE_STAMP_RT")).ToUniversalTime(): now  
                    };
                }

                if (msg.HasElement("LAST_PRICE"))
                {
                    trade = new TickData()
                    {
                        Type = DataType.Trade,
                        SecurityObj = securityObj,
                        Price = msg.GetElementAsFloat64("LAST_PRICE"),
                        Size = (msg.HasElement("TRADE_SIZE_ALL_SESSIONS_RT")) ? (uint)msg.GetElementAsInt32("TRADE_SIZE_ALL_SESSIONS_RT") : 0,
                        Codes = codes,
                        TimeStamp = (msg.HasElement("TRADE_UPDATE_STAMP_RT")) ? Convert.ToDateTime(msg.GetElementAsString("TRADE_UPDATE_STAMP_RT")).ToUniversalTime(): now  
                   
                    };
                }

                if (bid != null)
                {
                    OnBBRTDUpdate(new BBRTDEventArgs(EventType.DataInit, TickType.All, securityObj, bid, ask, trade));
                }
            }

          
        }

        private void processOtherEvent(Message msg, object securityObj)
        {
            throw new NotImplementedException();
        }
        
        private void processRequestStatusEvent(Event eventObj, Session session)
        {
            List<string> dataList = new List<string>();
            // process status message
            foreach (Message msg in eventObj)
            {
                object securityObj = msg.CorrelationID.Object;
                if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("SubscriptionStarted")))
                {

                    //try
                    //{
                        // check for error
                        if (msg.HasElement("exceptions"))
                        {
                            // subscription has error
                            Element error = msg.GetElement("exceptions");
                            for (int errorIndex = 0; errorIndex < error.NumValues; errorIndex++)
                            {
                                Element errorException = error.GetValueAsElement(errorIndex);
                                string field = errorException.GetElementAsString(FIELD_ID);
                                Element reason = errorException.GetElement(REASON);
                                string errorMessage = String.Concat("Field ", field, " Reason ", reason.GetElementAsString(DESCRIPTION));
                                OnBBRTDUpdate(new BBRTDEventArgs(EventType.ErrorMsg, errorMessage));
                            }
                        }
                    //}
                    //catch (Exception e)
                    //{
                    //    OnBBRTDUpdate(new BBRTDEventArgs(EventType.ErrorMsg, "Error: " + e.Message));
                    //}
                }
                else
                {
                    // check for subscription failure
                    if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("SubscriptionFailure")))
                    {
                        if (msg.HasElement(REASON))
                        {
                            Element reason = msg.GetElement(REASON);
                            string message = reason.GetElementAsString(DESCRIPTION);
                            string errorMessage = String.Concat("SubscriptionFailure", reason.GetElementAsString(DESCRIPTION));
                            OnBBRTDUpdate(new BBRTDEventArgs(EventType.ErrorMsg, errorMessage));
                        }
                    }
                }
            }
        }

        private void processMiscEvents(Event eventObj, Session session)
        {
            foreach (Message msg in eventObj)
            {
                switch (msg.MessageType.ToString())
                {
                    case "SessionStarted":
                        // "Session Started"
                        break;
                    case "SessionTerminated":
                    case "SessionStopped":
                        // "Session Terminated"
                        break;
                    case "ServiceOpened":
                        // "Reference Service Opened"
                        break;
                    case "RequestFailure":
                        Element reason = msg.GetElement(REASON);
                        string message = string.Concat("Error: Source-", reason.GetElementAsString(SOURCE),
                            ", Code-", reason.GetElementAsString(ERROR_CODE), ", category-", reason.GetElementAsString(CATEGORY),
                            ", desc-", reason.GetElementAsString(DESCRIPTION));
                        OnBBRTDUpdate(new BBRTDEventArgs(EventType.ErrorMsg, message));
                        break;
                    default:
                        OnBBRTDUpdate(new BBRTDEventArgs(EventType.StatusMsg, string.Concat("Misc Message: ", msg.ToString())));
                        break;
                }
            }
        }
        #endregion

    }

}
