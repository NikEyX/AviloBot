using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Xml.Schema;
using Bot.AI;
using Google.Protobuf.Reflection;
using SC2APIProtocol;

namespace Bot {
    
    public static class Controller {
        //private const int frameDelay = 20; 
        public static int delay = 0;
        
        //don't edit
        private static ResponseObservation obs;
        private static List<SC2APIProtocol.Action> actions;
        private static ResponseGameInfo gameInfo;
        private static Random random = new Random();
        private static double FRAMES_PER_SECOND = 22.4;        
        private static bool lateGame = false;
        
        public static ulong frame = 0;        
        public static int possiblePlayers = 0;
        public static uint currentSupply = 0;
        public static uint armyCount = 0;
        public static uint armySupply = 0;
        public static uint maxSupply = 0;
        public static uint minerals = 0;
        public static uint vespene = 0;
        public static uint playerID = 0;
        public static int score = 0;
        public static Race enemyRace = Race.Random;
        public static Vector3 mapCenter = new Vector3();
        public static string mapName = "Unknown";

        public static Vector3 startLocation = Vector3.Zero;
        
//        public static Dictionary<uint, List<ulong>> scouted = new Dictionary<uint, List<ulong>>();
        public static Dictionary<uint, List<Vector3>> scouted = new Dictionary<uint, List<Vector3>>(); //HACK
        
        public static Dictionary<Vector3, uint> enemyLocations = new Dictionary<Vector3, uint>();                 
        public static List<Vector3> expansionLocations = new List<Vector3>();
        
        private static Dictionary<ulong, float> lastConstructionState = new Dictionary<ulong, float>();        
        public static List<Unit> completedConstruction = new List<Unit>();
        public static List<Unit> haltedConstruction = new List<Unit>();       
        public static List<ulong> killedUnitTags = new List<ulong>();
        
        public class UnitsHolder {
            public List<Unit> all = new List<Unit>();
            public List<Unit> workersAndArmy = new List<Unit>();
            public List<Unit> workers = new List<Unit>();
            public List<Unit> army = new List<Unit>();

            public List<Unit> marines = new List<Unit>();
            public List<Unit> marauders = new List<Unit>();
            public List<Unit> siegeTanks = new List<Unit>();
            public List<Unit> vikings = new List<Unit>();
            public List<Unit> hellions = new List<Unit>();
            public List<Unit> hellbats = new List<Unit>();
            public List<Unit> thors = new List<Unit>();

            public List<Unit> liftable = new List<Unit>();
            public List<Unit> barracks = new List<Unit>();
            public List<Unit> factories = new List<Unit>();
            public List<Unit> starports = new List<Unit>();
            public List<Unit> turrets = new List<Unit>();
            public List<Unit> armories = new List<Unit>();
            public List<Unit> engineeringBays = new List<Unit>();
            public List<Unit> depots = new List<Unit>();
            public List<Unit> refineries = new List<Unit>();
            public List<Unit> structures = new List<Unit>();
            public List<Unit> bunkers = new List<Unit>();
            public List<Unit> resourceCenters = new List<Unit>();
            public List<Unit> staticAirDefense = new List<Unit>();
            public List<Unit> staticGroundDefense = new List<Unit>();
            public List<Unit> blips = new List<Unit>();
        }

        public static UnitsHolder ownUnits = new UnitsHolder();
        public static UnitsHolder enemyUnits = new UnitsHolder();
        public static UnitsHolder neutralUnits = new UnitsHolder();
        
        private static Dictionary<ulong, Unit> mappedUnits = new Dictionary<ulong, Unit>();

        static Controller() {
            Logger.Info("Instantiated Controller");
        }

        
        public static void Pause() {
            Console.WriteLine("Press any key to continue...");
            while (Console.ReadKey().Key != ConsoleKey.Enter) {
                //do nothing
            }
        }

        public static List<SC2APIProtocol.Action> CloseFrame() {

            //if (actions.Count > 50) {
            //    Logger.Info("TOO MANY ACTIONS:");
            //    foreach (var action in actions) {
            //        if (action.ActionRaw == null) continue;
            //        if (action.ActionRaw.UnitCommand == null) continue;
            //        Logger.Info("BLEH: {0}", action.ActionRaw.UnitCommand.AbilityId);
            //    }
            //    Controller.Pause();
            //}

            return actions;
        }
        

        public static bool Before(float seconds) {
            var secs = (ulong) (FRAMES_PER_SECOND * seconds);
            return (frame < secs);
        }

        public static bool At(float seconds) {
            var secs = (ulong) (FRAMES_PER_SECOND * seconds);
            return (frame == secs);
        }

        public static bool After(float seconds) {
            var secs = (ulong) (FRAMES_PER_SECOND * seconds);
            return (frame >= secs);
        }
        
        public static bool Each(float seconds) {
            var secs = (ulong) (FRAMES_PER_SECOND * seconds);
            return (frame % secs == 0);
        }

        
        public static int GetMinutes() {
            return (int) (frame / FRAMES_PER_SECOND / 60f);
        }

