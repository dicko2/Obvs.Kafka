using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Serialization;
using Obvs.Kafka.Configuration;
using Obvs.Kafka.Serialization;
using Obvs.Serialization;

namespace Obvs.Kafka
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        private readonly KafkaConfiguration _kafkaConfiguration;
        private readonly string _topic;
        private readonly KafkaProducerConfiguration _producerConfig;
        private readonly IMessageSerializer _serializer;
        private readonly Func<TMessage, Dictionary<string, string>> _propertyProvider;

        private IDisposable _disposable;
        private bool _disposed;
        private long _connected;
        private Producer<string, KafkaHeaderedMessage> _producer;

        public MessagePublisher(KafkaConfiguration kafkaConfiguration, KafkaProducerConfiguration producerConfig, string topic, IMessageSerializer serializer, Func<TMessage, Dictionary<string, string>> propertyProvider)
        {
            _kafkaConfiguration = kafkaConfiguration;
            _topic = topic;
            _serializer = serializer;
            _propertyProvider = propertyProvider;
            _producerConfig = producerConfig;
            Connect();
        }

        public Task PublishAsync(TMessage message)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("Publisher has been disposed already.");
            }

            return Publish(message);
        }

        private Task Publish(TMessage message)
        {
            var properties = _propertyProvider != null ? _propertyProvider(message) : null;

            return Publish(message, properties);
        }

        private async Task Publish(TMessage message, Dictionary<string, string> properties)
        {
            if (_disposed)
            {
                return;
            }

            var kafkaHeaderedMessage = CreateKafkaHeaderedMessage(message, properties);

            await _producer.ProduceAsync(_topic,"", kafkaHeaderedMessage);
        }

        private KafkaHeaderedMessage CreateKafkaHeaderedMessage(TMessage message, Dictionary<string, string> properties)
        {
            byte[] payload;
            using (var stream = new MemoryStream())
            {
                _serializer.Serialize(stream, message);
                payload = stream.ToArray();
            }

            return new KafkaHeaderedMessage
            {
                PayloadType = message.GetType().Name,
                Properties = properties,
                Payload = payload
            };
        }

        private void Connect()
        {
            if (Interlocked.CompareExchange(ref _connected, 1, 0) == 0)
            {
                var configProducer = new Dictionary<string, object> { { "bootstrap.servers", _kafkaConfiguration.SeedAddresses }
                    //,{ "auto.create.topics.enable", true }
                };
                
                _producer = new Producer<string,KafkaHeaderedMessage>(configProducer, 
                    new StringSerializer(Encoding.UTF8),
                    new KafkaHeaderedMessageSerializer());

                _disposable = Disposable.Create(() =>
                {
                    _disposed = true;
                    _producer.Flush(5);
                    _producer.Dispose();
                });
            }
        }

        public void Dispose()
        {
            if (_disposable != null)
            {
                _disposable.Dispose();
            }
        }
    }
}