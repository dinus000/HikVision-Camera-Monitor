using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionMonitor
{
    internal class CameraEvent
    {
        public string EventType { get; set;  }
        public int EventState { get; set; }
        public string RawEventState { get; set; }
        public string IpAddress { get; }
        public int Count { get; set; }
        public long TimeStart { get; set; }
        public long TimeEnd { get; set; }
        public DateTime EventStart { get; set; }
        public IEnumerable<EventAction> Actions { get; set; }

        // Empty event
        public CameraEvent(string ipAddress)
        {
            IpAddress = ipAddress;
            EventState = 0;
            RawEventState = "";
            EventType = string.Empty;
            TimeStart = Utils.GetTimeStampMs();
            TimeEnd = Utils.GetTimeStampMs();
            EventStart = DateTime.Now;
        }
    }

    internal class SqlHelper
    {
        private static readonly string _conectionString = AppConfig.GetConnectionString();
        private const string ADDEVENT_SP = "sp_addCameraEvent";
        private const string ADDEVENT_WITHTIMECORRECTION_SP = "sp_addCameraEventWithTimeCorrection";
        private const string GETALLEVENTS_SP = "sp_getAllCameraEvents";
        private const string GETALLSTATES_SP = "sp_getAllEventStates";
        private const string CREATE_EVENT = "sp_createCameraEvent";
        private const string ID = "ID";
        private const string NAME = "Name";
        internal static bool AddEvent(CameraEvent cameraEvent, int id)
        {
            SqlConnection connection = null;
            try
            {
                connection = new SqlConnection(_conectionString);
                SqlCommand command = new SqlCommand(ADDEVENT_SP, connection);
                command.CommandType = CommandType.StoredProcedure;
                double duration = (double)(cameraEvent.TimeEnd - cameraEvent.TimeStart) / 1000.00;
                command.Parameters.Add("@idEvent", SqlDbType.Int).Value = id;
                command.Parameters.Add("@eventType", SqlDbType.VarChar).Value = cameraEvent.EventType;
                command.Parameters.Add("@eventState", SqlDbType.Int).Value = cameraEvent.EventState;
                command.Parameters.Add("@ipAddress", SqlDbType.VarChar).Value = cameraEvent.IpAddress;
                command.Parameters.Add("@duration", SqlDbType.Int).Value = duration;
                command.Parameters.Add("@triggeredDateTime", SqlDbType.DateTime).Value = cameraEvent.EventStart;

                connection.Open();
                int rowsAffected = command.ExecuteNonQuery();

                return rowsAffected == 1; // expect to insert exactly one row
            }
            catch (Exception ex)
            {
                Logger.Error($"[SqlHelper:AddEvent] message: {ex.Message}");
                return false;
            }
            finally
            {
                connection.Close();
            }
        }

        internal static void CreateEvent(CameraEvent cameraEvent)
        {
            SqlConnection connection = null;
            try
            {
                connection = new SqlConnection(_conectionString);
                SqlCommand command = new SqlCommand(CREATE_EVENT, connection);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@Name", SqlDbType.VarChar).Value = cameraEvent.EventType;

                connection.Open();
                int rowsAffected = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Logger.Error($"[SqlHelper:CreateEvent] message: {ex.Message}");
            }
            finally
            {
                connection.Close();
            }
        }

        internal static Dictionary<string, int> GetAllEventTypes()
        {
            var events = new Dictionary<string, int>();
            SqlConnection connection = null;
            SqlDataReader reader = null;

            try
            {
                connection = new SqlConnection(_conectionString);
                SqlCommand command = new SqlCommand(GETALLEVENTS_SP, connection);
                command.CommandType = CommandType.StoredProcedure;

                connection.Open();
                reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader[ID]);
                    string name = reader[NAME].ToString().ToUpper();
                    if (events.ContainsValue(id) || events.ContainsKey(name))
                    {
                        throw new ArgumentException("Event names and IDs must be unique");
                    }
                    events.Add(name, id);
                }

                return events;
            }
            catch (Exception ex)
            {
                Logger.Error($"[SqlHelper:GetAllEventTypes] message: {ex.Message}");
                return null;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }

                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        internal static Dictionary<string, int> GetAllEventStates()
        {
            var states = new Dictionary<string, int>();
            SqlConnection connection = null;
            SqlDataReader reader = null;

            try
            {
                connection = new SqlConnection(_conectionString);
                SqlCommand command = new SqlCommand(GETALLSTATES_SP, connection);
                command.CommandType = CommandType.StoredProcedure;

                connection.Open();
                reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                while (reader.Read())
                {
                    int id = Convert.ToInt32(reader[ID]);
                    string name = reader[NAME].ToString().ToUpper();
                    if (states.ContainsValue(id) || states.ContainsKey(name))
                    {
                        throw new ArgumentException("State names and IDs must be unique");
                    }
                    states.Add(name, id);
                }

                return states;
            }
            catch (Exception ex)
            {
                Logger.Error($"[SqlHelper:GetAllEventStates] message: {ex.Message}");
                return null;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }

                if (connection != null)
                {
                    connection.Close();
                }
            }
        }
    }
}
