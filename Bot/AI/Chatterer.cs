using System;
using System.Collections.Generic;
using SC2APIProtocol;

namespace Bot.AI {
    public static class Chatterer {
        private static Random rng = new Random();     
        
        private static List<string> usedChatIds = new List<string>(); 

        private static List<string> chatHello = new List<string> {
            "",
            "",
            "",
            "",
            "same cheater again..",
            "i know ur stream cheating",
            "playing against a known maphacker.. so much fun!",
            "nice race",
        };
        
        
        private static List<string> chatGG = new List<string> {
            "nice race",
            "FIX UR GAME BLIZZARD!!!",
            "can u be any more blatant!",
            "so blatant",
            "so garbage",
        };

        private static List<string> losing1 = new List<string>() {
            "",
            "",
            "nice stream cheating",
            "U SHOULDN'T KNOW MY UNITS ARE THERE!",
            "??????",
            "LOL",
            "garbage",
            "following my units in the fog of war...",
            "no scout and the perfect counter.. so blatant!",
            "u went 4 this w/out any scouting..."
        };

        private static List<string> winning1 = new List<string>() {
            "",
            "",
            "",
            "u forgot ur playing vs a tactical genius!",
            "saving esports!",
            "it's over little jimmy",
            "who is teaching u this bad play?",
            "gg",
            "gg",
            "leave",
            "uninstall the game bro",
        };
        

        private static List<string> winning2 = new List<string>() {
            "",
            "LOL",
            "it's over little jimmy",
        };


        private static List<string> winning3 = new List<string>() {
            "",
            "MY GOD LEAVE THE GAME ALREADY",
            "i can do this all day",
            "i got plenty of time, u?",
            "did u get ur account boosted?"
        };


        private static void Chat(string id, List<string> list) {
            if (usedChatIds.Contains(id)) return;
            usedChatIds.Add(id);
            
            var msg = list[rng.Next(list.Count)];
            if (msg.Equals("")) 
                return;
            
            Controller.Chat(msg);            
        }

        public static void Act(ResponseGameInfo gameInfo, ResponseObservation obs) {
            
            if (Controller.frame == 0) {
                Chat("hello", chatHello);
            }
            
            if ((Controller.ownUnits.structures.Count == 1) && (Controller.ownUnits.structures[0].integrity < 0.35f)) {
                Chat("gg", chatGG);
            }

            if (Controller.At(10 * 60))
            {
                if (Controller.score > 10000)
                    Chat("winning1", winning1);
                else
                    Chat("losing1", losing1);
            }

            if (Controller.At(15 * 60))
                if (Controller.score > 15000)
                    Chat("winning2", winning2);
                else
                    Chat("losing1", losing1);

            if (Controller.At(20 * 60))
                if (Controller.score > 25000)
                    Chat("winning2", winning3);
                else
                    Chat("losing1", losing1);

        }
        
        //same opponent again.. coincidence???
        //nice ling micro u bronze league trash
        //gg
        //u went 4 this w/out any scouting...
        //leave
        //???
        
        
        //wtf, these were pre-cloaked!
         
    }
}