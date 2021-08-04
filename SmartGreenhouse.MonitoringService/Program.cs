using System;
using System.Collections.Generic;
using System.Net.Mqtt;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartGreenhouse.MonitoringService
{
    class Program
    {
        static void Main(string[] args)
        {
            // initial setup: parameters
            const string _mqttBroker = "192.168.1.98";
            const string _mqttClientId = "MonitoringService";
            List<string> topics = new List<string> { "greenhouse/conditions", "anotherTopic" };

            // initial setup: initialize
            while (true)
            {
                Console.WriteLine("Starting application...");

                var mqtt = StartMqttClientAsync(_mqttBroker, _mqttClientId).Result;

                if (mqtt.connected)
                {
                    // subscribe to all topics
                    var subscribed = SubscribeToTopics(topics, mqtt.client).Result;
                    if (subscribed)
                    {
                        Console.WriteLine("Succesfully subscribed to all topics.");
                        // recieve messages from topics
                        mqtt.client
                            .MessageStream
                            .Subscribe(msg => MessagesMonitoring(msg.Topic, Encoding.UTF8.GetString(msg.Payload)));
                        //   ^ observable collection, triggered whenever new message is recieved from any topic
                    }

                    while (true)
                    {
                        // keep application alive = in a constant loop
                    }
                }
            }
        }

        static void MessagesMonitoring(string topic, string message)
        {
            // logic what to do with each type (topic) of a message
            // i.e. if this topic, then this...

            // for now, for all new messages -> display in console
            Console.WriteLine();
            Console.Write("Message payload: ");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write(message);
            Console.ResetColor();
            Console.Write($" [@ Topic: {topic}]");
        }

        static void Publish(IMqttClient mqttClient)
        {
            // publish after delay
            Thread.Sleep(1000);

            // test message
            var message = $"MQTT rocks! {DateTime.Now}";
            var topic = "test1";

            // publish message
            var result = PublishMessageAsync(message, topic, mqttClient).Result;
            if (result)
            {
                Console.WriteLine("Message sent to the MQTT broker succesfully.");
            }
            else
            {
                Console.WriteLine("Message to the MQTT broker not sent!!! Sorry.");
            }
        }

        static async Task<(IMqttClient client, bool connected)> StartMqttClientAsync(string brokerIpAddress, string monitoringServiceClientId)
        {
            // create new client
            var client = await MqttClient.CreateAsync(brokerIpAddress, new MqttConfiguration());

            // connect            
            Console.Write("Connecting to the MQTT broker... ");
            try
            {
                var mqttSession = await client.ConnectAsync(new MqttClientCredentials(clientId: monitoringServiceClientId));
                Console.WriteLine($"Connected succesfully!");
            }
            catch (Exception)
            {
                Console.WriteLine("Sorry, not connected.");
            }

            return (client, client.IsConnected);
        }

        static async Task<bool> PublishMessageAsync(string message, string topic, IMqttClient client)
        {
            var mqttMessage = new MqttApplicationMessage(topic, Encoding.UTF8.GetBytes(message));

            try
            {
                await client.PublishAsync(mqttMessage, MqttQualityOfService.AtLeastOnce);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static async Task<bool> SubscribeToTopics(List<string> topics, IMqttClient client)
        {
            try
            {
                foreach (var t in topics)
                {
                    await client.SubscribeAsync(t, MqttQualityOfService.AtLeastOnce);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}
