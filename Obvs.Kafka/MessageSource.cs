using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Confluent.Kafka;
using Obvs.Kafka.Configuration;
using Obvs.Serialization;
using ProtoBuf;

namespace Obvs.Kafka
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> 
        where TMessage : class
    {
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly KafkaConfiguration _kafkaConfig;
        private readonly string _topicName;
        private readonly Func<Dictionary<string, string>, bool> _propertyFilter;

        private readonly KafkaSourceConfiguration _sourceConfig;
        private readonly bool _applyFilter;
        private ObserverConsumer obscon;
        public MessageSource(KafkaConfiguration kafkaConfig,
            KafkaSourceConfiguration sourceConfig, 
            string topicName,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            Func<Dictionary<string, string>, bool> propertyFilter)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _kafkaConfig = kafkaConfig;
            _topicName = topicName;
            _propertyFilter = propertyFilter;
            _sourceConfig = sourceConfig;
            _applyFilter = propertyFilter != null;
            obscon = new ObserverConsumer(_kafkaConfig.SeedAddresses, _kafkaConfig.GroupId, _topicName);
        }
        
        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer => obscon
                    .Where(PassesFilter)
                    .Select(DeserializePayload)
                    .Subscribe(observer));
            }
        }
        
        private bool PassesFilter(Message<Ignore, KafkaHeaderedMessage> arg)
        {
            return !_applyFilter || _propertyFilter(arg.Value.Properties);
        }

        private TMessage DeserializePayload(Message<Ignore, KafkaHeaderedMessage> arg)
        {
            var deserializer = _deserializers[arg.Value.PayloadType];
            return deserializer.Deserialize(new MemoryStream(arg.Value.Payload));
        }

        public void Dispose()
        {
        }
    }
}