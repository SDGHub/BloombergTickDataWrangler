using System;
using System.Collections.Generic;
using DataWrangler;
using DataWrangler.Structures;
using DataWrangler.HistoricalData;
using Event = Bloomberglp.Blpapi.Event;
using Message = Bloomberglp.Blpapi.Message;
using Element = Bloomberglp.Blpapi.Element;
using Name = Bloomberglp.Blpapi.Name;
using Request = Bloomberglp.Blpapi.Request;
using Service = Bloomberglp.Blpapi.Service;
using Session = Bloomberglp.Blpapi.Session;
using SessionOptions = Bloomberglp.Blpapi.SessionOptions;
using EventHandler = Bloomberglp.Blpapi.EventHandler;
using CorrelationID = Bloomberglp.Blpapi.CorrelationID;
using BDateTime = Bloomberglp.Blpapi.Datetime;

namespace DataWrangler.Bloomberg
{
    public delegate void BBHTDEventHandler(object sender, EventArgs e);

    public class BloombergHistTickDataHandler: IHistoricalAdapter
    {
        public event BBHTDEventHandler BBHTDUpdate;
        public void OnBBHTDUpdate(BBHTDEventArgs e)
        {
            if (BBHTDUpdate != null)
                BBHTDUpdate(this, e);
        }

        public class BBHTDEventArgs : EventArgs
        {
            public string Msg { get; set; }
            public object cObj { get; set; }
            public TickData Trade { get; set; }
            public TickData Bid { get; set; }
            public TickData Ask { get; set; }
            public EventType MsgType;
            public TickType DataType;
            public BBHTDEventArgs(EventType msgType, string message)
            {
                this.MsgType = msgType;
                this.Msg = message;
                this.cObj = null;
                this.Bid = null;
                this.Ask = null;
                this.Trade = null;
            }
            public BBHTDEventArgs(EventType msgType, string message, object cObj)
            {
                this.MsgType = msgType;
                this.Msg = message;
                this.cObj = cObj;
                this.Bid = null;
                this.Ask = null;
                this.Trade = null;
            }
            public BBHTDEventArgs(EventType msgType, TickType dataType, string message, object cObj)
            {
                this.MsgType = msgType;
                this.DataType = dataType;
                this.Msg = message;
                this.cObj = cObj;
                this.Bid = null;
                this.Ask = null;
                this.Trade = null;
            }
            public BBHTDEventArgs(EventType msgType, TickType dataType, object cObj, TickData Bid, TickData Ask, TickData Trade)
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

        private static readonly Name EXCEPTIONS = new Name("exceptions");
        private static readonly Name FIELD_ID = new Name("fieldId");
        private static readonly Name REASON = new Name("reason");
        private static readonly Name CATEGORY = new Name("category");
        private static readonly Name DESCRIPTION = new Name("description");
        private static readonly Name ERROR_CODE = new Name("errorCode");
        private static readonly Name SOURCE = new Name("source");
        private static readonly Name MESSAGE = new Name("message");
        private static readonly Name RESPONSE_ERROR = new Name("responseError");

        public enum EventType { StatusMsg, DataMsg, DataInit, ErrorMsg }
        public enum TickType { Bid, Ask, Trade, All, None }

        public bool Asynchronous { get; set; }
        public bool AutoChainQueries { get; set; }
        public List<ITickDataQuery> TickDataQueries { get; private set; }
        public SessionOptions SessionOptions { get; private set; }
        public Session Session { get; private set; }

        private const string serverHost = "localhost";
        private const int serverPort = 8194;
        private int requestPointer = 0;
        private int partialResponseCnt = 0;

        // IHistoricalAdapter requirements
        public HistoricalDataHandler DataHandler { get; set; }
        public List<ITickDataQuery> Queries { get; set; }   
        
        public BloombergHistTickDataHandler()
        {
            TickDataQueries = new List<ITickDataQuery>(); 
            Asynchronous = true;
            initializeSessionOptions();
        }

        public void LoadHistoricalData(List<ITickDataQuery> queries)
        {
            if (queries == null || queries.Count == 0)
                throw new ArgumentNullException("(List<TickDataQuery>", "TickDataQueries list must contain at least one query");

            if (TickDataQueries.Count < 1)
            {
                TickDataQueries = queries;
            }
            else
            {
                foreach (var query in queries)
                {
                    TickDataQueries.Add(query);
                } 
            }
        }

