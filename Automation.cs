using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MotionMonitor
{
    internal class Automation
    {
        private const string _TimeStamp = "|TimeStamp|";
        private static HttpClient _client = null;
        private static IDictionary<string, AutomationCommand> _actions = AppConfig.GetAutomationActions();

        internal static async Task SendCommandAsync(string host, AutomationCommand command)
        {
            if (_client == null)
            {
                _client = new HttpClient();
                _client.BaseAddress = new Uri(host);
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
            var request = new HttpRequestMessage(HttpMethod.Post, $"rest/items/{command.Item}");
            string itemCommand = GetItemCommand(command);
            request.Content = new StringContent(itemCommand, Encoding.ASCII, "text/plain");
            var response = await _client.SendAsync(request);
            ValidateResponse(response);
        }

        internal static void Process(string host, CameraEvent cameraEvent)
        {
            string key = Utils.GenerateKey(cameraEvent);
            if (_actions.ContainsKey(key))
            {
                Task.Run(() => SendCommandAsync(host, _actions[key]));
            }
        }

        private static async void ValidateResponse(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return;
            }
            var responseString = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(responseString))
            {
                Logger.Error($"[Automation:ValidateResponse] Automation command failed with code {response.StatusCode}. Message: {responseString}");
            }
        }

        private static string GetItemCommand(AutomationCommand command)
        {
            // Check if item is a reserved word and convert it accordingly
            string itemCommand = command.Command;
            if (!itemCommand.StartsWith("|") || !itemCommand.EndsWith("|"))
            {
                return itemCommand;
            }

            if(itemCommand.Equals(_TimeStamp))
            {
                return DateTime.Now.ToString();
            }

            Logger.Error($"[Automation:GetCommandItem] The following reserved automation command is not recognized: {itemCommand}");

            return string.Empty;
        }
    }
}
