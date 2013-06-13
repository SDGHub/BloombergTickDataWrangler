using System;
using System.Collections.Generic;
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
    public class BloombergHistTickDataHandler
    {
        public bool Asynchronous { get; set; }
        public List<TickDataQuery> TickDataQueries { get; private set; }
        public SessionOptions SessionOptions { get; private set; }
        public Session Session { get; private set; }

        private const string serverHost = "localhost";
        private const int serverPort = 8194;
        private int requestPointer = 0;

        public BloombergHistTickDataHandler(List<TickDataQuery> tickDataQueries)
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

        public bool ConnectAndOpenSession()
        {
            // create session
            if (!createSession())
            {
                //OnBBHTDUpdate(new BBHTDEventArgs("Failed to start session."));
                return false;
            }
            // open reference data service
            if (!Session.OpenService("//blp/refdata"))
            {
                //OnBBHTDUpdate(new BBHTDEventArgs("Failed to open //blp/refdata"));
                return false;
            }
            //OnBBHTDUpdate(new BBHTDEventArgs("Connected. Session Open..."));
            return true;
        }

        public bool SendHistTickDataRequest()
        {
            if (Session != null)
            {
                requestPointer = 0;
                sendRequest(TickDataQueries[requestPointer]);
                requestPointer++;
                //OnBBHTDUpdate(new BBHTDEventArgs("Begining Requests"));
                return true;
            }


            return false;

        }

        private void sendRequest(TickDataQuery tickDataQueries)
        {

        }

        private void processEvent(Event eventObj, Session session)
        {
            switch (eventObj.Type)
            {
                case Event.EventType.RESPONSE:
                    // process final respose for request
                    // processRequestDataEvent(eventObj, session);
                    ///OnBBHTDUpdate(new BBHTDEventArgs("RESPONSE", requestPointer - 1));
                    break;
                case Event.EventType.PARTIAL_RESPONSE:
                    // process partial response
                    // processRequestDataEvent(eventObj, session);
                    //OnBBHTDUpdate(new BBHTDEventArgs("PARTIAL_RESPONSE", requestPointer - 1));
                    break;
                default:
                    // processMiscEvents(eventObj, session);
                    break;
            }
        }
 
    }
}
