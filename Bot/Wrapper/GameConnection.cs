using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using SC2APIProtocol;

namespace Bot
{
    public class GameConnection
    {
        private ProtobufProxy proxy = new ProtobufProxy();
        private Process process = new Process();
        private string address = "127.0.0.1";

        public static string starcraftExe;
        public static string starcraftDir;
        public static string starcraftMaps;
        
        public ulong steps = 0;

        public GameConnection()
        {
            ReadSettings();
        }

        public void StartSC2Instance(int port)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo(starcraftExe);
            processStartInfo.Arguments = String.Format("-listen {0} -port {1} -displayMode 0", address, port);
            processStartInfo.WorkingDirectory = Path.Combine(starcraftDir, "Support64");
            Logger.Info("Launching SC2:");
            Logger.Info("--> Working Directory: {0}", processStartInfo.WorkingDirectory);
            Logger.Info("--> Starcraft Executable: {0}", starcraftExe); 
            Logger.Info("--> Arguments: {0}", processStartInfo.Arguments);

            process.StartInfo = processStartInfo;
            process.Start();
        }

        public async Task Connect(int port) {
            var timeout = 60;
            for (int i = 0; i < timeout * 2; i++)
            {
                try {                                        
                    await proxy.Connect(address, port);
                    Logger.Info("--> Connected");
                    return;
                }
                catch (WebSocketException) {
//                    Logger.Info("Failed. Retrying...");
                }
                Thread.Sleep(500);
            }
            Logger.Info("Unable to connect to SC2 after {0} seconds.", timeout);
            throw new Exception("Unable to make a connection.");
        }

        public async Task CreateGame(String mapName, Race enemyRace, Difficulty enemyDifficulty)
        {
            RequestCreateGame createGame = new RequestCreateGame();
            createGame.Realtime = false;

            var mapPath = Path.Combine(starcraftMaps, mapName);
            
            if (!File.Exists(mapPath)) {
                Logger.Info("Unable to locate map: " + mapPath);
                throw new Exception("Unable to locate map: " + mapPath);
            }

            createGame.LocalMap = new LocalMap();
            createGame.LocalMap.MapPath = mapPath;

            PlayerSetup player1 = new PlayerSetup();
            createGame.PlayerSetup.Add(player1);
            player1.Type = PlayerType.Participant;

            PlayerSetup player2 = new PlayerSetup();
            createGame.PlayerSetup.Add(player2);
            player2.Race = enemyRace;
            player2.Type = PlayerType.Computer;
            player2.Difficulty = enemyDifficulty;

            Request request = new Request();
            request.CreateGame = createGame;
            Response response = await proxy.SendRequest(request);
        }

