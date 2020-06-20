using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionMonitor
{
    public enum EventAction
    {
        None,
        Store,
        Email
    }

    public class CameraConfig
    {
        public string IpAddress { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public class EmailConfig
    {
        public int Port { get; set; }
        public string Host { get; set; }
        public bool UseSSL { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public string ToName { get; set; }
        public string Password { get; set; }
    }

    public class AutomationCommand
    {
        public string Device { get; set; }
        public string Event { get; set; }
        public string Item { get; set; }
        public string Command { get; set; }
    }

    public class SubscriptionEvents
    {
        public string Event { get; set; }
        public string Filter { get; set; }
        public string Ignore { get; set; }
        public string Actions { get; set; }

        public SubscriptionEventsConfig CreateEventsConfig()
        {
            var filterList = new List<string>();
            var ignoreList = new List<string>();
            var actionListRaw = new List<string>();
            var actionList = new List<EventAction>();

            if (string.IsNullOrWhiteSpace(Event))
            {
                Logger.Error($"[SubscriptionEvents:CreateEventsConfig] message: missing Event field for event {Event}");
                return null;
            }
            if (string.IsNullOrWhiteSpace(Actions))
            {
                Logger.Error($"[SubscriptionEvents:CreateEventsConfig] message: missing Action field for event {Event}");
                return null;
            }

            actionListRaw = Actions.Split(',').ToList();
            if (!actionListRaw.Any())
            {
                Logger.Error($"[SubscriptionEvents:CreateEventsConfig] message: at least one action must be specified for event {Event}");
                return null;
            }
            foreach(string a in actionListRaw)
            {
                EventAction action = EventAction.None;
                if (Enum.TryParse(a, out action) && action != EventAction.None)
                {
                    actionList.Add(action);
                }
                else
                {
                    Logger.Error($"[SubscriptionEvents:CreateEventsConfig] the following value is not a valid action: {a}");
                }
            }
            if (!actionList.Any())
            {
                Logger.Error($"[SubscriptionEvents:CreateEventsConfig] message: no valid actinos defined for event {Event}");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(Filter))
            {
                filterList = Filter.Split(',').ToList();
            }

            if (!string.IsNullOrWhiteSpace(Ignore))
            {
                ignoreList = Ignore.Split(',').ToList();
            }

            return new SubscriptionEventsConfig(Event, filterList, ignoreList, actionList);
        }
    }

    public class SubscriptionEventsConfig
    {
        public string Event { get; private set; }
        public IEnumerable<string> FilterList { get; private set; }
        public IEnumerable<string> IgnoreList { get; private set; }
        public IEnumerable<EventAction> Actions { get; private set; }

        public SubscriptionEventsConfig(string eventName, IEnumerable<string> filter, IEnumerable<string> ignore, IEnumerable<EventAction> actions)
        {
            Event = eventName;
            FilterList = filter;
            IgnoreList = ignore;
            Actions = actions;
        }
    }

    internal class AppConfig
    {
        internal static IEnumerable<CameraConfig> GetCameraConfig()
        {
            try
            {
                string configJson = ConfigurationManager.AppSettings["CamConfig"];

                return JsonConvert.DeserializeObject<IEnumerable<CameraConfig>>(configJson);
            }
            catch (Exception ex)
            {
                Logger.Error($"[AppConfig:GetCameraConfig] message: {ex.Message}");
                return null;
            }
        }

        internal static IEnumerable<SubscriptionEventsConfig> GetSubscriptinoEventsConfig()
        {
            try
            {
                string configJson = ConfigurationManager.AppSettings["SubscriptionEvents"];

                var subscriptionEvents =  JsonConvert.DeserializeObject<IEnumerable<SubscriptionEvents>>(configJson);
                var config = subscriptionEvents.Select(e => e.CreateEventsConfig()).Where(e => e != null);

                if (!config.Any())
                {
                    Logger.Error($"[AppConfig:GetSubscriptinoEvents] message: No Events subscription events configuration found.");
                    return null;
                }

                return config;
            }
            catch (Exception ex)
            {
                Logger.Error($"[AppConfig:GetSubscriptinoEvents] message: {ex.Message}");
                return null;
            }
        }

        internal static EmailConfig GetEmailConfig()
        {
            try
            {
                string configJson = ConfigurationManager.AppSettings["EmailCredentials"];

                return JsonConvert.DeserializeObject<EmailConfig>(configJson);
            }
            catch (Exception ex)
            {
                Logger.Error($"[AppConfig:GetEmailConfig] message: {ex.Message}");
                return null;
            }
        }

        internal static int GetMaxEmailFrequency()
        {
            int value = 0;
            string rawValue = ConfigurationManager.AppSettings["MaximumEmailSendingFrequency"];
            if (!int.TryParse(rawValue, out value))
            {
                Logger.Error("[AppConfig:GetMaxEmailFrequency] MaximumEmailSendingFrequency is not a proper value.");
                return 0;
            }

            return value * 60 * 1000; // Convert minutes to miliseconds
        }

        internal static string GetConnectionString()
        {
            return ConfigurationManager.AppSettings["SqlConnectionString"];
        }

        internal static int GetEventsTimtout()
        {
            string rawValue = ConfigurationManager.AppSettings["EventsTimeOut"];
            int value = 0;

            return int.TryParse(rawValue, out value) ? value : 0;
        }

        internal static string GetAutomationHost()
        {
            return ConfigurationManager.AppSettings["AutomationHost"];
        }

        internal static IDictionary<string, AutomationCommand> GetAutomationActions()
        {
            try
            {
                string configJson = ConfigurationManager.AppSettings["AutomationActions"];
                var actions = JsonConvert.DeserializeObject<IEnumerable<AutomationCommand>>(configJson);

                return actions.ToDictionary(a => Utils.GenerateKey(a), a => a);
            }
            catch (Exception ex)
            {
                Logger.Error($"[AppConfig:GetAutomationActions] message: {ex.Message}");
                return null;
            }
        }
    }
}
