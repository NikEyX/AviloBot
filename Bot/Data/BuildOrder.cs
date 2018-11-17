using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Numerics;
using Bot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SC2APIProtocol;

namespace Data {
    
    public class BuildOrder {
        private static readonly string location = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Data", "BuildOrders");

        private Random random = new Random();
        private List<Action> actions = new List<Action>();
        private List<int> completed = new List<int>();
        private int currentAction = 0;

        public enum Automation {
            PILOT,
            BUILD_ORBITAL_COMMANDS,
            BUILD_SUPPLY_DEPOTS,
            BUILD_REFINERIES,
            BUILD_STATIC_DEFENSE,
            HANDLE_UPGRADES,
            BUILD_EXPANSION,
            TRAIN_WORKERS,
            TRAIN_ARMY,
            CAUTIOUS_MULES,
        }

        public Dictionary<Automation, bool> automation = new Dictionary<Automation, bool>();

        public class Action {                        
            public enum ActionTypes {
                CONSTRUCT,
                RESEARCH,
                TRAIN,
                SCOUT,
                ENABLE,
                DISABLE
            }

            public enum ConditionTypes {
                ONCE,
                REQUIRE,
                IF
            }

            public Action(string raw) {
                this.raw = raw;
            }
                        
            private string raw;
            public ConditionTypes conditionType;
            public List<string[]> conditions = new List<string[]>();
            public ActionTypes actionType = ActionTypes.CONSTRUCT;

            public uint targetUnit;
            public int targetAbility;
            public Automation targetAutomation;

            public override string ToString() {
                return raw;
            }
        }


        public BuildOrder() {
            if (!Directory.Exists(location))
                Directory.CreateDirectory(location);
            
            automation[Automation.BUILD_SUPPLY_DEPOTS] = false;
            automation[Automation.BUILD_ORBITAL_COMMANDS] = false;
            automation[Automation.BUILD_EXPANSION] = false;
            automation[Automation.BUILD_REFINERIES] = false;
            automation[Automation.BUILD_STATIC_DEFENSE] = false;
            automation[Automation.HANDLE_UPGRADES] = false;
            automation[Automation.TRAIN_WORKERS] = false;
            automation[Automation.TRAIN_ARMY] = false;
            automation[Automation.CAUTIOUS_MULES] = false;
            automation[Automation.PILOT] = false;
        } 
        
        public static Dictionary<string, BuildOrder> LoadAll() {
            string[] filePaths = Directory.GetFiles(location, "*.csv", SearchOption.TopDirectoryOnly);
            
            Dictionary<string, BuildOrder> results = new Dictionary<string, BuildOrder>();
            foreach (var file in filePaths) {
                string name = Path.GetFileNameWithoutExtension(file);
                results[name] = Load(file);
            }
            return results;
        }