        private void ReadSettings()
        {
            string myDocuments = Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string executeInfo = Path.Combine(myDocuments, "StarCraft II", "ExecuteInfo.txt");            
            if (!File.Exists(executeInfo))
                executeInfo = Path.Combine(myDocuments, "StarCraftII", "ExecuteInfo.txt");           

            if (File.Exists(executeInfo)) {
                string[] lines = File.ReadAllLines(executeInfo);
                foreach (string line in lines)
                {
                    string argument = line.Substring(line.IndexOf('=') + 1).Trim();
                    if (line.Trim().StartsWith("executable"))
                    {
                        starcraftExe = argument;
                        starcraftDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(starcraftExe))); //we need 2 folders down
                        starcraftMaps = Path.Combine(starcraftDir, "Maps");
                    }
                }
            }
            else
                throw new Exception("Unable to find:" + executeInfo + ". Make sure you started the game successfully at least once.");
        }
        
        public async Task<uint> JoinGame(Race race)
        {
            RequestJoinGame joinGame = new RequestJoinGame();
            joinGame.Race = race;

            joinGame.Options = new InterfaceOptions();
            joinGame.Options.Raw = true;
            joinGame.Options.Score = true;

            Request request = new Request();
            request.JoinGame = joinGame;
            Response response = await proxy.SendRequest(request);
            return response.JoinGame.PlayerId;
        }

        public async Task<uint> JoinGameLadder(Race race, int startPort)
        {
            RequestJoinGame joinGame = new RequestJoinGame();
            joinGame.Race = race;
            
            joinGame.SharedPort = startPort + 1;
            joinGame.ServerPorts = new PortSet();
            joinGame.ServerPorts.GamePort = startPort + 2;
            joinGame.ServerPorts.BasePort = startPort + 3;

            joinGame.ClientPorts.Add(new PortSet());
            joinGame.ClientPorts[0].GamePort = startPort + 4;
            joinGame.ClientPorts[0].BasePort = startPort + 5;

            joinGame.Options = new InterfaceOptions();
            joinGame.Options.Raw = true;
            joinGame.Options.Score = true;

            Request request = new Request();
            request.JoinGame = joinGame;

            Response response = await proxy.SendRequest(request);
            return response.JoinGame.PlayerId;
        }

        public async Task Ping()
        {
            await proxy.Ping();
        }

        public async Task RequestLeaveGame()
        {
            Request requestLeaveGame = new Request();
            requestLeaveGame.LeaveGame = new RequestLeaveGame();
            await proxy.SendRequest(requestLeaveGame);
        }

        public async Task SendRequest(Request request)
        {
            await proxy.SendRequest(request);
        }

        public async Task<ResponseQuery> SendQuery(RequestQuery query)
        {
            Request request = new Request();
            request.Query = query;
            Response response = await proxy.SendRequest(request);
            return response.Query;
        }
        

        public async Task Run(Bot bot, uint playerId)
        {
            
            Request gameInfoReq = new Request();
            gameInfoReq.GameInfo = new RequestGameInfo();

            Response gameInfoResponse = await proxy.SendRequest(gameInfoReq);
            
            while (true)
            {
                Request observationRequest = new Request();
                observationRequest.Observation = new RequestObservation();
                Response response = await proxy.SendRequest(observationRequest);

                ResponseObservation observation = response.Observation;

                if (response.Status == Status.Ended || response.Status == Status.Quit)
                    break;
                
                IEnumerable<SC2APIProtocol.Action> actions = bot.OnFrame(gameInfoResponse.GameInfo, observation);

                Request actionRequest = new Request();
                actionRequest.Action = new RequestAction();
                actionRequest.Action.Actions.AddRange(actions);
                if (actionRequest.Action.Actions.Count > 0)
                    await proxy.SendRequest(actionRequest);
                
                Request stepRequest = new Request();
                stepRequest.Step = new RequestStep();
                stepRequest.Step.Count = 1;
                steps += 1;
                await proxy.SendRequest(stepRequest);
            }
            
        }
        
        public async Task RunSinglePlayer(Bot bot, string map, Race myRace, Race enemyRace, Difficulty enemyDifficulty) {
            var port = 5678;
            Logger.Info("Starting SinglePlayer Instance");
            StartSC2Instance(port);
            Logger.Info("Connecting to port: {0}", port);
            await Connect(port);
            Logger.Info("Creating game");
            await CreateGame(map, enemyRace, enemyDifficulty);
            Logger.Info("Joining game");
            uint playerId = await JoinGame(myRace);
            await Run(bot, playerId);
        }

        public async Task RunLadder(Bot bot, Race race, int gamePort, int startPort)
        {
            Logger.Info("Connecting to port: {0}", gamePort);
            await Connect(gamePort);
            uint playerId = await JoinGameLadder(race, startPort);
            await Run(bot, playerId);
            await RequestLeaveGame();
        }

        public async Task RunLadder(Bot bot, Race race, string[] args)
        {
            CLArgs clargs = new CLArgs(args);
            await RunLadder(bot, race, clargs.GamePort, clargs.StartPort);
        }

        public void TerminateSC2() {
            if (!process.HasExited)
                process.Kill();
        }
    }
}
