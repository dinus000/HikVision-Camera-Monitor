using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace MotionMonitor
{
    internal class EmailHelper
    {
        private const int MAX_FREQUENCY = 60 * 1000; // 1 minute
        private EmailConfig _emailConfig = null;
        private int _maxFrequency = 0;
        private const string EMAIL_NAME = "Motion Monitoring";
        private IDictionary<string, long> _lastSent = new Dictionary<string, long>();

        public EmailHelper()
        {
            _emailConfig = AppConfig.GetEmailConfig();
            _maxFrequency = AppConfig.GetMaxEmailFrequency();
            if (_maxFrequency < MAX_FREQUENCY)
            {
                _maxFrequency = MAX_FREQUENCY;
            }
        }
        internal void Send(CameraEvent cameraEvent)
        {
            if (!CanSendEmailNotification(cameraEvent))
            {
                Logger.Debug($"[EmailHelper:Send] Ignoring sending email message.");
                return;
            }
            try
            {
                var from = new MailAddress(_emailConfig.FromAddress, EMAIL_NAME);
                if (from == null)
                {
                    Logger.Error("[EmailHelper:Send] Could not initialize MailAddress.");
                }

                var smtp = new SmtpClient
                {
                    Host = _emailConfig.Host,
                    Port = _emailConfig.Port,
                    EnableSsl = _emailConfig.UseSSL,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(from.Address, _emailConfig.Password)
                };

                double eventDurationInSeconds = (double)(cameraEvent.TimeEnd - cameraEvent.TimeStart) / 1000.00;
                string subject = $"A message from {cameraEvent.IpAddress}";
                string body = $"Message from {cameraEvent.IpAddress}: \r\n Event type: {cameraEvent.EventType} occured {cameraEvent.Count} times.\r\n";
                body += $"Start time: {cameraEvent.EventStart}, Duration: {eventDurationInSeconds}seconds.";
                using (var message = new MailMessage(from.Address, _emailConfig.ToAddress)
                {
                    Subject = subject,
                    Body = body
                })
                {
                    smtp.Send(message);
                }
            }
            catch(Exception ex)
            {
                Logger.Error($"[EmailHelper:Send] message: {ex.Message}.");
            }
        }
        private bool CanSendEmailNotification(CameraEvent cameraEvent)
        {
            string key = Utils.GenerateKey(cameraEvent);

            if (_lastSent.ContainsKey(key))
            {
                bool expired = (Utils.GetTimeStampMs() - _lastSent[key]) < _maxFrequency;
                _lastSent[key] = Utils.GetTimeStampMs();

                return expired;
            }

            _lastSent.Add(key, Utils.GetTimeStampMs());

            return true;
        }
    }
}
