﻿using System;
using System.Text;
using RabbitMQ.Client;

namespace Hik.Helpers
{
    public class RabbitMQSender : IDisposable
    {
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly string routingKey;
        private bool disposedValue = false;

        public RabbitMQSender(string hostName, string queueName, string routingKey)
        {
            this.routingKey = routingKey;
            ConnectionFactory factory = new ConnectionFactory() { HostName = hostName };
            connection = factory.CreateConnection();
            channel = connection.CreateModel();
            channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        }

        public void Sent(string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: string.Empty, routingKey: this.routingKey, basicProperties: null, body: body);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                connection?.Dispose();
                channel?.Dispose();
                disposedValue = true;
            }
        }
    }
}