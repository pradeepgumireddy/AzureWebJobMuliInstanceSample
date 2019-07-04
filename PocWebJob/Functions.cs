using Microsoft.ApplicationInsights;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PocWebJob.ConfigSettings;
using PocWebJob.Utilities;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace PocWebJob
{
    public class Functions
    {
        private TelemetryClient _tclient;
        private IOptions<ServiceBusSettings> _serviceBusSettings;
        private IOptions<CommonSettings> _commonSettings;

        private static ISessionClient _sessionClient;
        IMessageSession session;

        private int _processID;
        private int _threadID;
        private string _instanceID = string.Empty;
        public Functions(IConfiguration configuration)
        {
            _tclient = new TelemetryClient();
            _tclient.Context.Operation.Id = Guid.NewGuid().ToString();
            _tclient.Context.Operation.Name = "PocWebJob9090";
            _serviceBusSettings = Options.Create(configuration.GetSection("ServiceBusSettings").Get<ServiceBusSettings>());
            _commonSettings = Options.Create(configuration.GetSection("CommonSettings").Get<CommonSettings>());
            _sessionClient = new SessionClient(_serviceBusSettings.Value.Connection, _serviceBusSettings.Value.PullQueueName);
        }
        public async Task ProcessMessagesFromQueue([TimerTrigger("0/01 * * * * *")]TimerInfo timerInfo)//TextWriter log
        {
            //while (true)
            //{
            //    string instanceid = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
            //    Console.WriteLine($"InstanceID:{ instanceid}");
            //    for (int i = 0; i <= 5; i++)
            //    {
            //        Console.WriteLine($"Count:{ i}");
            //    }
            //    _tClient.TrackTrace($"InstanceID:{ instanceid}");
            //    Thread.Sleep(5000);
            //}
            await ProcessMessagesFromQueueSession();
        }

        private async Task ProcessMessagesFromQueueSession()
        {
            try
            {
                _processID = System.Diagnostics.Process.GetCurrentProcess().Id;
                _threadID = Thread.CurrentThread.ManagedThreadId;
                _instanceID = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
                //_tclient.TrackTrace($"InstanceID:{ _instanceid}");

                #region receive the sessions from wo-xml and process the messages
                _sessionClient.OperationTimeout = new TimeSpan(0, 0, 5);
                //Console.WriteLine($"Start Receiving Session");
                _tclient.TrackTrace($"InstanceID:{ _instanceID}, Start Receiving Session");
                session = await _sessionClient.AcceptMessageSessionAsync();

                if (session != null)
                {
                    // Messages within a session will always arrive in order.
                    _tclient.TrackTrace($"InstanceID:{ _instanceID}, Received Session: [{session.SessionId}]");

                    while (true)
                    {
                        //Read All the messages from sessions
                        Message message = await session.ReceiveAsync();
                        if (message != null)
                        {
                            MemoryStream woBody = new MemoryStream(message.Body);
                            XmlDocument xmlDoc = new XmlDocument();
                            xmlDoc.Load(woBody);
                            var wordOrderID = WOCommonUtility.GetNodeValue(xmlDoc, "Source_WO_ID");
                            var workOrderPriority = WOCommonUtility.GetNodeValue(xmlDoc, "WOPriority");
                            var workOrderSequence = WOCommonUtility.GetNodeValue(xmlDoc, "QueueSequence");
                            var workOrderPostedDateTime = WOCommonUtility.GetNodeValue(xmlDoc, "PostedDateTime");


                            string query = $"Insert into tblQueueLog (ProcessedBy,WorkOrder,Sequence,Priority,PostedTime,ProcessedTime,ProcessID,ThreadID, InstanceId) " +
                                                      $"values ('PocWebJob9090', '{wordOrderID}', '{workOrderSequence}', '{workOrderPriority}','{workOrderPostedDateTime}' , '{DateTime.UtcNow}','{_processID}','{_threadID }','{_instanceID}')";
                            await LogMesssagesInDb(query, _commonSettings.Value.SQLDBConnectionString);

                            // Complete the message so that it is not received again.
                            // This can be done only if the queueClient is created in ReceiveMode.PeekLock mode (which is default).
                            await session.CompleteAsync(message.SystemProperties.LockToken);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    //Console.WriteLine($"No any session available in queue to process from WebJob.");
                }
            }
            catch (ServiceBusTimeoutException timeOutEx)
            {
                string query = $"Insert into tblQueueLog (ProcessedBy,ProcessedTime,ProcessID,ThreadID, InstanceId, ExceptionDetails ) " +
               $"values ('PocWebJob9090', '{DateTime.UtcNow}','{_processID}','{_threadID}','{_instanceID}','Exception Type: ServiceBusTimeoutException, Queue session timed out')";
                await LogMesssagesInDb(query, _commonSettings.Value.SQLDBConnectionString);

                _tclient.TrackException(timeOutEx, new Dictionary<string, string>()
                        {
                            { "ApplicationName","PocWebJob9090"},{ "ModuleName", "Functions"},{"Method","ProcessMessagesFromQueue" },
                            { "ServiceBusTimeoutException","Service bus session timeout, No sessions in FIFO order."},
                            { "InstanceID",_instanceID},{ "Message",timeOutEx.Message}
                        });
            }
            catch (SessionCannotBeLockedException lockedEx)
            {
                string query = $"Insert into tblQueueLog (ProcessedBy,ProcessedTime,ProcessID,ThreadID, InstanceId,ExceptionDetails ) " +
              $"values ('PocWebJob9090', '{DateTime.UtcNow}','{_processID}','{_threadID }','{_instanceID}','Exception Type: SessionCannotBeLockedException, The requested session cannot be accepted. It may be locked by another receiver.')";
                await LogMesssagesInDb(query, _commonSettings.Value.SQLDBConnectionString);

                _tclient.TrackTrace($"The requested session cannot be accepted. It may be locked by another receiver.");

                _tclient.TrackException(lockedEx, new Dictionary<string, string>()
                        {
                            { "ApplicationName","PocWebJob9090"},{ "ModuleName", "Functions"},{"Method","ProcessMessagesFromQueue" },
                            { "SessionCannotBeLockedException","The requested session cannot be accepted. It may be locked by another receiver."},{ "Message",lockedEx.Message},
                            { "InstanceID",_instanceID}
                        });
            }
            catch (Exception ex)
            {
                string query = $"Insert into tblQueueLog (ProcessedBy,ProcessedTime,ProcessID,ThreadID,InstanceId,ExceptionDetails ) " +
                $"values ('PocWebJob9090', '{DateTime.UtcNow}','{_processID}','{_threadID }','{_instanceID}','Exception Type: Exception, exception Occurred while going to fetch message from queue.')";
                await LogMesssagesInDb(query, _commonSettings.Value.SQLDBConnectionString);

                _tclient.TrackException(ex, new Dictionary<string, string>()
                        {
                            { "ApplicationName","PocWebJob9090"},{ "ModuleName", "Functions"},{"Method","ProcessMessagesFromQueue" },
                            { "InstanceID",_instanceID},{ "Message",ex.Message}
                        });
            }
            #endregion
        }
        public async static Task LogMesssagesInDb(string query, string connectionString)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    SqlCommand comm = new SqlCommand(query, con);
                    await comm.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                //ToDo for exception
            }

        }
    }
}