        private void initializeSessionOptions()
        {
            SessionOptions = new SessionOptions();
            SessionOptions.ServerHost = serverHost;
            SessionOptions.ServerPort = serverPort;
        }

        public bool ConnectAndOpenSession()
        {
            // create session
            if (!createSession())
            {
                OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "Failed to start session."));
                return false;
            }
            // open reference data service
            if (!Session.OpenService("//blp/refdata"))
            {
                OnBBHTDUpdate( new BBHTDEventArgs(EventType.StatusMsg, "Failed to open //blp/refdata"));
                return false;
            }

            OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "Connected. Session Open..."));

            return true;
        }

        private bool createSession()
        {
            if (Session == null)
            {
                if (Asynchronous)
                {
                    Session = new Session(SessionOptions, new EventHandler(processEvent));
                }
                else
                {
                    Session = new Session(SessionOptions);
                }
                return Session.Start();
            }
            return true;
        }

        public bool SendHistTickDataRequest()
        {
            if (Session != null)
            {
                requestPointer = 0;
                partialResponseCnt = 0;
                sendRequest(TickDataQueries[requestPointer]);
                requestPointer++;
                OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "Begining Requests"));
                return true;
            }
            return false;
        }

        public bool SendNextRequest()
        {
            if (requestPointer <= TickDataQueries.Count - 1)
            {
                sendRequest(TickDataQueries[requestPointer]);
                partialResponseCnt = 0;
                requestPointer++;
                return true;
            }

             OnBBHTDUpdate(new BBHTDEventArgs(EventType.DataMsg,"Completed all"));
            return false;
        }

        private bool sendRequest(ITickDataQuery tickDataQuery)
        {
            if (!ConnectAndOpenSession())
                return false;

            Request request = getBBRequest(tickDataQuery);

            CorrelationID cID = new CorrelationID(tickDataQuery);

            Session.Cancel(cID);
            Session.SendRequest(request, cID);

            var msg = String.Format("Submitted request: {0} ({1} ~ {2}) ...",
                tickDataQuery.Security,
                tickDataQuery.StartDate.ToString("yy/MM/dd HH:mm:ss"),
                tickDataQuery.EndDate.ToString("yy/MM/dd HH:mm:ss"));
            OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, msg));

            if (!Asynchronous) return synchronousProcessing();

            return true;
        }

        private Request getBBRequest(ITickDataQuery query)
        {

            Service refDataService = Session.GetService("//blp/refdata");
            Request request = refDataService.CreateRequest("IntradayTickRequest");

            request.Set("security", query.Security);

            Element eventTypes = request.GetElement("eventTypes");
            foreach (string field in query.Fields)
                eventTypes.AppendValue(field);

            request.Set("includeConditionCodes", query.IncludeConditionCode);
            request.Set("includeExchangeCodes", query.IncludeExchangeCode);

            request.Set("startDateTime",
                new BDateTime(query.StartDate));

            request.Set("endDateTime", 
                new BDateTime(query.EndDate));

            return request;

        } 

        private bool synchronousProcessing()
        {
            // synchronous mode. Wait for reply before proceeding.
            while (true)
            {
                Event eventObj = Session.NextEvent();
                OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "Processing data..."));
                processEvent(eventObj, Session);
                if (eventObj.Type == Event.EventType.RESPONSE)
                {
                    break;
                }
            }
            OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "Completed"));
            return true;
        }

        private void processEvent(Event eventObj, Session session)
        {
            switch (eventObj.Type)
            {
                case Event.EventType.RESPONSE: // final respose

                    OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg,
                        String.Format("Response ({0} of {1})  received", requestPointer.ToString(), TickDataQueries.Count.ToString())));

                    processRequestDataEvent(eventObj, session);

                    foreach (Message msg in eventObj)
                    {
                        if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("IntradayTickResponse")))
                        {
                            CorrelationID cID = msg.CorrelationID;
                            var tickDateQuery = (ITickDataQuery)msg.CorrelationID.Object;
                            OnBBHTDUpdate(new BBHTDEventArgs(EventType.DataMsg,
                                String.Format("Completed ({0} of {1})", requestPointer.ToString(), TickDataQueries.Count.ToString()), tickDateQuery));
                        }
                    }

                    if (AutoChainQueries)
                        SendNextRequest();

                    break;
                case Event.EventType.PARTIAL_RESPONSE:
                    partialResponseCnt++;
                    OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "Pratial Response #" + partialResponseCnt.ToString()));
                    processRequestDataEvent(eventObj, session);
                    break;
                default:
                    // processMiscEvents(eventObj, session);
                    break;
            }
        }

        private void processRequestDataEvent(Event eventObj, Session session)
        {
            // process message
            foreach (Message msg in eventObj)
            {
                if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("IntradayTickResponse")))
                {
                    CorrelationID cID = msg.CorrelationID;
                    var tickDateQuery = (ITickDataQuery)msg.CorrelationID.Object;
                    List<string> reponseFields = getReponseFields();
                    if (msg.HasElement(RESPONSE_ERROR))
                    {
                        // process error
                        Element error = msg.GetElement(RESPONSE_ERROR);
                        if (msg.NumElements == 1)
                        {
                            string errorMsg = tickDateQuery.Security + " " + error.GetElementAsString(MESSAGE);
                            OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, errorMsg));
                            return;
                        }
                    }

                    Element tickDataArray = msg.GetElement("tickData");
                    var tickData = new List<TickData>();

                    foreach (Element blmbTickDataElement in tickDataArray.Elements)
                    {
                        if (blmbTickDataElement.Name.ToString() == "tickData")
                        {
                            if (blmbTickDataElement.NumValues > 0)
                            {
                                for (int pointIndex = 0; pointIndex < blmbTickDataElement.NumValues; pointIndex++)
                                {
                                    TickData tick = BBEventToTickData(blmbTickDataElement.GetValueAsElement(pointIndex), tickDateQuery);
                                    tickData.Add(tick);
                                    //Console.WriteLine(tick.ToString());
                                }
                            }
                        }
                    }

                    DataHandler.ParseTickDataList(tickDateQuery.CorrelationIdObj, tickData);
               
                }
            }
        }

        private TickData BBEventToTickData(Element bbElement, ITickDataQuery tickDateQuery)
        {
            var dataPoint = new TickData()
            {
                Security = tickDateQuery.Security,
                SecurityObj = tickDateQuery.CorrelationIdObj
            };


            if (bbElement.HasElement("time")
                && bbElement.HasElement("type")
                && bbElement.HasElement("value")
                && bbElement.HasElement("size"))
            {
                // tick field data
                dataPoint.TimeStamp = bbElement.GetElementAsTime("time").ToSystemDateTime();
                //dataPoint.TimeStamp = Convert.ToDateTime(bbElement.GetElementAsString("time"));//.ToUniversalTime();
                dataPoint.Price = bbElement.GetElementAsFloat64("value");
                dataPoint.Size = (uint)bbElement.GetElementAsInt32("size");
                string msgType = bbElement.GetElementAsString("type");
                Enum.TryParse(msgType, true, out dataPoint.Type);
            }

            return dataPoint;
        }

        private static Dictionary<string, string> GetCodes(string condCode, string exchCode, DataWrangler.Structures.Type type)
        {
            var codes = new Dictionary<string, string>();

            if (exchCode != String.Empty)
            {
                switch (type)
                {
                    case DataWrangler.Structures.Type.Ask:
                        codes.Add("EXCH_CODE_LAST", exchCode);
                        break;
                    case DataWrangler.Structures.Type.Bid:
                        codes.Add("EXCH_CODE_BID", exchCode);
                        break;
                    case DataWrangler.Structures.Type.Trade:
                        codes.Add("EXCH_CODE_ASK", exchCode);
                        break;
                }
            }
            else
            {
                if (condCode != String.Empty)
                {
                    codes.Add("COND_CODE", condCode);
                }
            }

            if (codes.Count == 0) codes = null;

            return codes;
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
                        //OnBBHTDUpdate(new BBHTDEventArgs(message));
                        break;
                    default:
                        OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, msg.MessageType.ToString()));
                        break;
                }
            }
        }

        private List<string> getReponseFields()
        {
            List<string> fields = new List<string>();
            fields.Add("security");
            fields.Add("time");
            fields.Add("type");
            fields.Add("value");
            fields.Add("size");
            fields.Add("conditionCodes");
            fields.Add("exchangeCode");
            return fields;
        }
    }

}
