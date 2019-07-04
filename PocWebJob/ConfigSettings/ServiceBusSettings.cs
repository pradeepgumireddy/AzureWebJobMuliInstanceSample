using System;
using System.Collections.Generic;
using System.Text;

namespace PocWebJob.ConfigSettings
{
    public class ServiceBusSettings
    {
        public string Connection { get; set; }
        public string PullQueueName { get; set; }
    }
}
