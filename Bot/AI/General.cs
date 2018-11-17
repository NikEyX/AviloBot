using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Xml.Schema;
using SC2APIProtocol;
using Data;

namespace Bot.AI {
    public static class General {
        
        private const int maxChokeBuildings = 4;

        private static Random random = new Random();
        private static Dictionary<string, BuildOrder> buildOrders;
        private static BuildOrder selectedBuildOrder;

        private static ulong trackChokeUnit = 0;
        private static List<Vector3> trackChokePath = new List<Vector3>();
        private static Vector3 chokeLocation = Vector3.Zero; 
        private static Vector3 chokeDirection = Vector3.Zero;        

        static General() {
            Logger.Info("Loading Build Orders");            
            buildOrders = BuildOrder.LoadAll();

            Logger.Info("Loading Map Knowledge");            
            var mapData = Map.LoadAll();
            Logger.Info("--> Loaded {0} map results", mapData.Count);
        }


        private static Vector3 GetNextExpansionLocation() {   
            var expansionLocations = Controller.GetFreeExpansionLocations();
            
            //var closestLocation = Vector3.Zero;
            //var closestDistance = 9999999d;
            //foreach (var rc in Controller.ownUnits.resourceCenters) {
            //    foreach (var potentialLocation in expansionLocations) {
            //        var closestEnemyDistance = 9999999d;
            //        foreach (var enemyUnit in Controller.enemyUnits.resourceCenters) {
            //            var enemyDistance = Vector3.Distance(enemyUnit.position, potentialLocation);
            //            if (enemyDistance < closestEnemyDistance)
            //                closestEnemyDistance = enemyDistance;
            //        }

            //        var distance = rc.GetDistance(potentialLocation) - closestEnemyDistance;
            //        if (distance < closestDistance) {
            //            closestDistance = distance;
            //            closestLocation = potentialLocation;
            //        }
            //    }
            //}

            return expansionLocations[0];
        }

        private static void FindChoke(Unit rc) {
            if (trackChokeUnit != 0) {                
                var worker = Controller.GetUnitByTag(trackChokeUnit);
                trackChokePath.Add(worker.position);

                if (Math.Abs(trackChokePath.Last().Z - trackChokePath.First().Z) >= 0.15f) {
                    worker.Stop();

                    chokeLocation = trackChokePath[trackChokePath.Count - 2];
                    if (trackChokePath.Count >= 2)
                        chokeDirection = trackChokePath[trackChokePath.Count - 1] - trackChokePath[trackChokePath.Count - 2];  
                    
                    trackChokePath.Clear();
                    trackChokeUnit = 0;                    
                    
                    Logger.Info("Found Choke @ {0} with direction: {1}", chokeLocation, chokeDirection);
                }
            }
            else {
                var worker = Controller.GetAvailableWorker();
                if (worker == null) return;

                if (Controller.enemyLocations.Count == 0) return;
                Logger.Info("Attempting to find choke");

                trackChokeUnit = worker.tag;
                
                var enemyLocation = worker.GetClosestPosition(Controller.enemyLocations.Keys.ToList());                
                worker.Move(enemyLocation);
            }
        }
        
        public static Vector3 GetChokeDirection() {
            return chokeDirection;
        }

        public static Vector3 GetChokeLocation() {
            return chokeLocation;
        }


        public static void HandleDepots() {
            const int operatingDistance = 7;
            foreach (var depot in Controller.ownUnits.depots) {
                bool threatened = false;
                foreach (var enemy in Controller.enemyUnits.army) {
                    if (enemy.GetDistance(depot) < operatingDistance) {
                        threatened = true;
                        break;
                    }
                }
                
                foreach (var enemy in Controller.enemyUnits.workers) {
                    if (enemy.GetDistance(depot) < operatingDistance) {
                        threatened = true;
                        break;
                    }
                }
                
                depot.OperateDepot(threatened);
            }
        }


        private static void ExpandWithOC() {
            //expand by flying existing OC to expansion            
            foreach (var rc in Controller.ownUnits.resourceCenters) {
                if ((rc.unitType != Units.ORBITAL_COMMAND) && (rc.unitType != Units.ORBITAL_COMMAND_FLYING)) continue;                                        

                if (rc.isFlying) {
                    if (rc.order.ability == 0) {
                        var position = GetNextExpansionLocation();

                        if (rc.GetDistance(position) < 5) {
                            rc.Land(position);
                        }
                        else
                            rc.Move(position);
                    }

                }
                else {
                    //lift off relevant OCs
                    if (!rc.IsUsable()) continue;
                    var closestDistance = rc.GetClosestDistance(Controller.expansionLocations);
                    if (closestDistance < 7) continue;

                    Logger.Info("Expanding with {0}", rc);                                                
                    rc.Lift();
                }
            }
        }


