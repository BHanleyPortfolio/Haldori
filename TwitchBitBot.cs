using System;
using Microsoft.Extensions.Logging;
using TwitchLib;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events; 

namespace HaldoriBot
{
    internal class TwitchBitBot
    {
        readonly ConnectionCredentials bitCredentials = new ConnectionCredentials(BitInfo.ChannelName, BitInfo.AccessToken);
        TwitchClient client;
        TwitchPubSub pubsub;

        internal void Start()
        {
            client = new TwitchClient();
            client.Initialize(bitCredentials, BitInfo.ChannelName);
            client.Connect();

            pubsub = new TwitchPubSub();

            pubsub.OnPubSubServiceConnected += PubSub_Connected;
            pubsub.OnListenResponse += onPubSubResponse;
            pubsub.OnBitsReceived += PubSub_OnCheer;
            Console.WriteLine("PubSub Started");
            pubsub.Connect();
        }

        private void onPubSubResponse(object sender, OnListenResponseArgs e)
        {
            if (e.Successful)
                Console.WriteLine($"Successfully verified listening to topic: {e.Topic}");
            else
                Console.WriteLine($"Failed to listen! Error: {e.Response.Error}");
        }

        private void PubSub_Connected(object sender, EventArgs e)
        {
            pubsub.ListenToBitsEvents(BitInfo.ChannelName);
            pubsub.SendTopics(BitInfo.AccessToken);
            Console.WriteLine("PubSub Connected");
            client.SendWhisper(BitInfo.ChannelName, $"Listening for bits you sneaky snek.");
        }


        /**
         * Holiday bits game
         * 
         * 
         **/

        private void PubSub_OnCheer(object sender, OnBitsReceivedArgs e)
        {
            Console.WriteLine($"{e.BitsUsed} received!");
            //client.SendWhisper(TwitchInfo.ChannelName, $"{e.Username} gave you {e.TotalBitsUsed} bits!");
        }
    }


}