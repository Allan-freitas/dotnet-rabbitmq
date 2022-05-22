# One-Way Messaging

## Conteúdo

- [Overview](#overview)
- [Características](#Características)
- [Fluxo](#Fluxo)
- [Exemplo de Solução](#Exemplo-de-Solução)
- [Rodando o exemplo](#Rodando-o-exemplo)

---

## Overview

A maneira mais fácil de usar _RabbitMQ_ é implementar o padrão _One-Way Messaging_. Nesse cenário, um _Producer_ envia uma _Message_ para uma _Queue_. Um _Consumer_ da _Queue_ recebe a _Message_ e a processa.

---

## Características

  - Um nome vazio é especificado para o _Exchange_. Um _Exchange_ com um nome vazio é chamado de _default (ou nameless) exchange_.
   - _Exchange_ = "" (sem nome ou padrão)
- Uma _Routing Key_ especificada é idêntica ao nome da _Queue_ para a qual está roteando
   - _Chave de roteamento_ = _Nome da fila_

---

## Fluxo

![rmq-one-way-messaging](https://github.com/allansud/dotnet-rabbitmq/blob/main/doc/one_way_message.png?raw=true)

- _Producer_ envia _Message_ para _Queue_ usando o _Default Exchange_
- A _Message_ é roteada do _Default Exchange_ para a _Queue_ usando uma _routing key_
- _Consumer_ assina a _Queue_
- _Consumer_ recebe e processa _Message_
- _Consumer_ envia uma confirmação (ACK). Um _ACK_ pode ser enviado automaticamente ou manualmente.
- Quando um _ACK_ é recebido, a mensagem é removida permanentemente da _Queue_

---

## Exemplo de Solução

Existem 2 partes para a solução. Um _Produtor_ e um _Consumidor_. O _Producer_ é um aplicativo de console .NET Core que envia _trades_ para uma fila em um intervalo específico. O _Consumer_ é um aplicativo .NET Core Console que aguarda e consome mensagens da fila.

As duas seções a seguir, _Producer_ e _Consumer_, destacam o código necessário para interagir com _RabbitMQ_

### Consumidor (Consumer)

#### Step 1 - Criando a conexão (Connection)

Crie uma _Connection_ usando uma _ConnectionFactory_. O _ConnectionFactory_ especifica várias propriedades padrão, por exemplo:

```csharp
public const string DefaultPass = "guest";
public const string DefaultUser = "guest";
public const string DefaultVHost = "/";
```

Como estamos usando _Docker_ para hospedar nossa instância _RabbitMQ_, precisamos usar os valores definidos por nossa pilha _RabbitMQ_. Observe que o nome de usuário e a senha podem ser encontrados no arquivo _`docker-compose`_:

```yaml
environment:
    ...
    RABBITMQ_DEFAULT_USER: admin
    RABBITMQ_DEFAULT_PASS: password
```

Crie um _ConnectionFactory_ e, em seguida, use o _ConnectionFactory_ para criar um _Connection_ da seguinte forma:

```csharp
var connectionFactory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "admin",
    Password = "password"
};

// a connection is of type IConnection and implements IDisposable
using var connection = connectionFactory.CreateConnection();
```

Um _Connection_ representa uma conexão TCP entre o _Client_ e o _Broker_. É responsável por todas as tarefas de rede e autenticação subjacentes.

#### Step 2 - Criando o canal (Channel)

_Canais_ permitem uma comunicação eficiente e são "conexões leves" que compartilham a conexão TCP primária. Toda a comunicação acontece através de um _Channel_. _Canais_ também são completamente isolados um do outro.

```csharp
// a channel is of type IModel and implements IDisposable
using var channel = connection.CreateModel();
```

#### Step 3 - Declarando a fila (Queue)

Uma _Queue_ é uma coleção ordenada de mensagens FIFO (First-In-First-Out). Ao declarar uma _Queue_, uma _Binding_ entre a _Queue_ e a _Default Exchange_ é criada automaticamente. A _Binding_ terá uma _Routing Key_ idêntica ao nome da _Queue_. Por exemplo, para a _Queue_ a seguir, a _Binding_ criada também terá uma _Routing Key_ de _"example1_trades_queue"_.

Chave de roteamento (Routing key) = Nome da fila = example1_trades_queue

```csharp
var queue = channel.QueueDeclare(
    queue: "example1_trades_queue",
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: ImmutableDictionary<string, object>.Empty);
```

#### Step 4 - Criando o consumidor (Consumer)

Crie um _Consumer_ e assine os eventos _Received_. No _callback_ do evento, pode-se extrair e processar a mensagem que foi enviada para _Queue_.

```csharp
var consumer = new EventingBasicConsumer(channel);

// subscribe to 'Received' event      
consumer.Received += (sender, eventArgs) =>
{
    var messageBody = eventArgs.Body.ToArray();

    var trade = Trade.FromBytes(messageBody);
    
    ...
    ...
    ...
};
```

#### Step 5 - Consumindo as mensagens (Messages)

Comece a consumir mensagens da _Queue_ usando o _Consumer_ que foi criado. Para este exemplo, também desabilitamos os _Acknowledgements (ACKS)_ automáticos.

```csharp
channel.BasicConsume(
    queue: queue.QueueName,
    autoAck: false,
    consumer: consumer);
```

#### Step 6 - Enviando Acknowledgements (ACKS)

Como os _Acknowledgements_ automáticos foram desabilitados na etapa anterior, um _ACK_ deve ser enviado manualmente no _callback_ do _Received Event_ tratado.

```csharp
// subscribe to 'Received' event      
consumer.Received += (sender, eventArgs) =>
{
    ...
    ...
    ...    
    channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
};
```

#### Lista Completa

```csharp
namespace Rabbit.Example1.Consumer
{
    internal sealed class Program
    {
        private static void Main()
        {
            Console.WriteLine("\nEXAMPLE 1 : ONE-WAY MESSAGING : CONSUMER");

            var connectionFactory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "admin",
                Password = "password"
            };

            using var connection = connectionFactory.CreateConnection();

            using var channel = connection.CreateModel();

            var queue = channel.QueueDeclare(
                queue: "example1_trades_queue",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: ImmutableDictionary<string, object>.Empty);

            var consumer = new EventingBasicConsumer(channel);
            
            consumer.Received += (sender, eventArgs) =>
            {
                var messageBody = eventArgs.Body.ToArray();
                var trade = Trade.FromBytes(messageBody);

                // helper to display broker information to the console
                DisplayInfo<Trade>
                    .For(trade)
                    .SetExchange(eventArgs.Exchange)
                    .SetQueue(queue)
                    .SetRoutingKey(eventArgs.RoutingKey)
                    .SetVirtualHost(connectionFactory.VirtualHost)
                    .Display(Color.Yellow);
                
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            };

            channel.BasicConsume(
                queue: queue.QueueName,
                autoAck: false,
                consumer: consumer);

            Console.ReadLine();
        }
    }
}
```

### Produtor

#### Step 1 - Criando a Connection

```csharp
var connectionFactory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "admin",
    Password = "password"
};

using var connection = connectionFactory.CreateConnection();
```

#### Step 2 - Criando Channel

```csharp
using var channel = connection.CreateModel();
```

#### Step 3 - Declarando Queue

```csharp
var queue = channel.QueueDeclare(
    queue: QueueName,
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: ImmutableDictionary<string, object>.Empty);
```

#### Step 4 - Criando e publicando a mensagem

```csharp
var trade = TradeData.GetFakeTrade();

channel.BasicPublish(
    exchange: ExchangeName,
    routingKey: QueueName,
    mandatory: false,
    basicProperties: null,
    body: trade.ToBytes()
);
```

#### Lista Completa

```csharp
namespace Rabbit.Example1.Producer
{
    internal class Program
    {
        internal static async Task Main()
        {
            Console.WriteLine("\nEXAMPLE 1 : ONE-WAY MESSAGING : PRODUCER");

            const string ExchangeName = "";
            const string QueueName = "example1_trades_queue";

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
                var trade = TradeData.GetFakeTrade();

                channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: QueueName,
                    mandatory: false,
                    basicProperties: null,
                    body: trade.ToBytes()
                );

                DisplayInfo<Trade>
                    .For(trade)
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

### Repositório código fonte

Todo o código necessário para executar este exemplo pode ser encontrado em [Github](https://github.com/allansud/dotnet-rabbitmq)

```bash

git clone https://github.com/allansud/dotnet-rabbitmq

```

### Gerenciando RabbitMQ Server

Por exemplo, o RabbitMQ é hospedado em um contêiner _Docker_.

O repositório de código de exemplo inclui um arquivo _'docker-compose'_ que descreve a pilha RabbitMQ com um conjunto razoável de padrões. Use _docker-compose_ para iniciar, parar e exibir informações sobre a pilha RabbitMQ da seguinte forma:

```bash
# Verifica se o 'docker-compose' está instalado
docker-compose --version

# Inicie RabbitMQ stack em segundo plano
docker-compose up --detach

# Verifica se o RabbitMQ container está rodando
docker-compose ps

# Mostra RabbitMQ logs
docker-compose logs

# Mostra e acompanha RabbitMQ logs
docker-compose logs --tail="all" --follow

# Derrube a pilha do RabbitMQ
# Remova os volumes nomeados declarados no `volumes`
# seção do arquivo Compose e volumes anônimos
# anexado ao contêiner
docker-compose down --volumes
```

### Start Producer

```bash

dotnet run -p ./Example1/Rabbit.Example1.Producer/

```

### Start Consumer

```bash

dotnet run -p ./Example1/Rabbit.Example1.Consumer/

```