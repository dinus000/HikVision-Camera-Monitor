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
        private const int MIN_FREQUENCY = 60 * 1000; // 1 minute
        private EmailConfig _emailConfig = null;
        private int _maxFrequency = 0;
        private const string EMAIL_NAME = "Motion Monitoring";
        private long _lastSentTimeStamp = 0;

        public EmailHelper()
        {
            _emailConfig = AppConfig.GetEmailConfig();
            _maxFrequency = AppConfig.GetMaxEmailFrequency();
            if (_maxFrequency < MIN_FREQUENCY)
            {
                _maxFrequency = MIN_FREQUENCY;
            }
        }
        internal void Send(CameraEvent cameraEvent)
        {
            if (Utils.GetTimeStampMs() - _lastSentTimeStamp < _maxFrequency)
            {
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
            finally
            {
                _lastSentTimeStamp = Utils.GetTimeStampMs();
            }
        }
    }
}