        private static BuildOrder Load(String fn) {            
            var bo = new BuildOrder();            

            using (Microsoft.VisualBasic.FileIO.TextFieldParser parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(fn)) {
                parser.TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited;
                parser.SetDelimiters(",");
                while (!parser.EndOfData) {
                    string[] row = parser.ReadFields();
                    if (row.Length != 3) continue;

                    var condition = row[0].ToUpper().Split(' ');
                    var type = row[1].ToUpper();
                    var target = row[2].ToUpper();                    

                    var boAction = new Action(String.Join(",", row));
                    boAction.conditions.Add(condition);

                    if (condition[0] == "ONCE")
                        boAction.conditionType = Action.ConditionTypes.ONCE;
                    else if (condition[0] == "REQUIRE")
                        boAction.conditionType = Action.ConditionTypes.REQUIRE;
                    else 
                        boAction.conditionType = Action.ConditionTypes.IF;

                    
                    if (type == "CONSTRUCT") {
                        boAction.actionType = Action.ActionTypes.CONSTRUCT; 
                        boAction.targetUnit = Units.GetUnit(target);
                    }
                    else if (type == "TRAIN") {
                        boAction.actionType = Action.ActionTypes.TRAIN;

                        if (target == "WORKER") {
                            boAction.targetUnit = Units.SCV;
                        }
                        else 
                            boAction.targetUnit = Units.GetUnit(target);
                    }
                    else if (type == "RESEARCH") {
                        boAction.actionType = Action.ActionTypes.RESEARCH;
                        boAction.targetAbility = Abilities.GetAbility(target);
                    }
                    else if (type == "SCOUT") {
                        boAction.actionType = Action.ActionTypes.SCOUT;
                    }
                    else if (type == "ENABLE") {
                        boAction.actionType = Action.ActionTypes.ENABLE;
                        if (target == "AUTOORBITAL") 
                            boAction.targetAutomation = Automation.BUILD_ORBITAL_COMMANDS;
                        else if (target == "AUTOWORKERS") 
                            boAction.targetAutomation = Automation.TRAIN_WORKERS;
                        else if (target == "AUTOARMY")
                            boAction.targetAutomation = Automation.TRAIN_ARMY;
                        else if (target == "AUTOREFINERIES")
                            boAction.targetAutomation = Automation.BUILD_REFINERIES;
                        else if (target == "AUTOSUPPLYDEPOTS")
                            boAction.targetAutomation = Automation.BUILD_SUPPLY_DEPOTS;
                        else if (target == "AUTOSTATICDEFENSE")
                            boAction.targetAutomation = Automation.BUILD_STATIC_DEFENSE;                        
                        else if (target == "AUTOUPGRADES")
                            boAction.targetAutomation = Automation.HANDLE_UPGRADES;
                        else if (target == "CAUTIOUSMULES")
                            boAction.targetAutomation = Automation.CAUTIOUS_MULES;
                        else if (target == "AUTOEXPAND")
                            boAction.targetAutomation = Automation.BUILD_EXPANSION;
                        else if (target == "AUTOPILOT")
                            boAction.targetAutomation = Automation.PILOT;
                        else 
                            throw new Exception("Unknown automation: " + target);                    
                    }
                    else if (type == "DISABLE") {
                        boAction.actionType = Action.ActionTypes.DISABLE;
                        throw new Exception("Disabling an automation is not implemented yet.");
                    }
                    else 
                        throw new Exception("Unknown action in build order: " + type);                    

                    bo.actions.Add(boAction);
                }
            }
            return bo;
        }


        public Action GetNextAction() {
            for (int i= 0; i < this.actions.Count; i++) {
                var action = this.actions[i];           

                if (IsCompleted(i)) continue;
                
                currentAction = i;
                Logger.Info("Next Action: {0}", action);
                return action;
            }

            return null;
        }

        public void CompleteAction() {
            completed.Add(currentAction);
        }


