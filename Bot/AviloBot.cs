using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Net.NetworkInformation;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading;
using System.Xml.Schema;
using Google.Protobuf.Collections;
using Microsoft.Win32;
using SC2APIProtocol;
using Action = System.Action;
using System.Numerics;
using Bot.AI;
using Data;


namespace Bot
{
    internal class AviloBot : Bot
    {                
        private Map mapData;

        public void OnGameEnded() {            
            // draws and ties will count as loss 
            bool win = false;
            var buildings = Controller.GetUnits(Units.Structures);
            if ((buildings.Count >= 1) && (buildings[0].integrity > 0.4))
                win = true;
            
            //Logger.Info("Game ended. Determined: {0}", win ? "WIN" : "LOSS");
            
            mapData.win = win;
            mapData.frames = Controller.frame;
            mapData.Save();
        }
        
        
        public IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseGameInfo gameInfo, ResponseObservation obs) {
            Controller.OpenFrame(gameInfo, obs);

            if (Controller.frame == 0) {
                Logger.Info("Initializing AviloBot");
                Logger.Info("--------------------------------------");
                Logger.Info("Map: {0}", gameInfo.MapName);
                Logger.Info("--------------------------------------");

                mapData = new Map(gameInfo, Controller.playerID);
            }


            Chatterer.Act(gameInfo, obs);
            General.Act(gameInfo, obs);

            Commander.Act(gameInfo, obs);


            //hack, because LadderManager doesn't deliver Result
            //if (Controller.After(60 * 5) && (Controller.Each(10)))
            //    OnGameEnded();


            return Controller.CloseFrame();
        }
    }
}













