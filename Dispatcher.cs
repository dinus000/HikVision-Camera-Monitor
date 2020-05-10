using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Xml;

namespace MotionMonitor
{
    internal class Dispatcher : IDisposable
    {
        private const double DEFAULT_EVENTS_TIMER_INTERVAL = 4000;
        private double _eventsTimeOut = DEFAULT_EVENTS_TIMER_INTERVAL;
        private const string EVENT_TYPE = "eventType";
        private const string EVENT_STATE = "eventState";
        private const string EVENT_COUNTER = "activePostCount";
        private const string EVENT_BOUNDARY = "--boundary";
        private IDictionary<string, int> _allEvents = null;
        private IDictionary<string, int> _eventStates = null;
        private IEnumerable<SubscriptionEventsConfig> _subscriptionEventsConfig;
        private IDictionary<string, string> _matchLines = new Dictionary<string, string>();
        private IDictionary<long, CameraEvent> _partialEvents = new Dictionary<long, CameraEvent>();
        private IDictionary<string, CameraEvent> _completeEvents = new Dictionary<string, CameraEvent>();
        private EmailHelper _emailHelper;
        private Timer eventManager;
        private readonly object sync = new object();

        internal Dispatcher(IEnumerable<SubscriptionEventsConfig> subscriptionEventsConfig)
        {
            _eventsTimeOut = AppConfig.GetEventsTimtout() * 1000; // value in miliseconds
            _allEvents = SqlHelper.GetAllEventTypes();
            _eventStates = SqlHelper.GetAllEventStates();
            _subscriptionEventsConfig = subscriptionEventsConfig;
            _matchLines = _subscriptionEventsConfig.ToDictionary(s => $"<{EVENT_TYPE}>{s.Event}</{EVENT_TYPE}>".ToUpper(), s => s.Event);
            double tiemrInterval = _eventsTimeOut > 0 ? _eventsTimeOut * 4 : DEFAULT_EVENTS_TIMER_INTERVAL;
            _emailHelper = new EmailHelper();
            eventManager = new Timer(tiemrInterval);
            eventManager.Elapsed += ProcessEvents;
            eventManager.Start();
        }

        private void ProcessEvents(object sender, ElapsedEventArgs e)
        {
            lock (sync)
            {
                // Even though the body of this method is protected against reentries, the _completeEvents may be changed externally so let's cache it.
                string[] eventKeys = _completeEvents.Keys.ToArray();
                long timeNow = Utils.GetTimeStampMs();
                foreach (string id in eventKeys)
                {
                    var cmd = _completeEvents[id];

                    if (!ShouldBeReported(cmd))
                    {
                        _completeEvents.Remove(id);
                        continue;
                    }
                    Logger.Debug($"[Dispatcher:ProcessEvents] Processing event {cmd.EventType}");
                    if (timeNow - cmd.TimeEnd > _eventsTimeOut)
                    {
                        // We're removing events before processing them. This potential may lead to cases when an event has been removed but failed to be processed further.
                        // This is intended behavior as overwise the events would pile up. We will log detailed erorrs if there're any.
                        _completeEvents.Remove(id);
                        foreach(EventAction a in cmd.Actions)
                        {
                            Logger.Debug($"[Dispatcher:ProcessEvents] Processing event {cmd.EventType}, action {a}");
                            switch (a)
                            {
                                case EventAction.Store:
                                    StoreEvent(cmd);
                                    break;
                                case EventAction.Email:
                                    _emailHelper.Send(cmd);
                                    break;
                                default:
                                    Logger.Error($"[Dispatcher:ProcessEvents] Unknown action: {a}");
                                    break;
                            }
                        }
                    }
                }
            }
        }

        // Processes the line of data which is in XML format but not always.
        // Since we're dealing with a tiny docs, we can save lots of resources by matching some lines instead of blindly parsing all the received XML stream.
        // Note that received data usually contains only one set of information (event, state ot else).
        internal void Process(string dataLine, long id)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
            {
                return;
            }

            string ipAddress = Utils.LongToIpAddress(id);

            // If event boundary is received then the event object should be complete. Remove any parts of it.
            if (dataLine.Equals(EVENT_BOUNDARY, StringComparison.OrdinalIgnoreCase) && _partialEvents.ContainsKey(id))
            {
                _partialEvents.Remove(id);
                return;
            }

            if (!_partialEvents.ContainsKey(id))
            {
                // Add an empty event so we can track its state later
                var cameraEvent = new CameraEvent(ipAddress);
                _partialEvents.Add(id, cameraEvent);
            }

            var partialEvent = _partialEvents[id];