        private static UnitsHolder PopulateUnitHolder(Alliance alliance) {
            UnitsHolder holder = new UnitsHolder();
            foreach (var sc2Unit in obs.Observation.RawData.Units) {
                if (sc2Unit.Alliance != alliance) continue;                
                var unit = new Unit(sc2Unit);

                //blips
                if (unit.tag == 0) {
                    holder.blips.Add(unit);
                    continue; 
                }

                mappedUnits.Add(unit.tag, unit);
                
                holder.all.Add(unit);

                if (Units.ArmyUnits.Contains(sc2Unit.UnitType)) {                    
                    holder.army.Add(unit);
                    holder.workersAndArmy.Add(unit);
                }

                if (Units.Workers.Contains(sc2Unit.UnitType)) {
                    holder.workers.Add(unit);
                    holder.workersAndArmy.Add(unit);
                }
                
                if (sc2Unit.UnitType == Units.MULE) {
                    //we only really want this in here 
                    holder.workersAndArmy.Add(unit);
                }
                
                if (sc2Unit.UnitType == Units.MARAUDER)
                    holder.marauders.Add(unit);
                
                if (sc2Unit.UnitType == Units.MARINE)
                    holder.marines.Add(unit);
                
                if (Units.SiegeTanks.Contains(sc2Unit.UnitType))
                    holder.siegeTanks.Add(unit);
                
                if (sc2Unit.UnitType == Units.THOR)
                    holder.thors.Add(unit);
                
                if (sc2Unit.UnitType == Units.HELLION)
                    holder.hellions.Add(unit);
                
                if (sc2Unit.UnitType == Units.HELLBAT)
                    holder.hellbats.Add(unit);
                
                if (Units.Vikings.Contains(sc2Unit.UnitType))
                    holder.vikings.Add(unit);

                if (Units.Structures.Contains(sc2Unit.UnitType))
                    holder.structures.Add(unit);

                if (Units.StaticAirDefense.Contains(sc2Unit.UnitType))
                    holder.staticAirDefense.Add(unit);

                if (Units.StaticGroundDefense.Contains(sc2Unit.UnitType))
                    holder.staticGroundDefense.Add(unit);
                                
                if (sc2Unit.UnitType == Units.MISSILE_TURRET)
                    holder.turrets.Add(unit);
                
                if (sc2Unit.UnitType == Units.BUNKER)
                    holder.bunkers.Add(unit);
                
                if (sc2Unit.UnitType == Units.ARMORY)
                    holder.armories.Add(unit);

                if (sc2Unit.UnitType == Units.ENGINEERING_BAY)
                    holder.engineeringBays.Add(unit);
                
                if (Units.Liftable.Contains(sc2Unit.UnitType))
                    holder.liftable.Add(unit);

                if (Units.ResourceCenters.Contains(sc2Unit.UnitType))
                    holder.resourceCenters.Add(unit);
                
                if (sc2Unit.UnitType == Units.REFINERY)
                    holder.refineries.Add(unit);
                
                if ((sc2Unit.UnitType == Units.FACTORY) || (sc2Unit.UnitType == Units.FACTORY_FLYING))
                    holder.factories.Add(unit);
                
                if ((sc2Unit.UnitType == Units.STARPORT) || (sc2Unit.UnitType == Units.STARPORT_FLYING))
                    holder.starports.Add(unit);

                if ((sc2Unit.UnitType == Units.SUPPLY_DEPOT) || (sc2Unit.UnitType == Units.SUPPLY_DEPOT_LOWERED))
                    holder.depots.Add(unit);

                if ((sc2Unit.UnitType == Units.BARRACKS) || (sc2Unit.UnitType == Units.BARRACKS_FLYING))
                    holder.barracks.Add(unit);
            }

            return holder;
        }



        public static void RemoveEnemyLocations() {            
            //removing one at a time
            foreach (var enemyLocation in enemyLocations.Keys) {
                foreach (var unit in ownUnits.workersAndArmy) {
                    if (unit.GetDistance(enemyLocation, true) > 3) continue;

                    bool found = false;
                    foreach (var enemyStructures in enemyUnits.structures) {
                        if (enemyStructures.GetDistance(enemyLocation, true) < 2)
                            found = true;
                    }
                    
                    if (!found) {
                        enemyLocations.Remove(enemyLocation);
                        return;
                    }
                }
            }
        }

        public static bool CanPlace(ulong workerTag, uint unitType, Vector3 targetPos) {
            int ability;
            if (unitType == Units.TECHLAB) //hack for addons
                ability = 421;
            else
                ability = Units.ToAbility[unitType];
            
            RequestQueryBuildingPlacement queryBuildingPlacement = new RequestQueryBuildingPlacement();
            queryBuildingPlacement.AbilityId = ability;
            queryBuildingPlacement.PlacingUnitTag = workerTag;
            queryBuildingPlacement.TargetPos = new Point2D();
            queryBuildingPlacement.TargetPos.X = targetPos.X;
            queryBuildingPlacement.TargetPos.Y = targetPos.Y;
            
            Request requestQuery = new Request();
            requestQuery.Query = new RequestQuery();
            requestQuery.Query.Placements.Add(queryBuildingPlacement);

            var result = Program.gc.SendQuery(requestQuery.Query);
            if (result.Result.Placements.Count > 0)
                return (result.Result.Placements[0].Result == ActionResult.Success);

            return false;
        }
        
        
        public static void AddNewEnemyLocations() {            
            foreach (var structure in enemyUnits.structures) {

                var position = new Vector3(structure.position.X, structure.position.Y, 0); 
                if (enemyLocations.ContainsKey(position)) continue;

                enemyLocations[position] = structure.unitType;
                Logger.Info("Discovered enemy structure: {0} @ {1} ", structure, position);
            }
        }