        private static bool HandleEmergency() {
            if (Controller.frame % 25 != 0) return false;

            if (Controller.enemyRace == Race.Protoss) {
                //defend vs protoss cheeses
                var nexi = Controller.GetScoutedCount(Units.NEXUS);
                var gateWays = Controller.GetScoutedCount(Units.GATEWAY);
                var starGates = Controller.GetScoutedCount(Units.STARGATE);
                var darkShrines = Controller.GetScoutedCount(Units.DARK_SHRINE);
                
//                Logger.Info("--------------------------------> Spotted {0} gateways.", gateWays);
//                if (gateWays >= 2)
//                    Controller.Pause();
                
                if (Controller.Before(3.0f * 60) && (gateWays >= 3) && (nexi <= 1) && (Controller.ownUnits.bunkers.Count < 1)) {
                    if (Controller.CanConstruct(Units.BUNKER)) {
                        Logger.Info("Defending vs gateway rush. Spotted {0} gateways.", gateWays);
                        Controller.Construct(Units.BUNKER);
                    }
                    return true;
                }

                else if (darkShrines >= 1) {
                    //dark templars
                    if (Controller.GetUnits(Units.ENGINEERING_BAY).Count < 1) {
                        if (Controller.CanConstruct(Units.ENGINEERING_BAY)) {
                            Logger.Info("Defending vs dark templars: Engineering Bay");
                            Controller.Construct(Units.ENGINEERING_BAY);
                        }
                        return true;
                    }
                    if (Controller.GetUnits(Units.MISSILE_TURRET).Count < 2) {
                        if (Controller.CanConstruct(Units.MISSILE_TURRET)) {
                            Logger.Info("Defending vs dark templars: Turrets");
                            Controller.Construct(Units.MISSILE_TURRET);
                        }
                        return true;
                    }
                }

                else if (Controller.Before(3.5f * 60) && (starGates >= 1)) {
                    //oracle
                    if (Controller.GetUnits(Units.ENGINEERING_BAY).Count < 1) {
                        if (Controller.CanConstruct(Units.ENGINEERING_BAY)) {
                            Logger.Info("Defending vs Oracles: Engineering Bay");
                            Controller.Construct(Units.ENGINEERING_BAY);
                        }
                        return true;
                    }
                    if (Controller.GetUnits(Units.MISSILE_TURRET).Count < 2) {
                        if (Controller.CanConstruct(Units.MISSILE_TURRET)) {
                            Logger.Info("Defending vs Oracles: Turrets");
                            Controller.Construct(Units.MISSILE_TURRET);
                        }
                        return true;
                    }
                }

                //forge xxx
                //send a scouter once a minute, maybe just create a reaper
            }
            
            else if (Controller.enemyRace == Race.Zerg) {
                //defend vs zerg cheeses
                var banelingNest = Controller.GetScoutedCount(Units.BANELING_NEST);
                
                if (Controller.Before(3 * 60) && (banelingNest >= 1) && (Controller.ownUnits.bunkers.Count < 1)) {
                    if (Controller.CanConstruct(Units.BUNKER)) {
                        Logger.Info("Defending vs baneling bust");
                        Controller.Construct(Units.BUNKER);
                    }
                    return true;
                }
            }
            
            else if (Controller.enemyRace == Race.Terran) {
                //defend vs terran cheeses
                var barracks = Controller.GetScoutedCount(Units.BARRACKS) + Controller.GetScoutedCount(Units.BARRACKS_FLYING);
                
                if (Controller.After(1.5f * 60) && Controller.Before(3 * 60) && (barracks == 0) && (Controller.ownUnits.bunkers.Count < 1) && (Controller.possiblePlayers <= 2)) {
                    if (Controller.CanConstruct(Units.BUNKER)) {
                        Logger.Info("Defending vs proxy marine cheese");
                        Controller.Construct(Units.BUNKER);                        
                    }
                    return true;
                }
                else if (Controller.Before(3 * 60) && (barracks >= 3) && (Controller.ownUnits.bunkers.Count < 1)) {
                    if (Controller.CanConstruct(Units.BUNKER)) {
                        Logger.Info("Defending vs mass marine cheese");
                        Controller.Construct(Units.BUNKER);
                    }

                    return true;
                }
            }

            return false;
        }
        
                
        private static string SelectBuildOrder() {
            //return "Test";
            //return "Main";

            if (Controller.enemyRace == Race.Random)
                return "Main";
            
            if (Controller.enemyRace == Race.Terran) {
                if (Controller.mapName.ToUpper().Contains("RED"))
                    return "OneBaseBanshees";

                if (Controller.mapName.ToUpper().Contains("ACID"))
                    return "OneBaseBanshees";

                if (Controller.mapName.ToUpper().Contains("DARKNESS"))
                    return "BansheeExpand";
            }
            else if (Controller.enemyRace == Race.Zerg) {
                if (random.NextDouble() < 1.0f / 3.0f) {
                    return "OneBaseBanshees";
                } 
                else {
                    return "OneBaseCyclones";
                }    
            }         
            
            var roll = random.NextDouble();
            if (roll < 1.0f / 3.0f) {
                return "OneBaseBanshees";
            } 
            else if (roll < 2.0f / 3.0f) {
                return "OneBaseCyclones";
            }      
            else  {
                return "BansheeExpand";
            }            
            //else
            //    return "Main";            
        }
        