        public bool IsCompleted(int actionID) {
            var action = actions[actionID];
            foreach (var condition in action.conditions) {
                if ((action.conditionType == Action.ConditionTypes.ONCE) || (action.conditionType == Action.ConditionTypes.REQUIRE)) {
                    if (completed.Contains(actionID)) return true;
                }
                else if (action.actionType == Action.ActionTypes.RESEARCH) {
                    return Controller.IsBeingResearched(action.targetAbility, true);
                }
                else if (action.actionType == Action.ActionTypes.ENABLE) {
                    return automation[action.targetAutomation];
                }
                else if (action.actionType == Action.ActionTypes.DISABLE) {
                    return !automation[action.targetAutomation];
                }
                else {
                    var lhs = condition[0].Split('.');
                    var alliance = lhs[0] == "SELF" ? Alliance.Self : Alliance.Enemy;
                    var unitType = lhs[1];
                    var conditionType = lhs[2];

                    var operand = condition[1];

                    int x = 0;
                    var rhs = condition[2];
                    Int32.TryParse(rhs, out x);

                    var targetUnitType = Units.GetUnit(unitType);

                    var unitHolder = (alliance == Alliance.Self) ? Controller.ownUnits : Controller.enemyUnits;

                    List<Bot.Unit> units;
                    if (targetUnitType == Units.SUPPLY_DEPOT)
                        units = unitHolder.depots;
                    else if (targetUnitType == Units.BARRACKS)
                        units = unitHolder.barracks;
                    else if (targetUnitType == Units.FACTORY)
                        units = unitHolder.factories;
                    else if (targetUnitType == Units.STARPORT)
                        units = unitHolder.starports;
                    else
                        units = Controller.GetUnits(targetUnitType, alliance);                    

                    var completedCount = 0;
                    foreach (var unit in units)
                        if (unit.buildProgress >= 1)
                            completedCount += 1;

                    if (conditionType == "COUNT") {
                        int inProductionCount = 0;
                        if (alliance == Alliance.Self) {
                            if (Units.Structures.Contains(targetUnitType))
                                inProductionCount = Controller.InConstructionCount(targetUnitType);
                        }

                        var count = completedCount + inProductionCount;

                        if (operand == "<")
                            return !(count < x);
                        else if (operand == ">")
                            return !(count > x);
                        else if (operand == "<=")
                            return !(count <= x);
                        else if (operand == ">=")
                            return !(count >= x);
                    }
                    else
                        throw new Exception("Invalid Condition Type: " + conditionType);

                    
                }
            }
            return false;
        }

        
        //returns true if it blocks
        public bool PerformPreAutomation() {
            //always use mules, but we can save energy for scans in cautious mode
            var minEnergy = (automation[Automation.CAUTIOUS_MULES] || automation[Automation.PILOT]) ? 100 : 50;
            foreach (var cc in Controller.ownUnits.resourceCenters) {
                if (cc.unitType != Units.ORBITAL_COMMAND) continue;                
                if (cc.energy >= minEnergy)
                    cc.CallDownMule();
            }


            if (automation[Automation.BUILD_ORBITAL_COMMANDS] || automation[Automation.PILOT]) {                
                foreach (var cc in Controller.ownUnits.resourceCenters) {
                    if (cc.unitType != Units.COMMAND_CENTER) continue;
                    if (!cc.IsUsable()) continue;
                    if (Controller.CountStructures(Controller.ownUnits.barracks, true) < 1) continue;

                    if (Controller.CanConstruct(Units.ORBITAL_COMMAND))
                        cc.Train(Units.ORBITAL_COMMAND);
                    else
                        return true;
                }
            }

            
            //build new CC if time 
            if (automation[Automation.BUILD_EXPANSION] || automation[Automation.PILOT]) {
                var requiredOCs = Math.Min(Controller.GetMinutes() / 3 + 1, 8);
                if (Controller.ownUnits.resourceCenters.Count < requiredOCs)
                    if (!Controller.IsBeingConstructed(Units.COMMAND_CENTER))
                        if (Controller.CanConstruct(Units.COMMAND_CENTER))
                            Controller.Construct(Units.COMMAND_CENTER);
                else
                    //FORCE CC!
                    return true;
            }
            
            
            if (automation[Automation.BUILD_SUPPLY_DEPOTS] || automation[Automation.PILOT]) {
                if (Controller.maxSupply < 200) {
                    //keep on building depots if supply is tight
                    var supplyThreshold = 7;
                    if (Controller.currentSupply >= 30) supplyThreshold = 8;
                    if (Controller.currentSupply >= 50) supplyThreshold = 10;
                    if (Controller.currentSupply >= 70) supplyThreshold = 15;
                    if (Controller.currentSupply >= 90) supplyThreshold = 20;
                    if (Controller.currentSupply >= 100) supplyThreshold = 25;
                
                    if (Controller.maxSupply - Controller.currentSupply <= supplyThreshold) {          
                        var parallelConstructionCount = 1;
                        if (Controller.currentSupply > 50) parallelConstructionCount = 2;
                        if (Controller.currentSupply > 100) parallelConstructionCount = 3;
                        
                        if (Controller.InConstructionCount(Units.SUPPLY_DEPOT) < parallelConstructionCount) {
                            if (Controller.CanConstruct(Units.SUPPLY_DEPOT)) {
                                Controller.Construct(Units.SUPPLY_DEPOT);
                            }
                        }
                    }
                }
            }

            
            if (automation[Automation.BUILD_REFINERIES] || automation[Automation.PILOT]) {
                if ((Controller.vespene < 200) && (!Controller.IsBeingConstructed(Units.REFINERY)) && (Controller.CanConstruct(Units.REFINERY))) {
                    List<Bot.Unit> eligibleVespeneGeysers = new List<Bot.Unit>();
                    List<Bot.Unit> vgs = Controller.GetUnits(Units.GasGeysers, Alliance.Neutral);
                    foreach (var rc in Controller.ownUnits.resourceCenters) {
                        if (Controller.ownUnits.barracks.Count == 0) continue;
                        if (rc.buildProgress < 1) continue;
                        if (rc.isFlying) continue;                        
                        if (!rc.AtExpansion()) continue;

                        //forget about it, if this it auto pilot in early game
                        if ((automation[Automation.PILOT]) && (Controller.ownUnits.refineries.Count >= 1) && (Controller.ownUnits.barracks.Count == 1) && (Controller.ownUnits.factories.Count == 0)) continue;
                                            
                        foreach (var vg in vgs) {
                            if (vg.displayType == DisplayType.Hidden) continue;
                            if (eligibleVespeneGeysers.Contains(vg)) continue;
                            if (Math.Abs(vg.position.Z - rc.position.Z) > 0.25) continue;
                            if (vg.GetDistance(rc) < 10) {
                                bool eligible = true;
                                foreach (var refinery in Controller.ownUnits.refineries) {
                                    if (refinery.GetDistance(vg) < 2) {
                                        eligible = false;
                                        break;
                                    }
                                }   
                            
                                if (eligible)
                                    eligibleVespeneGeysers.Add(vg);
                            }
                        }                                     
                    }

                    if (eligibleVespeneGeysers.Count > 0)
                        Controller.Construct(Units.REFINERY);
                }
            }

            return false;
        }


