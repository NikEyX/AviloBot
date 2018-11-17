using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SC2APIProtocol;

namespace Bot
{
    internal class Program
    {
        // Settings for your bot.
        private static Random random = new Random();
        private static Bot bot = new AviloBot();
        private static Race race = Race.Terran;

        private static List<string> mapPool = new List<string> {
            "DreamcatcherLE.SC2Map",
            "DarknessSanctuaryLE.SC2Map",
            "CatalystLE.SC2Map",
            "LostAndFoundLE.SC2Map",
            "RedshiftLE.SC2Map",
            "16BitLE.SC2Map",
            "AcidPlantLE.SC2Map",
        };

        //private static string mapName = mapPool[random.Next(mapPool.Count)];
        private static string mapName = "test.SC2Map";
        //private static string mapName = "CatalystLE.SC2Map";
        //private static string mapName = "16BitLE.SC2Map";
        //private static string mapName = "RedShiftLE.SC2Map";


        //        private static Race enemyRace = Race.Random;
        //private static Race enemyRace = Race.Protoss;
        //private static Race enemyRace = Race.Terran;
        private static Race enemyRace = Race.Zerg;

        private static Difficulty enemyDifficulty = Difficulty.VeryHard;
        //private static Difficulty enemyDifficulty = Difficulty.VeryEasy;

        public static GameConnection gc = null;

        private static void Main(string[] args) {
            try {
                gc = new GameConnection();
                if (args.Length == 0)
                    gc.RunSinglePlayer(bot, mapName, race, enemyRace, enemyDifficulty).Wait();
                else {
                    gc.RunLadder(bot, race, args).Wait();
                }
            }
            catch (Exception ex) {
                Logger.Info(ex.ToString());
            }

            if (gc.steps > 22.4 * 60) {
                //only call if the bot has run for at least 1 minute
                bot.OnGameEnded();
            }

            gc.TerminateSC2();            
            Logger.Info("Terminated.");
        }
    }
}
