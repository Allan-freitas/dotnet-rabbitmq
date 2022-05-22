# Work Queues (Task Queue)

## Contents

- [Overview](#overview)
- [Características](#Características)
- [Fluxo](#Fluxo)
- [Exemplo de Solução](#Exemplo-de-Solução)
- [Rodando o exemplo](#Rodando o exemplo)

---

## Overview

- Uma _Fila de Trabalho_ é usada para distribuir tarefas entre vários trabalhadores.
- Normalmente, seria usado um _Work Queue_ para descarregar um trabalho ou tarefa para ser processado de forma assíncrona.
- Um _Producer_ envia uma mensagem para um _Exchange_ que roteia a _Message_ para um _Queue_ nomeado. A _Queue_ pode ter vários _Subscribers/Consumers_ conhecidos como _Workers_ que terão um turno (usando uma estratégia de _Round Robin_ por padrão) para processar uma mensagem.

---

## Características

- Um nome vazio é especificado para o _Exchange_. Uma _Exchange_ com um nome vazio é chamada de exchange padrão ou sem nome.
   - _Exchange_ = "" (troca sem nome ou padrão)
- Uma _Routing Key_ especificada é idêntica ao nome da _Queue_ para a qual está roteando
   - _Chave de roteamento_ = _Nome da fila_
- Uma fila _Transient_ ou _Durable_ pode ser usada
- Vários _Consumers (Workers)_ escutam mensagens na mesma fila
- Usa a estratégia _Round Robin_ por padrão para despachar mensagens para _Workers_. Um _Despacho Justo_ pode ser configurado

---

## Fluxo

![rmq-work-queue](https://github.com/allansud/dotnet-rabbitmq/blob/main/doc/worker_queues.png?raw=true)

- Um _Producer_ envia uma mensagem (contendo a chave de roteamento) para um _Exchange_.
- Um _Binding_ é criado entre _Queue_ e _Exchange_ usando uma _Routing Key_.
- Após as mensagens serem recebidas pela exchange, as mensagens são roteadas para a mensagem _Queue_
- _Consumidores (Trabalhadores)_ escutam a _Fila_ para mensagens.
- Cada mensagem é despachada para um _Consumer (Worker)_ usando _Round Robin_ dispatching (padrão) ou _Fair_ dispatching (contagem de pré-busca = 1)
- Quando um _Worker_ conclui o processamento da mensagem, ele envia um _Acknowledgement (ACK)_

---

## Exemplo de Solução

Existem 2 partes para a solução. Um _Produtor_ e um _Consumidor_. O _Producer_ é um aplicativo .Net Core Console que envia _trades_ para uma fila em um intervalo específico. O _Consumer_ é um aplicativo .NET Core Console que aguarda e consome mensagens à medida que as mensagens chegam à fila.

As duas seções a seguir, _Producer_ e _Consumer_, destacam o código necessário para interagir com _RabbitMQ_

### Consumidor (Consumer)

#### Step 1 - Criar a conexão

```csharp
var connectionFactory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "admin",
    Password = "password"
};

using var connection = connectionFactory.CreateConnection();
```

#### Step 2 - Criar o canal (Channel)

```csharp
using var channel = connection.CreateModel();

// Round Robin dispatching is used by default
// Uncomment the following code to enable Fair dispatch
// channel.BasicQos(
//     prefetchSize: 0,
//     prefetchCount: 1,
//     global: false);
```

#### Step 3 - Declare a Fila (Queue)

```csharp
var queue = channel.QueueDeclare(
    queue: "example2_signals_queue",
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: ImmutableDictionary<string, object>.Empty);
```

#### Step 4 - Criar o consumidor (Consumer)

```csharp
var consumer = new EventingBasicConsumer(channel);

consumer.Received += (sender, eventArgs) =>
{
    var messageBody = eventArgs.Body.ToArray();
    var signal = Signal.FromBytes(messageBody);

    DisplayInfo<Signal>
        .For(signal)
        .SetExchange(eventArgs.Exchange)
        .SetQueue(queue)
        .SetRoutingKey(eventArgs.RoutingKey)
        .SetVirtualHost(connectionFactory.VirtualHost)
        .Display(Color.Yellow);

    DecodeSignal(signal);

    channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
};
```

#### Step 5 - Consumindo as mensagens (Consume Messages)

```csharp
channel.BasicConsume(
    queue: queue.QueueName,
    autoAck: false,
    consumer: consumer);
```

#### Step 6 - Enviar Acknowledgements (ACKS)

```csharp
channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
```

#### Lista Completa

```csharp
internal class Program
{
    private static void Main()
    {
        Console.WriteLine($"\nEXAMPLE 2 : WORK QUEUE : CONSUMER");

        var connectionFactory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "admin",
            Password = "password"
        };

        using var connection = connectionFactory.CreateConnection();

        using var channel = connection.CreateModel();

        // Round Robin dispatching is used by default
        // Uncomment the following code to enable Fair dispatch
        // channel.BasicQos(
        //     prefetchSize: 0,
        //     prefetchCount: 1,
        //     global: false);

        var queue = channel.QueueDeclare(
            queue: "example2_signals_queue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: ImmutableDictionary<string, object>.Empty);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (sender, eventArgs) =>
        {
            var messageBody = eventArgs.Body.ToArray();
            var signal = Signal.FromBytes(messageBody);

            DisplayInfo<Signal>
                .For(signal)
                .SetExchange(eventArgs.Exchange)
                .SetQueue(queue)
                .SetRoutingKey(eventArgs.RoutingKey)
                .SetVirtualHost(connectionFactory.VirtualHost)
                .Display(Color.Yellow);

            DecodeSignal(signal);

            channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        };

        channel.BasicConsume(
            queue: queue.QueueName,
            autoAck: false,
            consumer: consumer);

        Console.ReadLine();
    }

    private static void DecodeSignal(Signal signal)
    {
        Console.WriteLine($"\nDECODE STARTED: [ TX: {signal.TransmitterName}, ENCODED DATA: {signal.Data} ]".Pastel(Color.Lime));

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var decodedData = Receiver.DecodeSignal(signal);

        stopwatch.Stop();

        Console.WriteLine($@"DECODE COMPLETE: [ TIME: {stopwatch.Elapsed.Seconds} sec, TX: {signal.TransmitterName}, DECODED DATA: {decodedData} ]".Pastel(Color.Lime));
    }
}
```

### Produtor (Producer)

#### Step 1 - Criar a Conexão

```csharp
var connectionFactory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "admin",
    Password = "password"
};

using var connection = connectionFactory.CreateConnection();
```

#### Step 2 - Criar o canal (Channel)

```csharp
using var channel = connection.CreateModel();
```

#### Step 3 - Declare a fila (Queue)

```csharp
var queue = channel.QueueDeclare(
    queue: QueueName,
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: ImmutableDictionary<string, object>.Empty);
```

#### Step 4 - Crie e publique a mensagem

```csharp
var signal = Transmitter.Fake().Transmit();

channel.BasicPublish(
    exchange: ExchangeName,
    routingKey: QueueName,
    body: Encoding.UTF8.GetBytes(signal.ToJson())
);
```

#### Lista Completa

```csharp
namespace Rabbit.Example2.Producer
{
    internal class Program
    {
        internal static async Task Main()
        {
            Console.WriteLine("\nEXAMPLE 1 : WORK QUEUES : PRODUCER");

            const string ExchangeName = "";
            const string QueueName = "example2_signals_queue";

            var connectionFactory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "admin",
                Password = "password"
            };

            using var connection = connectionFactory.CreateConnection();

            using var channel = connection.CreateModel();

            var queue = channel.QueueDeclare(
                queue: QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: ImmutableDictionary<string, object>.Empty);

            while (true)
            {
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

                await Task.Delay(millisecondsDelay: 5000);
            }
        }
    }
}
```

---

## Rodando o exemplo

### Repositório do código fonte

Todo o código necessário para executar este exemplo pode ser encontrado em [Github](https://github.com/allansud/dotnet-rabbitmq)

```bash

git clone https://github.com/allansud/dotnet-rabbitmq.git

```

### Manage RabbitMQ Server

Por exemplo, o RabbitMQ é hospedado em um contêiner _Docker_.

O repositório de código de exemplo inclui um arquivo _'docker-compose'_ que descreve a pilha RabbitMQ com um conjunto razoável de padrões. Use _docker-compose_ para iniciar, parar e exibir informações sobre a pilha RabbitMQ da seguinte forma:

```bash
# Verifique se o 'docker-compose' está instalado
docker-compose --version

# Inicie a pilha RabbitMQ em segundo plano
docker-compose up --detach

# Verifique se o contêiner RabbitMQ está em execução
docker-compose ps

# Exibir logs do RabbitMQ
docker-compose logs

# Exibir e seguir os logs do RabbitMQ
docker-compose logs --tail="all" --follow

# Derrube a pilha do RabbitMQ
# Remova os volumes nomeados declarados no `volumes`
# seção do arquivo Compose e volumes anônimos
# anexado ao contêiner
docker-compose down --volumes
```

### Inicie o produtor (Producer)

```bash

# open new terminal and run the following command
dotnet run -p ./Example2/Rabbit.Example2.Producer/

```

### Inicie Worker 1

```bash

# open new terminal and run the following command
dotnet run -p ./Example2/Rabbit.Example2.Consumer/

```

### Inicie Worker 2

```bash

# open new terminal and run the following command
dotnet run -p ./Example2/Rabbit.Example2.Consumer/

```

### Inicie Worker 3

```bash

# open new terminal and run the following command
dotnet run -p ./Example2/Rabbit.Example2.Consumer/

```