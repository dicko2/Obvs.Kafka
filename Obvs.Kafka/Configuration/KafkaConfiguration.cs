using System.Linq;
using System.Net;

namespace Obvs.Kafka.Configuration
{
    public class KafkaConfiguration
    {
        public KafkaConfiguration(string groupId, params IPEndPoint[] seedAddresses)
            : this(string.Join(",", seedAddresses.Select(ip => ip.ToString())), groupId)
        {
        }

        public KafkaConfiguration(string connectionString, string groupId)
        {
            GroupId = groupId;
            SeedAddresses = connectionString;
        }

        public string SeedAddresses { get; }

        public string GroupId { get; }
    }
}