using System;
using System.Collections.Generic;
using DataWrangler.Bloomberg;
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

    public class BloombergHistTickDataHandler
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
        public List<ITickDataQuery> TickDataQueries { get; private set; }
        public SessionOptions SessionOptions { get; private set; }
        public Session Session { get; private set; }

        private const string serverHost = "localhost";
        private const int serverPort = 8194;
        private int requestPointer = 0;
        private long correlationIDCnt = 0;    
   
        //!mktSummary.Complete
        
        public BloombergHistTickDataHandler(List<ITickDataQuery> tickDataQueries)
        {
            if (tickDataQueries == null || tickDataQueries.Count == 0)
                throw new ArgumentNullException("(List<TickDataQuery>", "TickDataQueries list must contain at least one query");

            TickDataQueries = tickDataQueries;
            Asynchronous = true;
            initializeSessionOptions();
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
                sendRequest(TickDataQueries[requestPointer]);
                requestPointer++;
                OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "Begining Requests"));
                return true;
            }
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

            OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "Submitted request. Waiting for response..."));

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
                new BDateTime(query.StartDate.Year, query.StartDate.Month, query.StartDate.Day,
                    query.StartDate.Hour, query.StartDate.Minute, query.StartDate.Second, 0));

            request.Set("endDateTime", 
                new BDateTime(query.EndDate.Year, query.EndDate.Month, query.EndDate.Day,
                query.EndDate.Hour, query.EndDate.Minute, query.EndDate.Second, 0));

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
                    processRequestDataEvent(eventObj, session);
                    OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "RESPONSE"));
                    break;
                case Event.EventType.PARTIAL_RESPONSE:
                    OnBBHTDUpdate(new BBHTDEventArgs(EventType.StatusMsg, "PARTIAL_RESPONSE"));
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

                    // process tick data
                    Element tickDataArray = msg.GetElement("tickData");
                    int numberOfTicks = tickDataArray.NumValues;
                    foreach (Element blmbTickDataElement in tickDataArray.Elements)
                    {
                        if (blmbTickDataElement.Name.ToString() == "tickData")
                        {
                            if (blmbTickDataElement.NumValues > 0)
                            {
                                for (int pointIndex = 0; pointIndex < blmbTickDataElement.NumValues; pointIndex++)
                                {
                                    TickData tick = BBEventToTickData(blmbTickDataElement.GetValueAsElement(pointIndex), tickDateQuery);

                                    //if (!mktSummary.Complete)
                                    //{
                                    //    mktSummary = PrepareMktSummaryEvent(factory, mktSummary, tick);
                                    //    _mktSummaryEvents[factory] = mktSummary;
                                    //}
                                    //else
                                    //{
                                    //    AddHistDataToCache(factory, tick);
                                    //}
                                }
                            }
                        } // end if
                    } // end foreach
                }
            }
        }

        private TickData BBEventToTickData(Element bbElement, ITickDataQuery tickDateQuery)
        {

            var dataPoint = new TickData()
            {
                Security = tickDateQuery.Security
            };


            if (bbElement.HasElement("time")
                && bbElement.HasElement("type")
                && bbElement.HasElement("value")
                && bbElement.HasElement("size"))
            {
                // tick field data
                dataPoint.TimeStamp = bbElement.GetElementAsTime("time").ToSystemDateTime();
                dataPoint.Price = bbElement.GetElementAsFloat64("value");
                dataPoint.Size = (uint)bbElement.GetElementAsInt32("value");
                Enum.TryParse(bbElement.GetElementAsString("type"), out dataPoint.Type);
            }


            return dataPoint;
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

        private struct MktSummaryEvent
        {
            public DateTime EventTime;
            public TickData Bid;
            public TickData Ask;
            public TickData Trade;
            public bool Complete;
           }

    }

}