            // Check if received data contains an event we're subscribed to
            string key = dataLine.ToUpper();
            if (_matchLines.ContainsKey(key))
            {
                partialEvent.EventType = _matchLines[key];
                var eventconfig = _subscriptionEventsConfig.FirstOrDefault(e => e.Event == _matchLines[key]);
                if (eventconfig == null || !eventconfig.Actions.Any(a => a != EventAction.None))
                {
                    // Should never happen as the event subscriptions should be validated in AppConfig class.
                    Logger.Warning($"[Dispatcher:Process] Unknown error. Could not find the proper event action.");
                }
                partialEvent.Actions = eventconfig.Actions;
                _partialEvents[id] = partialEvent;
                SaveIfComplete(partialEvent, id);

                return;
            }

            // Check if received data is event state
            if (dataLine.IndexOf(EVENT_STATE, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var rawEventState = ParseSingleAttribute(dataLine, EVENT_STATE);
                if (string.IsNullOrWhiteSpace(rawEventState))
                {
                    // Parsing function takes care of the error message
                    return;
                }

                if (!_eventStates.ContainsKey(rawEventState))
                {
                    Logger.Warning($"[Dispatcher:Process] The following state is not present in database: {rawEventState}. Please register it manually.");
                    return;
                }

                partialEvent.EventState = _eventStates[rawEventState];
                partialEvent.RawEventState = rawEventState;
                _partialEvents[id] = partialEvent;
                SaveIfComplete(partialEvent, id);

                return;
            }
        }

        private string ParseSingleAttribute(string line, string tag)
        {
            XmlDocument xml = new XmlDocument();
            try
            {
                xml.LoadXml(line);
                var elements = xml.GetElementsByTagName(tag);
                if (elements.Count != 1)
                {
                    Logger.Error($"[Dispatcher:Process] Failed to process the following data line: {line}.");
                    return null;
                }

                return elements[0].InnerText.ToUpper();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Dispatcher:ParseSingleAttribute] message: {ex.Message}.");
                return null;
            }
        }
        
        private void SaveIfComplete(CameraEvent cameraEvent, long id)
        {
            if (IsEventComplete(cameraEvent))
            {
                string completeEventKey = CreateCompleteEventKey(cameraEvent, id);
                if (!_completeEvents.ContainsKey(completeEventKey))
                {
                    cameraEvent.TimeEnd = Utils.GetTimeStampMs();
                    _completeEvents.Add(completeEventKey, cameraEvent);
                    Logger.Debug($"[Dispatcher:SaveIfComplete] Added complete event: Type {_completeEvents[completeEventKey].EventType}, duration {_completeEvents[completeEventKey].TimeEnd - _completeEvents[completeEventKey].TimeStart}.");
                }
                else
                {
                    var completeEvent = _completeEvents[completeEventKey];
                    completeEvent.Count++;
                    completeEvent.TimeEnd = Utils.GetTimeStampMs();
                    _completeEvents[completeEventKey] = completeEvent;
                    Logger.Debug($"[Dispatcher:SaveIfComplete] Added complete event: Type {_completeEvents[completeEventKey].EventType}, duration {_completeEvents[completeEventKey].TimeEnd - _completeEvents[completeEventKey].TimeStart}.");
                }
            }
        }
        
        private bool StoreEvent(CameraEvent cameraEvent)
        {
            string cmdType = cameraEvent.EventType.ToUpper();
            if (!_allEvents.ContainsKey(cmdType))
            {
                SqlHelper.CreateEvent(cameraEvent);
                _allEvents = SqlHelper.GetAllEventTypes();
            }

            return SqlHelper.AddEvent(cameraEvent, _allEvents[cmdType]);
        }

        private string CreateCompleteEventKey(CameraEvent cameraEvent, long id)
        {
            // It may happen that we have several events of different types stored for the same device.
            return $"{id}_{cameraEvent.EventType}";
        }

        private bool ShouldBeReported(CameraEvent cameraEvent)
        {
            var eventconfig = _subscriptionEventsConfig.FirstOrDefault(e => e.Event == cameraEvent.EventType);
            
            return (!eventconfig.FilterList.Any() || eventconfig.FilterList.Any(f => f.Equals(cameraEvent.RawEventState, StringComparison.OrdinalIgnoreCase))) 
                && (eventconfig.IgnoreList.Any() || !eventconfig.IgnoreList.Any(f => f.Equals(cameraEvent.RawEventState, StringComparison.OrdinalIgnoreCase)));
        }

        public bool IsEventComplete(CameraEvent cameraEvent)
        {
            return !string.IsNullOrWhiteSpace(cameraEvent.IpAddress) && !string.IsNullOrWhiteSpace(cameraEvent.EventType) && cameraEvent.EventState > 0;
        }

        public void Dispose()
        {
            _emailHelper = null;
            eventManager.Stop();
            eventManager.Elapsed -= ProcessEvents;
        }
    }
}
