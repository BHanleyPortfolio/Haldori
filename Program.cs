using System;
using TwitchLib;

namespace HaldoriBot
{
    class Program
    {
        static void Main(string[] args)
        {
            TwitchChatBot bot = new TwitchChatBot();
            bot.Connect();

            //TwitchBitBot bitGames = new TwitchBitBot();
            //bitGames.Start();

            Console.ReadLine();

            bot.Disconnect();
        }
    }
}