        private static void EvaluateConstructionState() {
            //note this is only live for ONE frame basically
            Dictionary<ulong, float> currentConstructionState = new Dictionary<ulong, float>();
            
            completedConstruction.Clear();
            haltedConstruction.Clear();
            
            foreach (var building in ownUnits.structures) {
                currentConstructionState.Add(building.tag, building.buildProgress);

                if (lastConstructionState.ContainsKey(building.tag)) {
                    if ((lastConstructionState[building.tag] < 1.0f) && (building.buildProgress >= 1.0f))
                        completedConstruction.Add(building);
                    else if ((Math.Abs(lastConstructionState[building.tag] - building.buildProgress) < 1e-7) && (building.buildProgress < 1.0f))
                        haltedConstruction.Add(building);
                }
            }

            lastConstructionState = currentConstructionState;
        }

        public static bool HasActionAttached(Unit unit) {
            foreach (var action in actions) {
                if (action.ActionRaw == null) continue;
                if (action.ActionRaw.UnitCommand == null) continue;
                if (action.ActionRaw.UnitCommand.UnitTags == null) continue;
                if (action.ActionRaw.UnitCommand.UnitTags.Contains(unit.tag))
                return true;
            }
            return false;
        }

        
        private static Race UpdateEnemyRace() {
            if (enemyRace != Race.Random) 
                return enemyRace;
                        
            //enemyRace is always initialized as Random
            foreach (var playerInfo in gameInfo.PlayerInfo) {
                if (playerInfo.PlayerId == playerID) continue;

                if (playerInfo.RaceRequested != Race.Random) {                    
                    return playerInfo.RaceRequested;
                }
                else {
                    //we need to wait till we find any enemy unit
                    foreach (var enemyUnit in enemyUnits.all) {
                        if (Units.Terran.Contains(enemyUnit.unitType)) {                            
                            Logger.Info("Determined Enemy Race: TERRAN");
                            return Race.Terran;
                        }
                        else if (Units.Zerg.Contains(enemyUnit.unitType)) {                            
                            Logger.Info("Determined Enemy Race: ZERG");
                            return Race.Zerg;
                        }
                        else if (Units.Protoss.Contains(enemyUnit.unitType)) {                            
                            Logger.Info("Determined Enemy Race: PROTOSS");
                            return Race.Protoss;
                        }
                    }                    
                }
            }
            return enemyRace;
        }


        private static Unit GetSameClusterResource(List<Unit> currentCluster, List<Unit> resourceFields) {
            foreach (var field in resourceFields) {
                foreach (var current in currentCluster) {
                    if (currentCluster.Contains(field)) continue;
                    
                    if (current.GetDistance(field) < 7)
                        return field;
                }
            }
            return null;
        }

        private static List<Vector3> GetExpansionLocations() {
            List<Vector3> expansionsClusters = new List<Vector3>();
            var resourceFields = GetUnits(Units.MineralFields, Alliance.Neutral).Union(GetUnits(Units.GasGeysers, Alliance.Neutral)).ToList();
            List<Unit> used = new List<Unit>();
            foreach (var field in resourceFields.ToArray()) {
                if (used.Contains(field)) continue;
                
                List<Unit> cluster = new List<Unit>();
                var neighborField = field; 
                while (neighborField != null) {
                    cluster.Add(neighborField);
                    used.Add(neighborField);
                    neighborField = GetSameClusterResource(cluster, resourceFields);
                }

                var avg = Vector3.Zero;
                foreach (var unit in cluster)
                    avg += unit.position;
                avg /= cluster.Count;
                
                expansionsClusters.Add(avg);         
            }
            
            //now sort expansion locations by desirable expansion order
            if (ownUnits.resourceCenters.Count == 0) return expansionsClusters;
            var homeRC = ownUnits.resourceCenters[0];            
            expansionsClusters.Sort((x,y) => homeRC.GetDistance(x).CompareTo(homeRC.GetDistance(y)));
            
            return expansionsClusters;
        }


