using System;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Client.Events;
//using TwitchLib.PubSub;
//using TwitchLib.PubSub.Events;
using System.Linq;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using System.Timers;

namespace HaldoriBot
{
    internal class TwitchChatBot
    {
        readonly ConnectionCredentials credentials = new ConnectionCredentials(TwitchInfo.BotUsername, TwitchInfo.BotToken);
        TwitchClient client;

        //TODO: move pubsub functionality out
        //TwitchPubSub pubsub;

        //readonly string connectionstring = $"Server = {mysql.connection}; Database = {mysql.schema}; User Id = {mysql.username}; Password = {mysql.password}";
        readonly string dbbackupstring = $"Server = {mysqlbak.connection}; Database = {mysqlbak.schema}; User Id = {mysqlbak.username}; Password = {mysqlbak.password}";
        readonly string jbdbconnection = $"Server = {jbdb.connection}; Database = {jbdb.schema}; User Id = {jbdb.username}; Password = {jbdb.password}";

        // Define current turn and game variable so that we can keep the log going
        private static String turnId = "";
        private static String gameId = "";

        // Timer booleans
        private bool creating = false;
        private bool voting = false;

        // Create vote ids and limits based off of these
        private int talkId = 0;
        private int skillId = 0;
        private int attackId = 0;

        // Generates random strings for turn and gameids
        private static Random random = new Random();
        

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public TwitchChatBot()
        {
        }

        internal void Connect()
        {
            Console.WriteLine("Connecting");

            client = new TwitchClient();
            client.Initialize(credentials, TwitchInfo.ChannelName);

            
            client.OnLog += Client_OnLog;
            client.OnJoinedChannel += Client_Joined;
            client.OnConnectionError += Client_OnConnectionError;
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnWhisperReceived += Client_OnWhisperReceived;

            client.Connect();

            //TwitchAPI.Settings.ClientId = TwitchInfo.ClientId;
        }

       

   

        private void Client_Joined(object sender, OnJoinedChannelArgs e)
        {
            client.SendMessage(e.Channel, $"Haldori in da hizouse!");
            gameId = RandomString(10);
            turnId = RandomString(8);
        }

        // Run Insert/Update/Remove queries to database
        internal void WriteToDb(string query, string connection)
        {
            MySqlConnection haldori = new MySqlConnection(connection);
            
            try
            {
                haldori.Open();
                MySqlCommand cmd = new MySqlCommand(query,haldori);
                cmd.ExecuteNonQuery();
                haldori.Close();
            } catch (ArgumentException ex)
            {
                Console.WriteLine($"Error! {ex.Message}");
            }
        }

        // Get information on if data exists from a query
        internal bool CheckIfDataExists(string query, string connection)
        {
            MySqlConnection haldori = new MySqlConnection(connection);

            haldori.Open();
            MySqlCommand cmd = new MySqlCommand(query, haldori);
            MySqlDataReader reader = cmd.ExecuteReader();
            //haldori.Close();

            if (reader.HasRows)
            {
                haldori.Close();
                return true;
            }
            haldori.Close();
                return false;
        }

