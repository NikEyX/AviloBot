using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Bot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SC2APIProtocol;

namespace Data {
    
    public class Map {
        private static readonly string location = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Data", "Maps");
        private string fn;

        public bool win = false;
        public ulong frames = 0;
        public string name = "Unknown";
        public string checksum = "Unknown";
        public string enemyRace = "Unknown";

        public Map() {
            if (!Directory.Exists(location))
                Directory.CreateDirectory(location);
        } 
        
        public Map(ResponseGameInfo gameInfo, uint playerID) {

            this.name = gameInfo.MapName;
            this.checksum = GetCheckSum(Path.Combine(GameConnection.starcraftMaps, gameInfo.LocalMapPath));
            this.fn = Path.Combine(location, name + " on " + DateTime.UtcNow.ToString("yyyy-MM-dd HH.mm.ss.ffff") + ".json").Replace("\\", "/");

            foreach (var playerInfo in gameInfo.PlayerInfo) {
                if (playerInfo.PlayerId != playerID) {
                    if (playerInfo.RaceRequested == Race.Protoss)
                        this.enemyRace = "Protoss";
                    else if (playerInfo.RaceRequested == Race.Terran)
                        this.enemyRace = "Terran";
                    else if (playerInfo.RaceRequested == Race.Zerg)
                        this.enemyRace = "Zerg";
                    else if (playerInfo.RaceRequested == Race.Random)
                        this.enemyRace = "Random";
                    break;
                }
            }
        }
        
        private string GetCheckSum(string filename) {
            byte[] hash;
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filename))
                hash = md5.ComputeHash(stream);
            
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        
        
        public void Save() {
            Logger.Info("Saving Map Data: {0}", fn);            
            string json = JsonConvert.SerializeObject(this);                        
            File.WriteAllText(fn, json);
        }


        public static Map Load(string filename) {
            JObject obj = JObject.Parse(File.ReadAllText(filename));
            
            Map mapData = new Map();
            mapData.name = (string) obj["name"];
            mapData.checksum = (string) obj["checksum"];
            mapData.win = (bool) obj["win"];
            mapData.enemyRace = (string) obj["enemyRace"];
            return mapData;
        }

        public static List<Map> LoadAll() {
            if (!Directory.Exists(location))
                Directory.CreateDirectory(location);
            
            string[] filePaths = Directory.GetFiles(location, "*.json", SearchOption.TopDirectoryOnly);
            
            List<Map> results = new List<Map>();
            foreach (var file in filePaths) {
                results.Add(Load(file));
            }

            return results;
        }
        
        
    }
}