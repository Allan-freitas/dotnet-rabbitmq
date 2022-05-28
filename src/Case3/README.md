# Routing

## Conteúdos

- [Overview](#overview)
- [Características](#Características)
- [Exemplo de Solução](#Exemplo-de-Solução)
- [Rodando o exemplo](#Rodando-o-exemplo)

---

## Overview

![example-routing-overview](https://github.com/Allan-freitas/dotnet-rabbitmq/blob/main/doc/routing_key.png?raw=true)

---

## Características

O _Routing Message Pattern_ usa um _Direct Exchange_ e captura a ideia de permitir que _Consumers_ recebam mensagens seletivamente. O _Producer_ publica mensagens usando várias _Routing Keys_ para uma _Direct Exchange_. _Consumidores_ se inscrevem seletivamente em mensagens de interesse usando _Chaves de roteamento_ específicas.

---

## Exemplo de Solução

Existem 2 partes para a solução. Um _Produtor_ e um _Consumidor_. O _Producer_ é um aplicativo .Net Core Console que envia _trades_ para uma fila em um intervalo específico. O _Consumer_ é um aplicativo .NET Core Console que aguarda e consome mensagens à medida que as mensagens chegam à fila.

As duas seções a seguir, _Producer_ e _Consumer_, destacam o código necessário para interagir com _RabbitMQ_

### Consumer

#### Step 1 - Create Connection

```csharp
var connectionFactory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "admin",
    Password = "password"
};

using var connection = connectionFactory.CreateConnection();
```

#### Step 2 - Criar Canal (Create Channel)

```csharp
using var channel = connection.CreateModel();
```

#### Step 3 - Declarar fila (Declare Queue)

```csharp
var queueName = channel.QueueDeclare().QueueName;
```

#### Step 4 - Declara a Troca (Declare Exchange)

```csharp
const string ExchangeName = "example4_trades_exchange";

channel.ExchangeDeclare(
    exchange: ExchangeName,
    type: ExchangeType.Direct,
    durable: false,
    autoDelete: false,
    arguments: ImmutableDictionary<string, object>.Empty);
```

#### Step 5 - Cria as viculações (Create Binding)

```csharp
var queue = channel.QueueDeclare(
    queue: QueueNames[region],
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: ImmutableDictionary<string, object>.Empty);

channel.QueueBind(
    queue: queue.QueueName,
    exchange: ExchangeName,
    routingKey: region);
```

#### Step 6 - Cria o consumidor (Create Consumer)

```csharp
var consumer = new EventingBasicConsumer(channel);

consumer.Received += (sender, eventArgs) =>
{
    var messageBody = eventArgs.Body.ToArray();
    var trade = Trade.FromBytes(messageBody);

    DisplayInfo<Trade>
        .For(trade)
        .SetExchange(eventArgs.Exchange)
        .SetQueue(queue.QueueName)
        .SetRoutingKey(eventArgs.RoutingKey)
        .SetVirtualHost(connectionFactory.VirtualHost)
        .Display(Color.Yellow);

    channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
};
```

#### Step 7 - Consome a mensagem (Consume Messages)

```csharp
channel.BasicConsume(
    queue: queue.QueueName,
    autoAck: false,
    consumer: consumer);
```

#### Step 8 - Enviar Acknowledgements (ACKS) (Send Acknowledgements (ACKS))

```csharp
channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
```

#### Listagem Completa

```csharp
internal sealed class Program
{
    private static void Main(string[] regions)
    {
        Console.WriteLine("\nEXAMPLE 4 : ROUTING : CONSUMER");

        var region = regions.FirstOrDefault() ?? string.Empty;

        var QueueNames = TradeData
            .Regions
            .Select(region =>
            {
                var normalizedRegion = region.Normalize().ToLower().Trim().Replace(" ", string.Empty);
                var queueName = $"example4_trades_{normalizedRegion}_queue";
                return new KeyValuePair<string, string>(region, queueName);
            })
            .ToImmutableDictionary();

        if (!QueueNames.ContainsKey(region))
        {
            Console.WriteLine($"\nInvalid region '{region}'.".Pastel(Color.Tomato));
            Console.WriteLine($"Enter valid region name to start ({string.Join(", ", QueueNames.Keys)})".Pastel(Color.Tomato));
            Console.WriteLine();
            Environment.ExitCode = 1;
            return;
        }

        var connectionFactory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "admin",
            Password = "password"
        };

        using var connection = connectionFactory.CreateConnection();

        using var channel = connection.CreateModel();

        const string ExchangeName = "example4_trades_exchange";

        channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Direct,
            durable: false,
            autoDelete: false,
            arguments: ImmutableDictionary<string, object>.Empty);

        var queue = channel.QueueDeclare(
            queue: QueueNames[region],
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: ImmutableDictionary<string, object>.Empty);

        channel.QueueBind(
            queue: queue.QueueName,
            exchange: ExchangeName,
            routingKey: region);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (sender, eventArgs) =>
        {
            var messageBody = eventArgs.Body.ToArray();
            var trade = Trade.FromBytes(messageBody);

            DisplayInfo<Trade>
                .For(trade)
                .SetExchange(eventArgs.Exchange)
                .SetQueue(queue.QueueName)
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
```

### Producer

#### Step 1 - Criar conexão

```csharp
var connectionFactory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "admin",
    Password = "password"
};

using var connection = connectionFactory.CreateConnection();
```

#### Step 2 - Criar canal (Create Channel)

```csharp
using var channel = connection.CreateModel();
```

#### Step 3 - Declarar troca (Declare Exchange)

```csharp
const string ExchangeName = "example4_trades_exchange";

channel.ExchangeDeclare(
    exchange: ExchangeName,
    type: ExchangeType.Direct,
    durable: false,
    autoDelete: false,
    arguments: ImmutableDictionary<string, object>.Empty);
```

#### Step 4 - Criar vinculações (Binndings)
```csharp
var QueueNames = TradeData
    .Regions
    .Select(region =>
    {
        var normalizedRegion = region.ToLower().Trim().Replace(" ", string.Empty);
        var queueName = $"example4_trades_{normalizedRegion}_queue";
        return new KeyValuePair<string, string>(region, queueName);
    })
    .ToImmutableDictionary();

foreach (var region in TradeData.Regions)
{
    var queue = channel.QueueDeclare(
        queue: QueueNames[region],
        durable: false,
        exclusive: false,
        autoDelete: false,
        arguments: ImmutableDictionary<string, object>.Empty);

    channel.QueueBind(
        queue: queue.QueueName,
        exchange: ExchangeName,
        routingKey: region,
        arguments: ImmutableDictionary<string, object>.Empty);
}
```

#### Step 5 - Criar e publicar mensagem

```csharp
var trade = TradeData.GetFakeTrade();

string routingKey = trade.Region;

channel.BasicPublish(
    exchange: ExchangeName,
    routingKey: routingKey,
    body: trade.ToBytes()
);
```

#### Listagem completa

```csharp
internal sealed class Program
{
    private static async Task Main()
    {
        Console.WriteLine("EXAMPLE 4 : ROUTING : PRODUCER");

        var connectionFactory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "admin",
            Password = "password"
        };

        using var connection = connectionFactory.CreateConnection();

        using var channel = connection.CreateModel();

        const string ExchangeName = "example4_trades_exchange";

        channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Direct,
            durable: false,
            autoDelete: false,
            arguments: ImmutableDictionary<string, object>.Empty);

        var QueueNames = TradeData
            .Regions
            .Select(region =>
            {
                var normalizedRegion = region.ToLower().Trim().Replace(" ", string.Empty);
                var queueName = $"example4_trades_{normalizedRegion}_queue";
                return new KeyValuePair<string, string>(region, queueName);
            })
            .ToImmutableDictionary();

        foreach (var region in TradeData.Regions)
        {
            var queue = channel.QueueDeclare(
                queue: QueueNames[region],
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: ImmutableDictionary<string, object>.Empty);

            channel.QueueBind(
                queue: queue.QueueName,
                exchange: ExchangeName,
                routingKey: region,
                arguments: ImmutableDictionary<string, object>.Empty);
        }

        while (true)
        {
            var trade = TradeData.GetFakeTrade();

            string routingKey = trade.Region;

            channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                body: trade.ToBytes()
            );

            DisplayInfo<Trade>
                .For(trade)
                .SetExchange(ExchangeName)
                .SetRoutingKey(routingKey)
                .SetVirtualHost(connectionFactory.VirtualHost)
                .Display(Color.Yellow);

            await Task.Delay(millisecondsDelay: 3000);
        }
    }
}
```

---

## Rodando o exemplo

### Repositório de código-fonte

Todo o código necessário para executar este exemplo pode ser encontrado em [Github](https://github.com/Allan-freitas/dotnet-rabbitmq)

```bash

git clone https://github.com/Allan-freitas/dotnet-rabbitmq.git

```

### Gerenciar servidor RabbitMQ (Manage RabbitMQ Server)

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

### Iniciar Producer

```bash

# abra um novo terminal e execute o seguinte comando
dotnet run -p ./Case3/Rabbitmq.Case3.Producer/

```

### Iniciar Worker 1

```bash

# abra um novo terminal e execute o seguinte comando
dotnet run "Australia" -p ./Case3/Rabbitmq.Case3.Consumer/

```

### Iniciar Worker 2

```bash

# abra um novo terminal e execute o seguinte comando
dotnet run "Great Britain" -p ./Example3/Rabbitmq.Case3.Consumer/

```

### Iniciar Worker 3

```bash

# abra um novo terminal e execute o seguinte comando
dotnet run "USA" -p ./Example3/Rabbitmq.Case3.Consumer/

```