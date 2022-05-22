using System.Collections.Immutable;
using System.Drawing;
using System.Text;
using Rabbitmq.Common.Data.Signals;
using Rabbitmq.Common.Display;
using RabbitMQ.Client;

namespace Rabbitmq.Casa2.Producer
{
    internal sealed class Program
    {
        private static async Task Main()
        {
            Console.WriteLine("\nEXAMPLE 1 : WORK QUEUE : PRODUCER");

            var connectionFactory = new ConnectionFactory
            {
                HostName = "192.168.88.20",
                UserName = "guest",
                Password = "guest"
            };

            using var connection = connectionFactory.CreateConnection();

            using var channel = connection.CreateModel();

            const string ExchangeName = "";

            const string QueueName = "example2_signals_queue";

            var queue = channel.QueueDeclare(
                queue: QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: ImmutableDictionary<string, object>.Empty);


            var signal = Transmitter.Fake().Transmit();

            channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: QueueName,
                body: Encoding.UTF8.GetBytes(signal.ToJson())
            );

            DisplayInfo<Signal>
                .For(signal)
                .SetExchange(ExchangeName)
                .SetQueue(QueueName)
                .SetRoutingKey(QueueName)
                .SetVirtualHost(connectionFactory.VirtualHost)
                .Display(Color.Cyan);

            await Task.Delay(millisecondsDelay: 3000);

        }
    }
}
