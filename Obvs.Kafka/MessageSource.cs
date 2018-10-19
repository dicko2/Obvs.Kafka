using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Confluent.Kafka;
using Obvs.Kafka.Configuration;
using Obvs.Kafka.Serialization;
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
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var configConsumer = new Dictionary<string, object>
                    {
                        { "bootstrap.servers", _kafkaConfig.SeedAddresses },
                        { "group.id", _kafkaConfig.GroupId },
                        { "enable.auto.commit", true }
                    };

                    var consumer =
                        new Consumer<Ignore, KafkaHeaderedMessage>(configConsumer, null, 
                                        new KafkaHeaderedMessageDeserializer());

                    return Observable.FromEventPattern<Message<Ignore, KafkaHeaderedMessage>>(
                        a => consumer.OnMessage += a,
                        b => consumer.OnMessage -= b)
                        .Where(PassesFilter)
                        .Select(DeserializePayload)
                        .Subscribe(observer);
                });
            }
        }

        private bool PassesFilter(EventPattern<Message<Ignore, KafkaHeaderedMessage>> arg)
        {
            return !_applyFilter || _propertyFilter(arg.EventArgs.Value.Properties);
        }

        private TMessage DeserializePayload(EventPattern<Message<Ignore, KafkaHeaderedMessage>> arg)
        {
            var deserializer = _deserializers[arg.EventArgs.Value.PayloadType];
            return deserializer.Deserialize(new MemoryStream(arg.EventArgs.Value.Payload));
        }

        public void Dispose()
        {
        }
    }
}