        public static List<Vector3> GetFreeExpansionLocations() {            
            var potentialExpansions = expansionLocations.ToList();
                                
            foreach (var kv in enemyLocations) {
                var enemyStructure = kv.Value;
                if (!Units.ResourceCenters.Contains(enemyStructure)) continue;

                //known enemyLocations
                var enemyLocation = kv.Key;
                foreach (var potentialLocation in potentialExpansions.ToArray()) {
                    var check = new Vector3(potentialLocation.X, potentialLocation.Y, 0);
                    if (Vector3.Distance(enemyLocation, check) < 10) {
                        potentialExpansions.Remove(potentialLocation);
                    }
                }
                
                //own locations
                foreach (var potentialLocation in potentialExpansions.ToArray()) {
                    foreach (var rc in ownUnits.resourceCenters) {
                        if (rc.isFlying) continue;
                        if (rc.GetDistance(potentialLocation) < 10) {
                            potentialExpansions.Remove(potentialLocation);
                        }
                    }
                }
            }

            return potentialExpansions;      
        }


        public static void OpenFrame(ResponseGameInfo responseGameInfo, ResponseObservation responseObservation) {            
            if (responseObservation == null) {
                Logger.Info("ResponseObservation is null! The application will terminate.");
                Pause();
                Environment.Exit(0);
            }            

            obs = responseObservation;            
            gameInfo = responseGameInfo;
            playerID = responseObservation.Observation.PlayerCommon.PlayerId;
            actions = new List<SC2APIProtocol.Action>();

            frame = responseObservation.Observation.GameLoop;
            armyCount = responseObservation.Observation.PlayerCommon.ArmyCount;
            currentSupply = responseObservation.Observation.PlayerCommon.FoodUsed;            
            armySupply = responseObservation.Observation.PlayerCommon.FoodArmy;
            maxSupply = responseObservation.Observation.PlayerCommon.FoodCap;
            minerals = responseObservation.Observation.PlayerCommon.Minerals;
            vespene = responseObservation.Observation.PlayerCommon.Vespene;
            score = responseObservation.Observation.Score.Score_;

            mappedUnits.Clear();
            ownUnits = PopulateUnitHolder(Alliance.Self);
            enemyUnits = PopulateUnitHolder(Alliance.Enemy);
            neutralUnits = PopulateUnitHolder(Alliance.Neutral);

            //get enemy race
            enemyRace = UpdateEnemyRace();            

            //get killed units tags
            if (obs.Observation.RawData.Event != null) {
                foreach (var tag in obs.Observation.RawData.Event.DeadUnits)
                    killedUnitTags.Add(tag);
            }
                                    
            
            foreach (var alert in obs.Observation.Alerts)
                Logger.Info("ALERT OBS: {0}", alert.ToString());
            
            foreach (var alert in responseObservation.ActionErrors) {
                Logger.Info("ALERT ACTION: {0}, {1}, {2}", alert.AbilityId, alert.UnitTag, alert.Result);
            }


            //LAST ACTIONS
            //foreach (var actions in obs.Actions) {
            //    if (actions == null) continue;
            //    if (actions.ActionRaw == null) continue;
            //    if (actions.ActionRaw.UnitCommand == null) continue;
            //    Logger.Info("ACTION: {0}", actions.ActionRaw.UnitCommand.AbilityId);
            //}

            
            //initialization at first frame
            if (frame == 0) {
                possiblePlayers = responseGameInfo.StartRaw.StartLocations.Count;

                Logger.Info("Possible enemy locations:");
                foreach (var potentialLocation in responseGameInfo.StartRaw.StartLocations) {
                    var location = new Vector3(potentialLocation.X, potentialLocation.Y, 0);
                    enemyLocations.Add(location, Units.COMMAND_CENTER);
                    Logger.Info("--> {0}", location);
                }

                startLocation = ownUnits.resourceCenters[0].position;
                Logger.Info("Own start location: {0}", startLocation);

                expansionLocations = GetExpansionLocations();                
                Logger.Info("Expansion Clusters: {0}", expansionLocations.Count);
                
                mapCenter = new Vector3(gameInfo.StartRaw.MapSize.X, gameInfo.StartRaw.MapSize.Y, 0);
                mapCenter.X /= 2f;
                mapCenter.Y /= 2f;                

                mapName = gameInfo.MapName;
            }


            EvaluateConstructionState();

            //removing any scouted enemy location one at a time if there is nothing
            RemoveEnemyLocations();
            AddNewEnemyLocations();

            //no more known enemy locations? let's check all expansions again
            if (enemyLocations.Count == 0) {
                foreach (var possibleLocation in expansionLocations) {
                    enemyLocations.Add(possibleLocation, Units.COMMAND_CENTER);
                }
            }

            AddScouted();

            if (Each(60))
                Logger.Info("Current Score: {0}", score);

            if (delay > 0) {
                System.Threading.Thread.Sleep(delay);
            }

        }

        public static void AddAction(SC2APIProtocol.Action action) {
            actions.Add(action);
        }


        public static void Chat(string message, bool team=false) {            
            ActionChat actionChat = new ActionChat();
            if (team)
                actionChat.Channel = ActionChat.Types.Channel.Team;
            else
                actionChat.Channel = ActionChat.Types.Channel.Broadcast;
            actionChat.Message = message;
            
            SC2APIProtocol.Action action = new SC2APIProtocol.Action();
            action.ActionChat = actionChat;
            AddAction(action);
        }
        
        
        
