using System;
using System.Collections.Generic;
using System.Net.Mqtt;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace SmartGreenhouse.MonitoringService
{
    class Program
    {
        static List<ConditionsReading> averageConditions = new();
        static HttpClient httpClient = new();

        static void Main(string[] args)
        {
            // initial setup: parameters
            const string _webApi = "http://192.168.1.132:8888/";
            const string _mqttBroker = "192.168.1.98";
            const string _mqttClientId = "MonitoringService";
            List<string> topics = new() { "greenhouse/conditions" };

            // initial setup: initialize
            while (true)
            {
                Console.WriteLine("Starting application...");

                // setup: http client for web api access
                httpClient.BaseAddress = new Uri(_webApi);
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // setup: mqtt broker to handle publish-subscribe messages
                var mqtt = StartMqttClientAsync(_mqttBroker, _mqttClientId).Result;

                if (mqtt.connected)
                {
                    // subscribe to all topics
                    var subscribed = SubscribeToTopicsAsync(topics, mqtt.client).Result;
                    if (subscribed)
                    {
                        // recieve messages from topics
                        mqtt.client
                            .MessageStream
                            .Subscribe(msg => MessagesMonitoring(msg.Topic, Encoding.UTF8.GetString(msg.Payload), mqtt.client));
                        // ^ observable collection, triggered whenever new message is recieved from any topic
                    }

                    while (true)
                    {
                        // constant loop to keep application alive
                    }
                }
            }
        }

        static void MessagesMonitoring(string topic, string message, IMqttClient mqttClient)
        {
            try
            {
                // deserialize recieved message into an object (model)
                ConditionsReading cr = JsonSerializer.Deserialize<ConditionsReading>(message);

                // add datetime stamp
                cr.TimeStamp = DateTime.Now;

                // display in a console
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"Temperature: {cr.AirTemperature} Humidity: {cr.AirHumidity}");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write($" Sensor: {cr.SensorNodeId}");
                Console.ResetColor();
                Console.Write($" @ {cr.TimeStamp}");
                Console.WriteLine();

                // save conditions reading in DB
                var response = PostConditionsToWebApiAsync(cr, message);
                if (response.Result.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Http Request with data sent succesfully to the Web API.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine("Http Request not sent!!!");
                    Console.ResetColor();
                    Console.WriteLine(response);
                }

                // calculate average conditions
                var avgConditions = CalculateAverageConditions(cr);
                avgConditions.TimeStamp = DateTime.Now;
                avgConditions.SensorNodeId = "averaged";

                // publish average conditions
                var msgAvgConditions = JsonSerializer.Serialize(avgConditions);
                var tryPublish = PublishMessageAsync(msgAvgConditions, "greenhouse/averageconditions", mqttClient);
                if (tryPublish.Result)
                {
                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                    Console.WriteLine("Average conditions published to the MQTT broker successfully.");
                    Console.ResetColor();
                }

            }
            #region Catch and Report Exceptions
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Failed to process the message:");
                Console.ResetColor();
                Console.WriteLine(message);

                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("Error:");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.ResetColor();
                Console.WriteLine();
                return;
            }
            #endregion
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

        static async Task<bool> SubscribeToTopicsAsync(List<string> topics, IMqttClient client)
        {
            try
            {
                foreach (var t in topics)
                {
                    await client.SubscribeAsync(t, MqttQualityOfService.AtLeastOnce);
                }

                Console.WriteLine("Succesfully subscribed to all topics.");
                return true;
            }
            catch (Exception)
            {
                Console.WriteLine("Subscribing to topics failed...");
                return false;
            }
        }

        static async Task<HttpResponseMessage> PostConditionsToWebApiAsync(ConditionsReading cr, string msg)
        {
            //var jsonCr = JsonSerializer.Serialize(cr);
            //var myJson = msg;
            var response = await httpClient.PostAsJsonAsync("api/conditionsreadings", cr);
            //var response = await httpClient.PostAsync("api/conditionsreadings", new StringContent(myJson, Encoding.UTF8, "application/json"));

            return response;
        }

        static ConditionsReading CalculateAverageConditions(ConditionsReading cr)
        {
            if (averageConditions.Where(x => x.SensorNodeId == cr.SensorNodeId).Any())
            {
                averageConditions.RemoveAll(x => x.SensorNodeId == cr.SensorNodeId);
            }
            averageConditions.Add(cr);

            var avgCR = new ConditionsReading();

            int airTemperatureCounter = 0;
            int airHumidityCounter = 0;
            int soilMoistureCounter = 0;
            int lightIntensityCounter = 0;

            foreach (var reading in averageConditions)
            {
                if (reading.AirTemperature != 0)
                {
                    airTemperatureCounter++;
                    avgCR.AirTemperature += reading.AirTemperature;
                }
                if (reading.AirHumidity != 0)
                {
                    airHumidityCounter++;
                    avgCR.AirHumidity += reading.AirHumidity;
                }
                if (reading.SoilMoisture != 0)
                {
                    soilMoistureCounter++;
                    avgCR.SoilMoisture += reading.SoilMoisture;
                }
                if (reading.LightIntensity != 0)
                {
                    lightIntensityCounter++;
                    avgCR.LightIntensity += reading.LightIntensity;
                }
            }

            if (avgCR.AirTemperature != 0) avgCR.AirTemperature = Math.Round(avgCR.AirTemperature / airTemperatureCounter, 2);
            if (avgCR.AirHumidity != 0) avgCR.AirHumidity = Math.Round(avgCR.AirHumidity / airHumidityCounter, 2);
            if (avgCR.SoilMoisture != 0) avgCR.SoilMoisture = Math.Round(avgCR.SoilMoisture / soilMoistureCounter, 2);
            if (avgCR.LightIntensity != 0) avgCR.LightIntensity = avgCR.LightIntensity / lightIntensityCounter;

            return avgCR;
        }

    }
}