        public static void Act(ResponseGameInfo gameInfo, ResponseObservation obs) { 
            if (selectedBuildOrder == null) {
                var name = SelectBuildOrder();
                selectedBuildOrder = buildOrders[name];
                Logger.Info("Selected Build Order: {0}", name);
            }

            //FINDING CHOKE
            if ((Controller.At(5)) || (trackChokeUnit != 0)) {
                if (Controller.ownUnits.resourceCenters.Count > 0) {
                    FindChoke(Controller.ownUnits.resourceCenters[0]);
                }
            }


            if ((Controller.completedConstruction.Count > 0) && (!Controller.IsLateGame())) {
                Logger.Info("Completed construction");

                //set rally point. Needs to be for all structures, because addons reset the barracks' rally point for example
                foreach (var structure in Controller.ownUnits.structures) {
                    Vector3 offset = Controller.startLocation - structure.position;
                    Vector3 target = structure.position + Vector3.Normalize(offset) * 5;                    
                    structure.Rally(target);                    
                }                
            }

                            
            //send SCVs to refinery if required. 
            if (Controller.Each(0.5f)) {
                foreach (var refinery in Controller.ownUnits.refineries) {
                    if (Controller.vespene > 200) continue;
                    if (refinery.buildProgress < 1.0f) continue;
                    if (refinery.assignedHarvesters < refinery.idealHarvesters) {                                                
                        var worker = Controller.GetAvailableWorker();
                        worker?.GatherResources();
                    }
                }
            }
            
            //any construction halted? send a new SCV every 5 secs
            if (Controller.Each(5f)) {
                foreach (var structure in Controller.haltedConstruction) {
                    var worker = Controller.GetAvailableWorker(structure.position);
                    worker?.Smart(structure);
                    Logger.Info("Sending {0} to resume construction of: {1}", worker, structure);
                }
            }

            //raise threatened depots and lower safe depots
            if (Controller.ownUnits.depots.Count > 0)
                HandleDepots();


            
            //highest priority
            //train worker if we have less than 6
            foreach (var cc in Controller.ownUnits.resourceCenters) {
                if (Controller.ownUnits.workers.Count > 6) continue;
                if (!cc.IsUsable()) continue;
                
                //i.e. a CC constructed not close to minerals
                if (cc.GetClosestDistance(Controller.expansionLocations) > 10) continue;                
                if (Controller.CanConstruct(Units.SCV))
                    cc.Train(Units.SCV);
            }

            
            //save burning but liftable structures (right now only CC)
            foreach (var building in Controller.ownUnits.resourceCenters) {
                if (!building.IsUsable()) continue;
                if (building.integrity > 0.7) continue;

                if (building.GetClosestDistance(Controller.enemyUnits.all) < 15) {
                    building.Lift();
                }
            }
            
            
            //let's periodically check what the opponent is up to
            if ((Controller.After(7 * 60)) && (Controller.Each(60f))) {
                var freeExpansionLocations = Controller.GetFreeExpansionLocations();

                foreach (var enemyRC in Controller.enemyUnits.resourceCenters) {                    
                    var skip = false;
                    var targetLocation = enemyRC.GetClosestPosition(freeExpansionLocations);
                    foreach (var unit in Controller.ownUnits.all) {
                        if (unit.InRange(targetLocation, 10)) {
                            skip = true;
                            break;
                        }
                    }

                    if (skip)
                        continue;

                    foreach (var rc in Controller.ownUnits.resourceCenters) {
                        if (rc.energy < 50) continue;
                        rc.Scan(targetLocation);
                        break;
                    }
                    break;
                }

            }



            //expand
            ExpandWithOC();


            //unload bunkers once we have tanks
            if (Controller.Each(1)) {
                foreach (var bunker in Controller.ownUnits.bunkers) {
                    if (Controller.IsLateGame()) {
                        bunker.Unload();
                        bunker.Salvage();
                    }
                }
            }
            
            
            foreach (var unit in Controller.enemyUnits.blips) {
                if (unit.isBlip) {
                    Logger.Info("ENEMY IS BLIP: {0}, {1}, {2}", unit, unit.unitType, unit.position);
                }
            }


            
            if (HandleEmergency()) return;

            //we need marines for bunker in emergency
            foreach (var bunker in Controller.ownUnits.bunkers) {
                if (bunker.passengers.Count < 4) {
                    foreach (var building in Controller.ownUnits.barracks) {
                        if (!building.IsUsable()) continue;
                        if (Controller.CanConstruct(Units.MARINE))
                            building.Train(Units.MARINE, true);
                    }

                    //break, in case something goes wrong, e.g. we want to build marines, but got no depots
                    if (Controller.minerals < 200)
                        return;
                }
            }

            

            if (Controller.Each(1)) {
                if (selectedBuildOrder.PerformPreAutomation()) return;
                if (selectedBuildOrder.PerformNextAction()) return;
                if (selectedBuildOrder.PerformPostAutomation()) return;
            }

        }
        
    }
}