        public static SC2APIProtocol.Action CreateRawUnitCommand(int ability) {            
            SC2APIProtocol.Action action = new SC2APIProtocol.Action();
            action.ActionRaw = new ActionRaw();
            action.ActionRaw.UnitCommand = new ActionRawUnitCommand();
            action.ActionRaw.UnitCommand.AbilityId = ability;
            return action;
        }       
        
        public static void Attack(List<Unit> units, Vector3 target, bool queue=false) {
            var action = CreateRawUnitCommand(Abilities.ATTACK);
            action.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.X = target.X;
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = target.Y;
            action.ActionRaw.UnitCommand.QueueCommand = queue;
            foreach (var unit in units)
                action.ActionRaw.UnitCommand.UnitTags.Add(unit.tag);            
            AddAction(action);
        }
        
        public static void Attack(List<Unit> units, Unit target) {
            var action = CreateRawUnitCommand(Abilities.ATTACK);
            action.ActionRaw.UnitCommand.TargetUnitTag = target.tag;
            action.ActionRaw.UnitCommand.QueueCommand = false;
            foreach (var unit in units)
                action.ActionRaw.UnitCommand.UnitTags.Add(unit.tag);            
            AddAction(action);
        }
        

        public static void Stop(List<Unit> units) {        
            var action = CreateRawUnitCommand(Abilities.STOP);
            foreach (var unit in units)
                action.ActionRaw.UnitCommand.UnitTags.Add(unit.tag);            
            AddAction(action);    
        }
        
        public static void Smart(List<Unit> units, Unit targetUnit) {        
            var action = CreateRawUnitCommand(Abilities.SMART);
            action.ActionRaw.UnitCommand.TargetUnitTag = targetUnit.tag;
            foreach (var unit in units) 
                action.ActionRaw.UnitCommand.UnitTags.Add(unit.tag);            
            AddAction(action);    
        }
        
        
        public static void Move(List<Unit> units, Vector3 target, bool queue=false) {
            var action = CreateRawUnitCommand(Abilities.MOVE);
            action.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.X = target.X;
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = target.Y;
            action.ActionRaw.UnitCommand.QueueCommand = queue;
            foreach (var unit in units)
                action.ActionRaw.UnitCommand.UnitTags.Add(unit.tag);
                        
            AddAction(action);
        }

//        private static void AddScouted() {
//            foreach (var enemyUnit in enemyUnits.all) {
//                if (!scouted.ContainsKey(enemyUnit.unitType))
//                    scouted[enemyUnit.unitType] = new List<ulong>();
//
//                if (!scouted[enemyUnit.unitType].Contains(enemyUnit.tag)) {
//                    scouted[enemyUnit.unitType].Add(enemyUnit.tag);
//                    Logger.Info("Scouted: {0}", enemyUnit);
//                    
//                    //HACK BECAUSE TAG DOESN'T WORK
//                    Controller.delay = 40;
//                }
//            }
//        }

        
        //HACK BECAUSE TAG DOESN'T WORK
        private static void AddScouted() { //HACK 
            foreach (var enemyUnit in enemyUnits.structures) {
                if (enemyUnit.isFlying) continue;
                if (!scouted.ContainsKey(enemyUnit.unitType))
                    //scouted[enemyUnit.unitType] = new List<ulong>();
                    scouted[enemyUnit.unitType] = new List<Vector3>();

                var position = enemyUnit.position;
                position.Z = 0;
                
                if (!scouted[enemyUnit.unitType].Contains(position)) {
                    scouted[enemyUnit.unitType].Add(position);
//                    Logger.Info("Scouted: {0} @ {1}", enemyUnit, position);                    
//                    Controller.delay = 40;
                }
            }
        }



        public static int GetScoutedCount(uint unitType) {
            return scouted.ContainsKey(unitType) ? scouted[unitType].Count : 0;
        }



        public static int InTrainingCount(uint unitType) {
            var count = 0;
            var ability = Units.ToAbility[unitType];
            foreach (var building in ownUnits.structures) {
                if (building.order.ability == ability)
                    count++; 

            }
            return count;
        }
        
        public static int InConstructionCount(uint unitType) {
            var count = 0;

            if (Units.AddOns.Contains(unitType)) {
                foreach (var addon in Controller.GetUnits(unitType)) {
                    if (addon.buildProgress < 1)
                        count += 1;
                }
                return count;
            }

            var ability = Units.ToAbility[unitType];
            foreach (var building in ownUnits.workers) {
                if (building.order.ability == ability)
                    count++;
            }
            return count;
        }

        public static bool IsPlanned(uint unitType) {
            var ability = Units.ToAbility[unitType];
            foreach (var worker in ownUnits.workers) {
                if (worker.order.ability == ability) {
                    bool planned = true;
                    var radius = 1.5 * 1.5;
                    foreach (var unit in GetUnits(unitType)) {
                        var check = unit.position;
                        check.Z = 0;

                        if (Vector3.DistanceSquared(check, worker.order.targetPosition) < radius) {
                            planned = false;
                            break;
                        }
                    }                    
                    if (planned)
                        return true;
                }
            }
            return false;
        }