        //Retrieve GameId field
        internal string RetrieveGameId(string query, string connection)
        {
            MySqlConnection haldori = new MySqlConnection(connection);
            var gameId = "fucked";
            haldori.Open();
            MySqlCommand cmd = new MySqlCommand(query, haldori);
            MySqlDataReader reader = cmd.ExecuteReader();
            //haldori.Close();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    gameId = reader[0].ToString();
                    haldori.Close();
                    return gameId;
                }
            }
            haldori.Close();
            return gameId;
        }

        internal void endActions()
        {
            creating = false;
        }

        internal void startActions()
        {
            creating = true;
        }

        internal void endVoting()
        {
            voting = false;
        }

        internal void startVoting()
        {
            voting = true;
        }

        private void startVote(object source, ElapsedEventArgs e)
        {

            Timer timer = (Timer)source; // Get the timer that fired the event
            timer.Stop(); // Stop the timer that fired the event
            client.SendMessage($"Actions over, you have 45 seconds to vote!");
            endActions();
            startVoting();
            votingPhase(45000);
            //Console.WriteLine("Hello World!");
        }

        private void endVote(object source, ElapsedEventArgs e)
        {
            Timer timer = (Timer)source; // Get the timer that fired the event
            client.SendMessage($"Voting over, wait until next turn.");
            endVoting();
        }

        private void actionPhase(int length)
        {
            Timer aTimer = new Timer();
            aTimer.Elapsed += new ElapsedEventHandler(startVote);
            // Set the Interval to 10 seconds.
            aTimer.Interval = length;
            aTimer.Enabled = true;
            aTimer.AutoReset = false;
        }

        private void votingPhase(int length)
        {
            startVoting();
            Timer aTimer = new Timer();
            aTimer.Elapsed += new ElapsedEventHandler(endVote);
            // Set the Interval to 10 seconds.
            aTimer.Interval = length;
            aTimer.Enabled = true;
            aTimer.AutoReset = false;
        }


        // Makes an action happen
        internal void TakeAction(string action, string target, string words, string user, int voteId)
        {
            WriteToDb($"INSERT INTO `haldori`.`game_actions` (`game_id`, `turn_id`, `action`, `target`, `words`, `username`, `vote_id`, `votes`) VALUES ('{gameId}', '{turnId}', '{action}', '{SanitizeChatInput(target)}', '{SanitizeChatInput(words)}', '{user}', '{voteId}', 0);", dbbackupstring);
        }

        // Votes on current turn
        internal void VoteAction(string action, string user, string vote)
        {
            //WriteToDb($"INSERT INTO `haldori`.`votes` (`channel`, `game_id`, `turn_id`, `username`, `action`, `vote_id`) VALUES ('{TwitchInfo.ChannelName}', '{gameId}', '{turnId}', '{user}', '{action}', '{vote}');");
            WriteToDb($"UPDATE `haldori`.`game_actions` SET `votes` = `votes` + 1 WHERE `action` = '{action}' AND `vote_id` = '{vote}' AND `game_id` = '{gameId}' AND `turn_id` = '{turnId}';", dbbackupstring);
        }

        // Update channel's current game
        internal void UpdateCurrentGame()
        {
            WriteToDb($"UPDATE `haldori`.`current_game` SET `game_id`='{gameId}', `turn_id`='{turnId}' WHERE `channel`='{TwitchInfo.ChannelName}';", dbbackupstring);
        }

        // Registers a game
        internal void RegisterGame()
        {
            WriteToDb($"INSERT INTO `haldori`.`games` (`channel`, `game_id`) VALUES ('{TwitchInfo.ChannelName}', '{gameId}');", dbbackupstring);
        }



        //Get GameId for current channel
        internal void CurrentGameId()
        {

        }

        //Registers a Jackbox game
        internal void JbRegisterGame(string gid, string channel)
        {
            if(!CheckIfDataExists($"SELECT * FROM `games` WHERE `channel` = '{channel}';", jbdbconnection))
            {
                WriteToDb($"INSERT INTO `jbdb`.`games` (`channel`, `game_id`) VALUES ('{channel}', '{gid}');", jbdbconnection);
            }
            else
            {
                WriteToDb($"UPDATE `jbdb`.`games` SET `game_id`='{gid}' WHERE `channel`='{channel}';", jbdbconnection);
            }
            
        }

        //Votes on Jackbox game
        internal void JbVote(string gid, string user, string vote)
        {
            WriteToDb($"INSERT INTO `jbdb`.`votes` (`username`, `game`, `vote`, `channel`) VALUES ('{user}', '{vote}', '{gid}', '{TwitchInfo.ChannelName}');", jbdbconnection);
        }

        // Removes chat command from input
        internal string RemoveCommand(string command, string input)
        {
            var regex = new Regex(command, RegexOptions.IgnoreCase);
            var output = regex.Replace(input, "").Trim();
            return output;
        }

        //TODO: Figure out how to get mods or remove this.
        internal static void GetEditors()
        {
            
        }

        //TODO: Possible refactor, little vague and doesn't account for additional search capabilities
        // Check if things already exist in DB
        internal bool DbCheck(string table, string column, string value)
        {
            var query = $"SELECT * FROM haldori.{table} WHERE `{column}` = '{value}';";
            return CheckIfDataExists(query, dbbackupstring);
        }

        // Check to see if user has already submitted an action of that context
        internal bool ActionCheck(string action, string username)
        {
            return CheckIfDataExists($"SELECT * FROM haldori.game_actions WHERE `action` = '{action}' AND `username` = '{username}' AND `game_id` = '{gameId}' AND `turn_id` = '{turnId}';", dbbackupstring);
        }

        // Check to see if user has already voted
        internal bool VoteCheck(string username)
        {
            return CheckIfDataExists($"SELECT username from haldori.votes WHERE `game_id` = '{gameId}' AND `turn_id` = '{turnId}' AND `username` = '{username}';", dbbackupstring);
        }

        // TODO: There's gotta be a better way to deal with sanitization, figure it out!
        // Sanitizes input by users to avoid MySQL Injection
        internal string SanitizeChatInput(string chat)
        {
            return chat.Replace("'", "&#39;").Replace("\\", "&#92;").Replace("\"", "&quot;").Trim();
        }

        // TODO: Closed Beta over, should be able to start removing all beta references
        internal bool BetaCheck(string username)
        {
            string[] betaTester = { "fluffsmckenzie", "theblorble", "areyouop1", "carelessgrin", "serfass", "saraskywalker01", "kraefishie", "bobothegrate", "jagerdelights", "sondassasda", "whatspopinjimbo", "luxybear", "earlyybird", "stumpicus22", "nailbunnygirl" };
            return betaTester.Contains(username);
        }

        // TODO: Find more efficient way for mods to be here
        internal bool ModCheck(string username)
        {
            string[] modList = { "fluffsmckenzie", "theblorble", "sondassasda", "earlyybird", "serfass", "nailbunnygirl", "po9014", "udinae", "mrh4nky" };
            return modList.Contains(username);
        }

        private void Client_OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {
            //TODO: Debugging, remove
            Console.WriteLine($"{e.WhisperMessage.Username} - {e.WhisperMessage.Message}");

            // TODO: define whispers from channel owner and mods
            string[] mods = { "fluffsmckenzie", "serfass", "earlyybird", "po9014" };
            try
            {
                if (mods.Contains(e.WhisperMessage.Username))
                {
                    //TODO: Remove Igor
                    //client.SendWhisper(e.WhisperMessage.Username, $"Yeeessss masterrrr");
                    // TODO: Remove - purely debugging
                    if (e.WhisperMessage.Message.StartsWith("!currentturn", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (turnId == "")
                        {
                            throw new ArgumentException($"No Turn ID set, run !nextturn ya mook.");
                        }
                            client.SendWhisper(e.WhisperMessage.Username, $"Current Turn ID is {turnId}");
                            //this.RunDBQuery($"INSERT INTO `haldori`.`game_actions` (`turn_id`) VALUES ('{turnId}');");
                    }
                    // TODO: Remove ID return and shorten command
                    // !nextturn - Creates a turn session ID
                    else if (e.WhisperMessage.Message.StartsWith("!nextturn", StringComparison.InvariantCultureIgnoreCase) || e.WhisperMessage.Message.StartsWith("!nt", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if(gameId == "")
                        {
                            throw new ArgumentException($"Dude...the game hasn't even started yet. Hold up.");
                        }
                        skillId = 0;
                        attackId = 0;
                        talkId = 0;
                        turnId = RandomString(8);
                        client.SendWhisper(e.WhisperMessage.Username, $"Next Turn starting. ID is {turnId}");
                        client.SendMessage($"Next turn starting, you have 90 seconds to create an action.");
                        startActions();
                        actionPhase(90000);
                        UpdateCurrentGame();
                    }
                    //TODO: Add AddTarget Command
                    else if (e.WhisperMessage.Message.StartsWith("!addtarget", StringComparison.InvariantCultureIgnoreCase))
                    {
                        client.SendWhisper(e.WhisperMessage.Username, $"Next Turn starting. ID is {turnId}");
                    }
                    //TODO: Add ClearTargets Command
                    //TODO: Add Health Commands
                    // !startgame - Begins game session
                    else if (e.WhisperMessage.Message.StartsWith("!startgame", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (gameId != "")
                        {
                            throw new ArgumentException($"Game session already started.");
                        }
                            gameId = RandomString(10);
                            turnId = RandomString(8);
                            client.SendWhisper(e.WhisperMessage.Username, $"Starting game! Game ID is {gameId}");
                            client.SendMessage($"Game starting, you have 90 seconds to create an action.");
                            startActions();
                            actionPhase(90000);
                        if (DbCheck($"current_game", $"channel", $"{TwitchInfo.ChannelName}"))
                        {
                            WriteToDb($"INSERT INTO `haldori`.`current_game` (`channel`, `game_id`, `turn_id`) VALUES ('{TwitchInfo.ChannelName}', '{gameId}', '{turnId}');", dbbackupstring);
                            RegisterGame();
                        }
                        else
                        {
                            UpdateCurrentGame();
                            RegisterGame();
                        }
                     }
                    // !endgame - Ends Game Session
                    else if (e.WhisperMessage.Message.StartsWith("!endgame", StringComparison.InvariantCultureIgnoreCase))
                    {
                        gameId = "";
                        turnId = "";
                        client.SendWhisper(e.WhisperMessage.Username, $"Game ended! You done good champ!");
                        client.SendMessage(TwitchInfo.ChannelName, $"Thanks for playing! Remember to destroy all doors!");
                    }
                    //END D&D COMMANDS
                    //START JACKBOX COMMANDS
                    else if (e.WhisperMessage.Message.StartsWith("!jackbox"))
                    {
                        gameId = RandomString(10);
                        turnId = RandomString(8);
                        client.SendWhisper(e.WhisperMessage.Username, $"It's Jackbox Party Time?! TIME'S UP LET'S DO THIS! HALDORI RORI ROOOOOOO");
                        JbRegisterGame(gameId, e.WhisperMessage.Username);
                        endVoting();
                    }
                    else if (e.WhisperMessage.Message.StartsWith("!jbgid"))
                    {
                        var query = $"SELECT `game_id` FROM `jbdb`.`games` WHERE `channel` = '{TwitchInfo.ChannelName}';";
                        var gid = RetrieveGameId(query, jbdbconnection);
                        client.SendWhisper(e.WhisperMessage.Username, gid);
                    }
                    //END JACKBOX COMMANDS
                }
                else
                {
                    throw new ArgumentException($"You are not my master, shoo!");
                }
            }
            catch(ArgumentException ex)
            {
                client.SendWhisper(e.WhisperMessage.Username, ex.Message);
            }
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            Console.WriteLine($"{e.ChatMessage.Channel} - {e.ChatMessage.Username} said \"{e.ChatMessage.Message}\"");
            string[] commands = { "!talk", "!attack", "!skill", "!vote", "!action" , "!jbvote", "!jbpoll"};

            try
            {
                // Don't worry about anything if the game hasn't started yet
                if (turnId == "" && BetaCheck(e.ChatMessage.Username) && commands.Any(c => e.ChatMessage.Message.StartsWith(c)))
                {
                    throw new ArgumentException($"The game has not begun yet. Please wait until the game begins.");
                }

                //!talk <target> | <words>
                if (e.ChatMessage.Message.StartsWith("!talk", StringComparison.InvariantCultureIgnoreCase))
                {
                    var chatArray = RemoveCommand("!talk", e.ChatMessage.Message).Split('|');

                    //TODO: Remove beta
                   /* if (!BetaCheck(e.ChatMessage.Username))
                    {
                        throw new ArgumentException($"Sorry, only beta testers allowed right now. Use your Loyalty Points to become a beta tester!");
                    }*/

                    if(chatArray.Length < 2)
                    {
                        throw new ArgumentException($"Make sure you're entering your command as !talk target | words.");
                    }

                    if (chatArray[0] == "")
                    {
                        throw new ArgumentException($"You have to choose someone or something to talk to.");
                    }

                    if (chatArray[1] == "")
                    {
                        throw new ArgumentException($"You're not a mute (yet), say something!");
                    }

                    if (creating == false && voting == true)
                    {
                        throw new ArgumentException($"It's voting time, save your actions for next turn.");
                    }

                    if(creating == false && voting == false)
                    {
                        throw new ArgumentException($"Wait until the next turn.");
                    }

                    if (ActionCheck($"talk", e.ChatMessage.Username))
                    {
                        client.SendMessage($"You want to tell {this.SanitizeChatInput(chatArray[0])} \"{this.SanitizeChatInput(chatArray[1])}\"");
                        talkId++;
                        TakeAction($"talk", chatArray[0], chatArray[1], e.ChatMessage.Username, talkId);
                    }
                    else
                    {
                        client.SendMessage($"You already chose to talk.");
                    }

                }

                //!attack <target> (| <battlecry>?)
                else if (e.ChatMessage.Message.StartsWith("!attack", StringComparison.InvariantCultureIgnoreCase))
                {
                    var target = RemoveCommand("!attack", e.ChatMessage.Message);

                    //TODO: Remove beta
                    //if (!BetaCheck(e.ChatMessage.Username))
                    //{
                    //    throw new ArgumentException($"Sorry, only beta testers allowed right now. Use your Loyalty Points to become a beta tester!");
                    //}

                    if (target == "")
                    {
                        throw new ArgumentException($"You must enter a target to attack.");
                    }

                    if (creating == false && voting == true)
                    {
                        throw new ArgumentException($"It's voting time, save your actions for next turn.");
                    }

                    if (creating == false && voting == false)
                    {
                        throw new ArgumentException($"Wait until the next turn.");
                    }
                    if (ActionCheck($"attack", e.ChatMessage.Username))
                    {
                        client.SendMessage($"You want to attack {target}!");
                        attackId++;
                        TakeAction($"attack", target, "", e.ChatMessage.Username, attackId);
                    }
                    else
                    {
                        client.SendMessage($"You already chose to attack.");
                    }
                }

                //!skill <skill> | <target>
                else if (e.ChatMessage.Message.StartsWith("!skill", StringComparison.InvariantCultureIgnoreCase))
                {
                    var chatArray = RemoveCommand("!skill", e.ChatMessage.Message).Split('|');

                    //TODO: Remove beta
                    /*if (!BetaCheck(e.ChatMessage.Username))
                    {
                        throw new ArgumentException($"Sorry, only beta testers allowed right now. Use your Loyalty Points to become a beta tester!");
                    }*/

                    if (chatArray.Length < 2)
                    {
                        throw new ArgumentException($"Make sure you're entering your command as !skill <skill> | <target>.");
                    }

                    // No Exception needed for target as a skill can just be an AOE or room
                    if (chatArray[0] == "")
                    {
                        throw new ArgumentException($"You need to pick a skill.");
                    }

                    if (creating == false && voting == true)
                    {
                        throw new ArgumentException($"It's voting time, save your actions for next turn.");
                    }

                    if (creating == false && voting == false)
                    {
                        throw new ArgumentException($"Wait until the next turn.");
                    }

                    if (ActionCheck($"skill", e.ChatMessage.Username))
                    {
                        client.SendMessage($"You want to use {chatArray[0]} on {chatArray[1]}.");
                        skillId++;
                        TakeAction("skill", chatArray[1], chatArray[0], e.ChatMessage.Username, skillId);
                    }
                    else
                    {
                        client.SendMessage($"You already chose to use a skill.");
                    }
                }
                //!action <action> | <target> | <message>
                else if (e.ChatMessage.Message.StartsWith("!action", StringComparison.InvariantCultureIgnoreCase))
                {
                    var chatArray = RemoveCommand("!action", e.ChatMessage.Message).Split('|');

                    //TODO: Remove beta
                    /*if (!BetaCheck(e.ChatMessage.Username))
                    {
                        throw new ArgumentException($"Sorry, only beta testers allowed right now. Use your Loyalty Points to become a beta tester!");
                    }*/

                    if (chatArray.Length < 3)
                    {
                        throw new ArgumentException($"Make sure you're entering your command as !action <action> | <target> | <message>.");
                    }

                    // No Exception needed for target as a skill can just be an AOE or room
                    if (chatArray[0] == "")
                    {
                        throw new ArgumentException($"You need to pick an action.");
                    }

                    if (creating == false && voting == true)
                    {
                        throw new ArgumentException($"It's voting time, save your actions for next turn.");
                    }

                    if (creating == false && voting == false)
                    {
                        throw new ArgumentException($"Wait until the next turn.");
                    }

                    if (ActionCheck($"action", e.ChatMessage.Username))
                    {
                        client.SendMessage($"You want to {chatArray[0]} on {chatArray[1]} saying \"{chatArray[2]}\".");
                        skillId++;
                        TakeAction("action", $"{chatArray[0]} - {chatArray[1]}", chatArray[2], e.ChatMessage.Username, skillId);
                    }
                    else
                    {
                        client.SendMessage($"You already chose to use a skill.");
                    }
                }
                //!halcommands
                else if (e.ChatMessage.Message.StartsWith("!halcommands", StringComparison.InvariantCultureIgnoreCase))
                {
                    client.SendMessage($"https://fluffsmckenzie.github.io/Haldori");
                }
                //!charsheet
                else if (e.ChatMessage.Message.StartsWith("!charsheet", StringComparison.InvariantCultureIgnoreCase))
                {
                    client.SendMessage($"https://fluffsmckenzie.github.io/Haldori/charsheet");
                }
                //!vote <actiontype> <number>
                else if (e.ChatMessage.Message.StartsWith("!vote", StringComparison.InvariantCultureIgnoreCase))
                {
                    string[] actions = { "talk", "attack", "skill" };

                    if(voting == false && creating == true)
                    {
                        throw new ArgumentException($"It's not time to vote yet.");
                    }

                    if (voting == false && creating == false)
                    {
                        throw new ArgumentException($"Wait until the next turn.");
                    }

                    //TODO: Remove beta
                    /*if (!BetaCheck(e.ChatMessage.Username))
                    {
                        throw new ArgumentException($"Sorry, only beta testers allowed right now. Use your Loyalty Points to become a beta tester!");
                    }*/

                    var vote = RemoveCommand("!vote", e.ChatMessage.Message);
                    var action = Regex.Replace(vote, "[^A-Za-z.]", "").ToLower();
                    var number = Regex.Replace(vote, "[^0-9.]", "");

                    // TODO: Get data and check if someone has already submitted
                    //if (actions.Contains(action))
                    //{
                     //   if (VoteCheck(e.ChatMessage.Username))
                       // {
                            client.SendMessage($"You have voted to {action} using option #{number}");
                            VoteAction(action, e.ChatMessage.Username, number);
                        //}
                        //else
                        //{
                         //   client.SendMessage($"You have already voted.");
                        //}
                    //}
                    //else
                    //{
                      //  client.SendMessage($"That's not a valid action to vote on.");
                    //}
                }
                else if (e.ChatMessage.Message.StartsWith("!introduction", StringComparison.InvariantCultureIgnoreCase) && ModCheck(e.ChatMessage.Username))
                {
                    client.SendMessage($"Hello, I am Haldori! It's nice to meet you!");
                }
                //END D&D COMMANDS
                //START JACKBOX RELATED COMMANDS
                else if(e.ChatMessage.Message.StartsWith("!jbpoll", StringComparison.InvariantCultureIgnoreCase))
                {
                    if(voting == true)
                    {
                        throw new ArgumentException($"We already got a poll! Don't make me call you Door!");
                    }
                    var vote = RemoveCommand("!jbpoll", e.ChatMessage.Message);
                    var number = Regex.Replace(vote, "[^0-9.]", "");
                    if(number == "")
                    {
                        number = "90";
                    }
                    var gid = RandomString(10);
                    JbRegisterGame(gid, TwitchInfo.ChannelName);
                    if (!ModCheck(e.ChatMessage.Username))
                    {
                        throw new ArgumentException($"Sorry only mods can start a Jackbox poll");
                    }
                    client.SendMessage($"Vote on the next game of Jackbox using !jbvote and the number. You have {number} seconds! 1 - YDKJ | 2 - Split the Room | 3 - Mad Verse City | 4 - Patently Stupid | 5 - Zeeple Dome");
                    votingPhase(System.Convert.ToInt32(number) * 1000);

                }
                else if(e.ChatMessage.Message.StartsWith("!jbvote", StringComparison.InvariantCultureIgnoreCase))
                {
                    if(voting == false)
                    {
                        throw new ArgumentException($"It's not voting time, shame @{e.ChatMessage.Username}!");
                    }

                    var query = $"SELECT `game_id` FROM `games` WHERE `channel` = '{TwitchInfo.ChannelName}';";
                    var gId = RetrieveGameId(query, jbdbconnection);
                    if (!CheckIfDataExists($"SELECT * FROM `votes` WHERE `username` = '{e.ChatMessage.Username}' AND `game` = '{gId}';", jbdbconnection))
                    {
                        var vote = RemoveCommand("!jbvote", e.ChatMessage.Message);
                        var number = Regex.Replace(vote, "[^0-9.]", "");
                        if (number == "")
                        {
                            number = "0";
                        }
                        var intNum = System.Convert.ToInt32(number);
                        if (intNum < 1 || intNum > 5)
                        {
                            throw new ArgumentException($"Not a valid choice, please try again @{e.ChatMessage.Username}.");
                        }

                        WriteToDb($"INSERT INTO `votes` (`username`, `game`, `vote`, `channel`) VALUES ('{e.ChatMessage.Username}', '{gId}', '{number}', '{TwitchInfo.ChannelName}');", jbdbconnection);
                    }
                    else
                    {
                        throw new ArgumentException($"You already voted!");
                    }
                    //END JACKBOX RELATED COMMANDS
                }
            }
            catch (ArgumentException ex)
            {
                client.SendMessage(ex.Message);
            }
        }

            

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine(e.Data);
            Console.WriteLine("Connected");
            
        }

        private void Client_OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            Console.WriteLine($"Error!! {e.Error}");
        }


        internal void Disconnect()
        {
            Console.WriteLine("Disconnecting");
        }

    }
}