        private void AddStaticDefense() {
            if (!Controller.CanConstruct(Units.MISSILE_TURRET)) return;                
            if (Controller.IsPlanned(Units.MISSILE_TURRET)) return;
            
            List<Bot.Unit> vgs = Controller.GetUnits(Units.GasGeysers, Alliance.Neutral);
            foreach (var rc in Controller.ownUnits.resourceCenters) {
                if (rc.isFlying) continue;
                if (rc.buildProgress < 1) continue;
                if (!rc.AtExpansion()) continue;

                foreach (var expansionLocation in Controller.expansionLocations) {
                    if (!rc.InRange(expansionLocation, 7)) continue;

                    var targetLocation = expansionLocation + Vector3.Normalize(expansionLocation - rc.position) * 5;
                            
                    bool exists = false;
                    foreach (var turret in Controller.ownUnits.turrets) {
                        if (turret.InRange(targetLocation, 3)) {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists) {
                        Controller.Construct(Units.MISSILE_TURRET, targetLocation);
                        return;
                    }
                }
                        
                for (int i=0; i < 2; i++) {
                    var closestGesyer = rc.GetClosestUnit(vgs);
                    vgs.Remove(closestGesyer);

                    if (closestGesyer.InRange(Controller.ownUnits.turrets, 3))
                        continue;

                    //BUILD TURRETS HERE
                    Controller.Construct(Units.MISSILE_TURRET, closestGesyer.position);
                    return;
                }
            }
        }

        public bool PerformPostAutomation() {
            if (automation[Automation.TRAIN_WORKERS] || automation[Automation.PILOT]) {                
                foreach (var cc in Controller.ownUnits.resourceCenters) {
                    if (Controller.ownUnits.workers.Count > 60) continue;
                    if (cc.assignedHarvesters > cc.idealHarvesters) continue;
                
                    //hack to be able to build OC immediately
                    if ((Controller.ownUnits.barracks.Count == 1) && (Controller.ownUnits.barracks[0].buildProgress > 0.8) && (Controller.ownUnits.barracks[0].buildProgress < 1.0)) continue;
                    if (!cc.IsUsable()) continue;
                
                    //i.e. avoid CCs constructed not nearby minerals
                    if (cc.GetClosestDistance(Controller.expansionLocations) > 10) continue;
                
                    if (Controller.CanConstruct(Units.SCV))
                        cc.Train(Units.SCV);
                }            
            }
            
            if (automation[Automation.BUILD_STATIC_DEFENSE] || automation[Automation.PILOT])
                AddStaticDefense();
                                
            
            if (automation[Automation.HANDLE_UPGRADES] || automation[Automation.PILOT]) {                                
                foreach (var armory in Controller.ownUnits.armories) {
                    if (!armory.IsUsable()) continue;

                    if (Controller.GetUpgradeLevel(Abilities.RESEARCH_UPGRADE_MECH_GROUND) < 3)
                        armory.Research(Abilities.RESEARCH_UPGRADE_MECH_GROUND);
                    else if (Controller.GetUpgradeLevel(Abilities.RESEARCH_UPGRADE_MECH_ARMOR) < 3)
                        armory.Research(Abilities.RESEARCH_UPGRADE_MECH_ARMOR);
                    //else if (Controller.GetUpgradeLevel(Abilities.RESEARCH_UPGRADE_MECH_AIR) < 3)
                    //    armory.Research(Abilities.RESEARCH_UPGRADE_MECH_AIR);
                }                       
            }      
            
            if (automation[Automation.TRAIN_ARMY] || automation[Automation.PILOT])
                TrainArmy();
           
            if  (automation[Automation.PILOT])
                ConstructBuildings();


            return false;
        }


        private void ConstructBuildings() {
            //fast expand
            if ((Controller.Before(60 * 3)) && (Controller.CanConstruct(Units.COMMAND_CENTER))) {
                Controller.Construct(Units.COMMAND_CENTER);
            }

            //construct barracks addons
            foreach (var building in Controller.ownUnits.barracks) {
                if (!Controller.CanConstruct(Units.BARRACKS_REACTOR)) continue;
                if (!building.IsUsable()) continue;
                if (building.addOnTag == 0)
                    building.ConstructAddOn(Units.REACTOR);
            }

            //construct factory addons
            foreach (var building in Controller.ownUnits.factories) {
                if (!Controller.CanConstruct(Units.FACTORY_REACTOR)) continue;
                if (!building.IsUsable()) continue;
                if (building.addOnTag == 0) {
                    var factoryTechLabs = Controller.GetUnits(Units.FACTORY_TECHLAB).Count;
                    var factoryReactors = Controller.GetUnits(Units.FACTORY_REACTOR).Count;

                    if (factoryTechLabs / 2 > factoryReactors)
                        building.ConstructAddOn(Units.REACTOR);
                    else
                        building.ConstructAddOn(Units.TECHLAB);
                }
            }


            //construct starport addons
            foreach (var building in Controller.ownUnits.starports) {
                if (!Controller.CanConstruct(Units.STARPORT_REACTOR)) continue;
                if (!building.IsUsable()) continue;
                if (building.addOnTag == 0) {
                    var starportTechlabs = Controller.GetUnits(Units.STARPORT_TECHLAB).Count;
                    var starportReactor = Controller.GetUnits(Units.STARPORT_REACTOR).Count;

                    if (starportTechlabs / 2 > starportReactor)
                        building.ConstructAddOn(Units.REACTOR);
                    else
                        building.ConstructAddOn(Units.TECHLAB);
                }
            }

            
            //research banshee cloak
            foreach (var techlab in Controller.GetUnits(Units.STARPORT_TECHLAB)) {
                if (!techlab.IsUsable()) continue;
                if (!Controller.IsBeingResearched(Abilities.RESEARCH_BANSHEE_CLOAK, true))
                    techlab.Research(Abilities.RESEARCH_BANSHEE_CLOAK);
            }

            //construct barracks
            var barracks = Controller.ownUnits.barracks.Count;
            var requiredBarracks = 1;
            if ((barracks < requiredBarracks) && (Controller.CanConstruct(Units.BARRACKS)))
                if (Controller.InConstructionCount(Units.BARRACKS) < 1)                                        
                    Controller.Construct(Units.BARRACKS);


            var requiredArmySupply = 0;
            var factories = Controller.ownUnits.factories.Count;
            var starports = Controller.ownUnits.starports.Count;
            
            //we need initial starport
            if ((starports < 1) && (Controller.CanConstruct(Units.STARPORT)))
                if (Controller.InConstructionCount(Units.STARPORT) < 1)
                    Controller.Construct(Units.STARPORT);
            
            //we need initial factory
            if ((factories < 1) && (Controller.CanConstruct(Units.FACTORY)))
                if (Controller.InConstructionCount(Units.FACTORY) < 1)
                    Controller.Construct(Units.FACTORY);

            //construct factories
            var requiredFactories = Math.Min(Controller.ownUnits.resourceCenters.Count * 2, 8);
            requiredArmySupply = Math.Min(Controller.ownUnits.resourceCenters.Count * 7, 50);
            if ((Controller.armySupply > requiredArmySupply) && (factories < requiredFactories) && (Controller.CanConstruct(Units.FACTORY)))
                if (Controller.InConstructionCount(Units.FACTORY) < 1)
                    Controller.Construct(Units.FACTORY);
            
            //construct starports 
            var requiredStarPorts = Math.Min(Controller.ownUnits.resourceCenters.Count, 3);
            requiredArmySupply = Math.Min(Controller.ownUnits.resourceCenters.Count * 20, 50);
            if ((Controller.armySupply > requiredArmySupply) && (starports < requiredStarPorts) && (Controller.CanConstruct(Units.STARPORT)))
                if (Controller.InConstructionCount(Units.STARPORT) < 1)
                    Controller.Construct(Units.STARPORT);            

            //construct engineering bay
            if ((Controller.After(6 * 60)) && (Controller.CanConstruct(Units.ENGINEERING_BAY)) && (Controller.ownUnits.engineeringBays.Count < 1))
                if (Controller.InConstructionCount(Units.ENGINEERING_BAY) < 1)
                    Controller.Construct(Units.ENGINEERING_BAY);

            //construct armory
            if ((Controller.After(7 * 60 + 30)) && (Controller.CanConstruct(Units.ARMORY)) && (Controller.ownUnits.armories.Count < 1))
                if (Controller.InConstructionCount(Units.ARMORY) < 1)
                    Controller.Construct(Units.ARMORY);
        }

        
        Dictionary<string, float> probabilities = new Dictionary<string, float> {
            { "Thors",        -1f },
            { "SiegeTanks", 0.50f },
            { "Banshees",   0.10f },
            { "Vikings",    0.30f },
            { "Hellions",   0.10f },
            { "Marines",    0.10f },
            { "Cyclones",   0.10f },
        };
                        
        private void TrainArmy() {
            //build ARMY. Needs more optimization, i.e. WHAT TO OVERWEIGHT
            if (Controller.enemyRace == Race.Zerg) {
                probabilities["Cyclones"] = 1.0f;
                probabilities["Hellions"] = 0.0f;
                probabilities["Marines"] = 1.0f;
                //probabilities["Thors"] = 0.1f;
            }
            
            //thors
            foreach (var building in Controller.ownUnits.factories) {
                if (!building.IsUsable()) continue;

                if (building.HasTechlab()) {
                    if (Controller.CanConstruct(Units.THOR))
                        if (random.NextDouble() < probabilities["Thors"])
                            building.Train(Units.THOR, true);
                }
            }

            //cyclones
            foreach (var building in Controller.ownUnits.factories) {
                if (!building.IsUsable()) continue;
                if (building.HasTechlab()) continue;
                if (Controller.CanConstruct(Units.CYCLONE))
                    if (random.NextDouble() < probabilities["Cyclones"])
                        building.Train(Units.CYCLONE, true);
            }

            //tanks
            foreach (var building in Controller.ownUnits.factories) {
                if (!building.IsUsable()) continue;

                if (building.HasTechlab()) {
                    if (Controller.CanConstruct(Units.SIEGE_TANK))
                        if (random.NextDouble() < probabilities["SiegeTanks"])
                            building.Train(Units.SIEGE_TANK, true);
                }
            }
            
            //banshees
            foreach (var building in Controller.ownUnits.starports) {
                if (!building.IsUsable()) continue;
                if (!building.HasTechlab()) continue;

                if (Controller.CanConstruct(Units.BANSHEE))
                    if (random.NextDouble() < probabilities["Banshees"])
                        building.Train(Units.BANSHEE, true);
            }

            
            //vikings
            foreach (var building in Controller.ownUnits.starports) {
                if (!building.IsUsable()) continue;
                if (building.HasTechlab()) continue;

                if (Controller.CanConstruct(Units.VIKING_FIGHTER))
                    if (random.NextDouble() < probabilities["Vikings"])
                        building.Train(Units.VIKING_FIGHTER, true);
            }
                       
            
            //hellions
            foreach (var building in Controller.ownUnits.factories) {
                if (!building.IsUsable()) continue;
                if (building.HasTechlab()) continue;
                
                if (Controller.CanConstruct(Units.HELLION))
                    if ((Controller.minerals > 400) || (random.NextDouble() < probabilities["Hellions"]))
                        building.Train(Units.HELLION, true);
            }

            //marines
            foreach (var building in Controller.ownUnits.barracks) {
                if (!building.IsUsable()) continue;

                if (Controller.CanConstruct(Units.MARINE))
                    if ((Controller.minerals > 400) || (random.NextDouble() < probabilities["Marines"]))
                        building.Train(Units.MARINE, true);
            }

        }



        public bool PerformNextAction() {
            var action = GetNextAction();
            if (action == null) return false;

            if (action.actionType == Action.ActionTypes.CONSTRUCT) {
                //Logger.Info("Attempting to construct: {0}", Units.GetName(action.targetUnit));
                
                if (!Controller.CanConstruct(action.targetUnit)) return (action.conditionType == Action.ConditionTypes.REQUIRE);

                if ((action.targetUnit == Units.BARRACKS_TECHLAB) || (action.targetUnit == Units.BARRACKS_REACTOR))  {
                    foreach (var building in Controller.ownUnits.barracks) {
                        if (building.addOnTag != 0) continue;
                        if (!building.IsUsable()) continue;
                        if (action.targetUnit == Units.BARRACKS_TECHLAB)
                            building.ConstructAddOn(Units.TECHLAB);
                        else
                            building.ConstructAddOn(Units.REACTOR);
                        return (action.conditionType == Action.ConditionTypes.REQUIRE);
                    }
                }
                else if ((action.targetUnit == Units.FACTORY_TECHLAB) || (action.targetUnit == Units.FACTORY_REACTOR))  {
                    foreach (var building in Controller.ownUnits.factories) {
                        if (building.addOnTag != 0) continue;
                        if (!building.IsUsable()) continue;
                        if (action.targetUnit == Units.FACTORY_TECHLAB)
                            building.ConstructAddOn(Units.TECHLAB);
                        else
                            building.ConstructAddOn(Units.REACTOR);
                        return (action.conditionType == Action.ConditionTypes.REQUIRE);
                    }
                }
                else if ((action.targetUnit == Units.STARPORT_TECHLAB) || (action.targetUnit == Units.STARPORT_REACTOR))  {
                    foreach (var building in Controller.ownUnits.starports) {
                        if (building.addOnTag != 0) continue;
                        if (!building.IsUsable()) continue;
                        if (action.targetUnit == Units.STARPORT_TECHLAB)
                            building.ConstructAddOn(Units.TECHLAB);
                        else
                            building.ConstructAddOn(Units.REACTOR);
                        return (action.conditionType == Action.ConditionTypes.REQUIRE);
                    }
                }
                else {
                    Controller.Construct(action.targetUnit);              
                    if ((action.conditionType == Action.ConditionTypes.ONCE) || (action.conditionType == Action.ConditionTypes.REQUIRE))
                        CompleteAction();           
                }
            }
            else if (action.actionType == Action.ActionTypes.TRAIN) {
                //Logger.Info("Attempting to train: {0}", Units.GetName(action.targetUnit));
                
                if (!Controller.CanConstruct(action.targetUnit)) return (action.conditionType == Action.ConditionTypes.REQUIRE);
                
                if (Units.Workers.Contains(action.targetUnit)) {
                    foreach (var building in Controller.ownUnits.resourceCenters) {
                        if (!building.IsUsable()) continue;
                        building.Train(action.targetUnit, false);
                        if ((action.conditionType == Action.ConditionTypes.ONCE) || (action.conditionType == Action.ConditionTypes.REQUIRE))
                            CompleteAction();                        
                        return (action.conditionType == Action.ConditionTypes.REQUIRE);
                    }
                }
                else if (Units.FromBarracks.Contains(action.targetUnit)) {
                    foreach (var building in Controller.ownUnits.barracks) {
                        if (!building.IsUsable()) continue;

                        building.Train(action.targetUnit, true);
                        if ((action.conditionType == Action.ConditionTypes.ONCE) || (action.conditionType == Action.ConditionTypes.REQUIRE))
                            CompleteAction();                        
                        return (action.conditionType == Action.ConditionTypes.REQUIRE);
                    }
                }
                else if (Units.FromFactory.Contains(action.targetUnit)) {
                    foreach (var building in Controller.ownUnits.factories) {
                        if (!building.IsUsable()) continue;

                        building.Train(action.targetUnit, true);
                        if ((action.conditionType == Action.ConditionTypes.ONCE) || (action.conditionType == Action.ConditionTypes.REQUIRE))
                            CompleteAction();                        
                        return (action.conditionType == Action.ConditionTypes.REQUIRE);
                    }
                }
                else if (Units.FromStarport.Contains(action.targetUnit)) {
                    foreach (var building in Controller.ownUnits.starports) {
                        if (!building.IsUsable()) continue;

                        building.Train(action.targetUnit, true);
                        if ((action.conditionType == Action.ConditionTypes.ONCE) || (action.conditionType == Action.ConditionTypes.REQUIRE))
                            CompleteAction();                        
                        return (action.conditionType == Action.ConditionTypes.REQUIRE);
                    }
                }
                else 
                    throw new Exception("Unable to find production building for: " + action.targetUnit);
            }
            else if (action.actionType == Action.ActionTypes.RESEARCH) {
                //Logger.Info("Attempting to research: {0}", Abilities.GetName(action.targetAbility));

                if (Controller.IsBeingResearched(action.targetAbility, true)) return (action.conditionType == Action.ConditionTypes.REQUIRE);  
                
                if (action.targetAbility == Abilities.RESEARCH_BANSHEE_CLOAK) {
                    foreach (var techlab in Controller.GetUnits(Units.STARPORT_TECHLAB)) {
                        if (!techlab.IsUsable()) continue;
                        techlab.Research(Abilities.RESEARCH_BANSHEE_CLOAK);
                        return (action.conditionType == Action.ConditionTypes.REQUIRE);
                    }
                }
            }
            else if (action.actionType == Action.ActionTypes.ENABLE) {
                Logger.Info("Enabling Automation: {0}", action.targetAutomation);
                automation[action.targetAutomation] = true;                
                if ((action.conditionType == Action.ConditionTypes.ONCE) || (action.conditionType == Action.ConditionTypes.REQUIRE))
                    CompleteAction();                        
            }
            else if (action.actionType == Action.ActionTypes.DISABLE) {
                Logger.Info("Disabling Automation: {0}", action.targetAutomation);
                automation[action.targetAutomation] = false;                
                if ((action.conditionType == Action.ConditionTypes.ONCE) || (action.conditionType == Action.ConditionTypes.REQUIRE))
                    CompleteAction();                        
            }
            else {
                CompleteAction();
            }
            return false;
        }


    }
}