        public static bool IsBeingConstructed(uint unitType) {
            var ability = Units.ToAbility[unitType];
            foreach (var building in ownUnits.workers) {
                if (building.order.ability == ability) 
                    return true;
            }
            return false;
        }

        public static bool IsBeingResearched(int ability, bool orCompleted) {
            //TODO: make this research buildings only for better performance
            foreach (var building in ownUnits.structures) {
                if (building.order.ability == ability) 
                    return true;
            }

            return (orCompleted && 1ched(ability));
        }

        public static bool IsResearched(int ability) {
            //translates to internal SC2 mappings automatically
            uint check;
            if (ability == Abilities.RESEARCH_BANSHEE_CLOAK)
                check = 20;
            else 
                throw new Exception("Ill-defined research ability: " + ability);           
            
            return obs.Observation.RawData.Player.UpgradeIds.Contains(check);
        }

        public static int GetUpgradeLevel(int ability) {
            if (ability == Abilities.RESEARCH_UPGRADE_MECH_ARMOR) {                
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(29)) return 3;
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(28)) return 2;
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(27)) return 1;
                return 0;
            }
            else if (ability == Abilities.RESEARCH_UPGRADE_MECH_GROUND) {                
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(32)) return 3;
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(31)) return 2;
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(30)) return 1;
                return 0;
            }
            else if (ability == Abilities.RESEARCH_UPGRADE_MECH_AIR) {                
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(38)) return 3;
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(37)) return 2;
                if (obs.Observation.RawData.Player.UpgradeIds.Contains(36)) return 1;
                return 0;
            }
            else 
                throw new Exception("Ill-defined upgrade ability: " + ability);
        }

        
        public static List<Unit> GetUnits(uint unitType, Alliance alliance=Alliance.Self) {
            List<Unit> units = new List<Unit>();
            foreach (var unit in obs.Observation.RawData.Units) {
                if ((unit.UnitType == unitType) && (unit.Alliance == alliance))
                    units.Add(new Unit(unit));
            }
            return units;
        }
        
        public static List<Unit> GetUnits(HashSet<uint> hashset, Alliance alliance=Alliance.Self) {
            List<Unit> units = new List<Unit>();                           
            foreach (var unit in obs.Observation.RawData.Units) {
                if ((hashset.Contains(unit.UnitType)) && (unit.Alliance == alliance)) 
                    units.Add(new Unit(unit));
            }
            return units;
        }
        
        public static Unit GetUnitByTag(ulong tag) {
            mappedUnits.TryGetValue(tag, out Unit result);
            return result;
        }


        public static bool IsLateGame() {
            if (lateGame) 
                return true;

            if (Controller.ownUnits.siegeTanks.Count >= 1)
                lateGame = true;
            return lateGame;
        }

        public static int CountStructures(List<Unit> units, bool functional) {
            var count = 0;
            foreach (var building in units) {
                if (building.buildProgress >= 1.0)
                    count += 1;
            }

            if (functional)
                return count;
            else
                return units.Count - count;
        }
        
        public static bool CanConstruct(uint unitType) {        
            if (ownUnits.workers.Count == 0) return false;        
            
            if (unitType == Units.REFINERY)
                return (minerals >= 75);
            
            if (unitType == Units.COMMAND_CENTER)
                return (minerals >= 400);

            //we need rc for every unit after here
            if (CountStructures(ownUnits.resourceCenters, true) < 1) return false;
            
            if (Units.Workers.Contains(unitType))
                return (currentSupply < maxSupply) && (minerals >= 50);
           
            if (unitType == Units.SUPPLY_DEPOT)          
                return (minerals >= 100);
            
            if (CountStructures(ownUnits.depots, true) < 1) return false;
            
            
            if (unitType == Units.ENGINEERING_BAY)          
                return (minerals >= 125);


            if (unitType == Units.MISSILE_TURRET)
                return (CountStructures(GetUnits(Units.ENGINEERING_BAY), true) >= 1) && (minerals >= 100);


            if (unitType == Units.BARRACKS)
                return (minerals >= 150);
            
            if (CountStructures(ownUnits.barracks, true) < 1) return false;
                        
            if (unitType == Units.BUNKER)
                return (minerals >= 100);

            if (unitType == Units.MARINE)
                return (currentSupply < maxSupply) && (minerals >= 50);

            if (unitType == Units.REAPER)
                return (currentSupply < maxSupply) && (minerals >= 50) && (vespene >= 50);
            
            if (unitType == Units.ORBITAL_COMMAND)
                return (minerals >= 150);

            if ((unitType == Units.TECHLAB) || (unitType == Units.BARRACKS_TECHLAB))
                return ((minerals >= 50) && (vespene >= 25));
            
            if ((unitType == Units.REACTOR) || (unitType == Units.BARRACKS_REACTOR))
                return ((minerals >= 50) && (vespene >= 50));
                        
            if (unitType == Units.FACTORY)
                return ((minerals >= 150) && (vespene >= 100));

            if (CountStructures(ownUnits.factories, true) < 1) return false;

            if (unitType == Units.FACTORY_TECHLAB)
                return ((minerals >= 50) && (vespene >= 25));
            
            if (unitType == Units.FACTORY_REACTOR)
                return ((minerals >= 50) && (vespene >= 50));

            if (unitType == Units.ARMORY)
                return ((minerals >= 150) && (vespene >= 100));
            
            if (unitType == Units.CYCLONE)
                return (currentSupply < maxSupply) && (minerals >= 150) && (vespene >= 100);
            
            if (unitType == Units.THOR) 
                return (currentSupply < maxSupply) && (CountStructures(ownUnits.armories, true) >= 1) && ((minerals >= 300) && (vespene >= 200));

            if (unitType == Units.HELLION)
                return (currentSupply < maxSupply) && (minerals >= 100);
            
            if (unitType == Units.SIEGE_TANK)
                return (currentSupply < maxSupply) && (minerals >= 150) && (vespene >= 125);

            if (unitType == Units.STARPORT)
                return ((minerals >= 150) && (vespene >= 100));
            
            if (CountStructures(ownUnits.starports, true) < 1) return false;
                        
            if (unitType == Units.STARPORT_TECHLAB)
                return ((minerals >= 50) && (vespene >= 25));
            
            if (unitType == Units.STARPORT_REACTOR)
                return ((minerals >= 50) && (vespene >= 50));

            
            if (unitType == Units.VIKING_FIGHTER)
                return (currentSupply < maxSupply) && (minerals >= 150) && (vespene >= 75);

            if ((unitType == Units.BANSHEE) || (unitType == Units.RAVEN) || (unitType == Units.BATTLECRUISER)) {
                if (CountStructures(GetUnits(Units.STARPORT_TECHLAB), true) < 1) return false;
                
                if (unitType == Units.BANSHEE)
                    return (currentSupply < maxSupply) && (minerals >= 150) && (vespene >= 100);
                
                if (unitType == Units.RAVEN)
                    return (currentSupply < maxSupply) && (minerals >= 100) && (vespene >= 200);
            }

            throw new Exception("Construction tech tree not defined for: " + Units.GetName(unitType));
        }

        public static List<Unit> GetUnusedVespeneGeysers() {
            List<Unit> vgs = GetUnits(Units.GasGeysers, Alliance.Neutral);
            foreach (var vg in vgs.ToArray()) {
                foreach (var refinery in ownUnits.refineries) {
                    if (refinery.GetDistance(vg) < 2) {
                        if (vgs.Contains(vg))
                            vgs.Remove(vg);
                        break;
                    }
                }
            }
            
            return vgs;
        }



        public static void ConstructOnce(uint unitType) {
            if (Controller.GetUnits(unitType).Count > 0) return;
            if (Controller.InConstructionCount(unitType) > 0) return;
            if (!Controller.CanConstruct(unitType)) return;

            Construct(unitType, Vector3.Zero, Vector3.Zero);
        }

        public static void Construct(uint unitType) {
            Construct(unitType, Vector3.Zero, Vector3.Zero);
        }

        public static void Construct(uint unitType, Vector3 location) {
            Construct(unitType, location, Vector3.Zero);
        }

        public static void Construct(uint unitType, Vector3 location, Vector3 direction) {
            var worker = GetAvailableWorker();
            if (worker == null) return;
            
            if ((unitType == Units.SUPPLY_DEPOT) && (ownUnits.depots.Count < 3)) {
                location = General.GetChokeLocation();
                direction = General.GetChokeDirection();
            }
            else if ((unitType == Units.BARRACKS) && (ownUnits.barracks.Count < 1)) {
                location = General.GetChokeLocation();
                direction = General.GetChokeDirection();
            }
            else if (unitType == Units.BUNKER) {
                location = General.GetChokeLocation();
                direction = General.GetChokeDirection();
            }
            
            //SPECIAL CASE: Refinery
            if (unitType == Units.REFINERY) {
                var vgs = GetUnusedVespeneGeysers();
                Unit closestUnit = null;
                foreach (var cc in ownUnits.resourceCenters) {
                    if (cc.isFlying) continue;                   

                    closestUnit = cc.GetClosestUnit(vgs);
                    if (cc.GetDistance(closestUnit) < 15) break;
                }
                
                if (closestUnit == null) return;
                                                
                Logger.Info("Constructing: {0} @ {1}", Units.GetName(unitType), closestUnit.position);

                var ability = Units.ToAbility[unitType];
                var constructAction = CreateRawUnitCommand(ability);
                constructAction.ActionRaw.UnitCommand.UnitTags.Add(worker.tag);
                constructAction.ActionRaw.UnitCommand.TargetUnitTag = closestUnit.tag;
                AddAction(constructAction);   
                return;
            }

            Vector3 constructionSpot = Vector3.Zero;
            if (location == Vector3.Zero) {
                for (int radius = 12; radius < 18; radius++) {
                    for (int attempt = 0; attempt < 25; attempt++) {
                        foreach (var cc in ownUnits.resourceCenters) {
                            if (cc.buildProgress < 1) continue;
                            if (cc.isFlying) continue;
                            location = cc.position;

                            constructionSpot = new Vector3(location.X + random.Next(5, radius + 1) * (random.Next(2) * 2 - 1), 
                                                           location.Y + random.Next(5, radius + 1) * (random.Next(2) * 2 - 1), 
                                                           0);

                            if (CanPlace(worker.tag, unitType, constructionSpot)) {
                                if ((unitType != Units.SUPPLY_DEPOT) && (unitType != Units.COMMAND_CENTER)) {
                                    if (CanPlace(worker.tag, unitType, constructionSpot + new Vector3(+2, 0, 0))) {
                                        if (CanPlace(worker.tag, unitType, constructionSpot + new Vector3(-2, 0, 0))) {
                                            if (CanPlace(worker.tag, unitType, constructionSpot + new Vector3(0, +2, 0))) {
                                                if (CanPlace(worker.tag, unitType, constructionSpot + new Vector3(0, -2, 0))) {
                                                    Logger.Info("Constructing: {0} @ {1}", Units.GetName(unitType), constructionSpot);

                                                    var ability = Units.ToAbility[unitType];
                                                    var constructAction = CreateRawUnitCommand(ability);
                                                    constructAction.ActionRaw.UnitCommand.UnitTags.Add(worker.tag);
                                                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
                                                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos.X = constructionSpot.X;
                                                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = constructionSpot.Y;
                                                    AddAction(constructAction);
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                                else {
                                    //supply depot and CC always goes anywhere
                                    Logger.Info("Constructing: {0} @ {1}", Units.GetName(unitType), constructionSpot);

                                    var ability = Units.ToAbility[unitType];
                                    var constructAction = CreateRawUnitCommand(ability);
                                    constructAction.ActionRaw.UnitCommand.UnitTags.Add(worker.tag);
                                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
                                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos.X = constructionSpot.X;
                                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = constructionSpot.Y;
                                    AddAction(constructAction);
                                    return;

                                }
                            }
                        }
                    }
                }
            }
            else {
                //trying to find a valid construction spot
                location.Z = 0;

                Vector3 offset;
                if (direction == Vector3.Zero)
                    offset = Vector3.Zero;
                else 
                    offset = Vector3.Normalize(direction) * 1.5f;
                
                offset.Z = 0;

                Logger.Info("Constructing: {0}", Units.GetName(unitType));
                Logger.Info("--> Offset: " + offset);
                location += offset;

                var radius = 6f;
                Dictionary<float, Vector3> spots = new Dictionary<float, Vector3>();
                for (var x = -radius; x <= radius + 1; x++) {
                    for (var y = -radius; y <= radius + 1; y++) {
                        constructionSpot = new Vector3(location.X + x, location.Y + y, 0);

                        var distance = Vector3.Distance(location, constructionSpot);
                        
                        while (spots.ContainsKey(distance))
                            distance += 0.0001f;
                            
                        spots.Add(distance, constructionSpot);                        
                    }
                }

                var keys = spots.Keys.ToList();
                keys.Sort();
                                
                foreach (var distance in keys) {
                    var spot = spots[distance];
                                        
                    if (!CanPlace(worker.tag, unitType, spot))
                        continue;
                    
                    Logger.Info("Constructing at location: {0} @ {1}", Units.GetName(unitType), location);
                    var ability = Units.ToAbility[unitType];
                    var constructAction = CreateRawUnitCommand(ability);
                    constructAction.ActionRaw.UnitCommand.UnitTags.Add(worker.tag);
                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos.X = spot.X;
                    constructAction.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = spot.Y;
                    constructAction.ActionRaw.UnitCommand.QueueCommand = false;
                    AddAction(constructAction);
                    break;
                }
                
            }
        }

        public static Unit GetAvailableWorker() {
            foreach (var worker in ownUnits.workers) {
                if (worker.order.ability != Abilities.GATHER_RESOURCES) continue;
                if (worker.IsCarryingResources()) continue;
                return worker;
            }
            return null;
        }
        
        
        public static Unit GetAvailableWorker(Vector3 target) {
            var closestDistance = 999d;
            Unit closestUnit = null; 
            foreach (var worker in ownUnits.workers) {
                if (worker.order.ability != Abilities.GATHER_RESOURCES) continue;
                if (worker.IsCarryingResources()) continue;

                var distance = worker.GetDistance(target);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestUnit = worker;
                }
            }
            return closestUnit;
        }                

        
        public static void FocusCamera(Unit unit) {
            if (unit == null) return;
            SC2APIProtocol.Action action = new SC2APIProtocol.Action();
            action.ActionRaw = new ActionRaw();
            action.ActionRaw.CameraMove = new ActionRawCameraMove();
            action.ActionRaw.CameraMove.CenterWorldSpace = new Point();
            action.ActionRaw.CameraMove.CenterWorldSpace.X = unit.position.X;
            action.ActionRaw.CameraMove.CenterWorldSpace.Y = unit.position.Y;
            action.ActionRaw.CameraMove.CenterWorldSpace.Z = unit.position.Z;
            AddAction(action);
        }

    }
}