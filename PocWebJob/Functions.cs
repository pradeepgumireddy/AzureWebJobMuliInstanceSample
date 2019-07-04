using Microsoft.ApplicationInsights;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PocWebJob
{
    public class Functions
    {
        private TelemetryClient _tClient;
        //[NoAutomaticTriggerAttribute]
        public Functions(IConfiguration configuration)
        {
            _tClient = new TelemetryClient();
            _tClient.Context.Operation.Id = Guid.NewGuid().ToString();
            _tClient.Context.Operation.Name = "PocWebJob";
        }
        public async Task ProcessMessageFromQueue([TimerTrigger("0/01 * * * * *")]TimerInfo timerInfo)//TextWriter log
        {
            while (true)
            {
                string instanceid = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
                Console.WriteLine($"InstanceID:{ instanceid}");
                for (int i = 0; i <= 5; i++)
                {
                    Console.WriteLine($"Count:{ i}");
                }
                _tClient.TrackTrace($"InstanceID:{ instanceid}");
                Thread.Sleep(5000);
            }
        }
    }
}
