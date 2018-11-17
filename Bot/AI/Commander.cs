using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Xml.Schema;
using Google.Protobuf.WellKnownTypes;
using SC2APIProtocol;

namespace Bot.AI {
    public static class Commander {
        private static Random random = new Random();
        private static Dictionary<ulong, Template> enlisted = new Dictionary<ulong, Template>();
        private static Dictionary<string, Squad> squads = new Dictionary<string, Squad>();
        private static ulong lastScan = 0;

        private static void Enlist(Unit unit) {
            if (unit == null) return;
            
            Logger.Info("[COMMANDER] Enlisting new unit: {0}", unit);
            if (Units.Workers.Contains(unit.unitType))
                enlisted.Add(unit.tag, new Worker(unit));    
            else if (unit.unitType == Units.MULE)
                enlisted.Add(unit.tag, new Mule(unit));    
            else if (unit.unitType == Units.REAPER)
                enlisted.Add(unit.tag, new Reaper(unit));    
            else if (unit.unitType == Units.BANSHEE)
                enlisted.Add(unit.tag, new Banshee(unit));
            else if (unit.unitType == Units.HELLION)
                enlisted.Add(unit.tag, new Hellion(unit));
            else if (unit.unitType == Units.MARINE)
                enlisted.Add(unit.tag, new Marine(unit));
            else if (unit.unitType == Units.SIEGE_TANK)
                enlisted.Add(unit.tag, new SiegeTank(unit));
            else if (unit.unitType == Units.VIKING_FIGHTER)
                enlisted.Add(unit.tag, new Viking(unit));
            else if (unit.unitType == Units.RAVEN)
                enlisted.Add(unit.tag, new Hellion(unit));
            else if (unit.unitType == Units.THOR)
                enlisted.Add(unit.tag, new Thor(unit));
            else if (unit.unitType == Units.CYCLONE)
                enlisted.Add(unit.tag, new Cyclone(unit));
            else 
                throw new Exception("Cannot enlist unit: " + unit);
        }
        
        private static void Delist(ulong tag) {
            enlisted.Remove(tag);
        }


        private static Squad GetSquad(string squadName) {
            if (squads.ContainsKey(squadName))
                return squads[squadName];
            else
                return null;
        }

        private static void RemoveSquad(string squadName, bool retreat) {
            if (squads.ContainsKey(squadName)) {                 
                var squad = squads[squadName];
                squad.Dismiss(retreat);
                squads.Remove(squadName);
            }
        }

        private static Squad CreateSquad(string squadName) {
            return squads[squadName] = new Squad(squadName);
        }
        
        private static bool InSquad(Unit unit) {            
            foreach (var squad in squads.Values) {
                if (squad.IsMember(unit)) return true;
            }
            return false;
        }

        public static Template GetEnsign(ulong unitTag) {
            enlisted.TryGetValue(unitTag, out Template result);
            return result;
        }
        

        private static Vector3 GetAveragePosition(List<Unit> units) {
            Vector3 averagePosition = Vector3.Zero;
            if (units.Count == 0) return averagePosition;

            foreach (var unit in units)
                averagePosition += unit.position;
            return averagePosition / units.Count;
        }
        
        public static void Act(ResponseGameInfo gameInfo, ResponseObservation obs) {
            //remove KIA units
            foreach (var killedTag in Controller.killedUnitTags) {
                if (enlisted.ContainsKey(killedTag)) {
                    Logger.Info("[COMMANDER] Enlisted unit killed in action: {0}", killedTag);
                    Delist(killedTag);
                }
            }

            //enlist new units
            foreach (var unit in Controller.ownUnits.workersAndArmy) {
                if (enlisted.ContainsKey(unit.tag)) continue;
                Enlist(unit);
            }
            
            //perform actions for each unit
            foreach (var ensign in enlisted.Values)
                ensign.Act();

            
            //perform actions for squad
            foreach (var squad in squads.Values) {
                squad.Act();
            }



            //scout with early worker
            if (Controller.At(30)) {
                var worker = Controller.GetAvailableWorker();
                if ((worker != null) && (enlisted.ContainsKey(worker.tag))) {
                    Logger.Info("[COMMANDER] Sending early worker scout: {0}", worker);
                    var ensign = enlisted[worker.tag];
                    worker.Stop();
                    ensign.SetRole(Roles.SCOUT);
                }
            }


            //rally units "strategically" 
            //if ((Controller.Each(5)) && (Controller.enemyUnits.structures.Count > 0) && (Controller.CountStructures(Controller.ownUnits.resourceCenters, true) >= 1)) {
            //    var closestDistance = 99999d;
            //    Unit closestStructure = null;
            //    foreach (var structure in Controller.ownUnits.resourceCenters) {
            //        if (structure.isFlying) continue;
            //        if (structure.buildProgress < 1.0) continue;
            //        var distance = structure.GetClosestDistance(Controller.enemyUnits.structures);
            //        if (distance < closestDistance) {
            //            closestDistance =  distance;
            //            closestStructure = structure;
            //        }
            //    }

            //    if (closestStructure != null) {
            //        var enemyStructure = closestStructure.GetClosestUnit(Controller.enemyUnits.structures);
            //        var offset = Vector3.Normalize(enemyStructure.position - closestStructure.position) * 3;
            //        var targetPosition = closestStructure.position + offset;

            //        foreach (var armyUnit in Controller.ownUnits.army) {
            //            if (armyUnit.order.ability != 0) continue;
            //            if (!enlisted.ContainsKey(armyUnit.tag)) continue;

            //            var ensign = enlisted[armyUnit.tag];
            //            if (!ensign.IsAvailable()) continue;

            //            armyUnit.Attack(targetPosition);                    
            //        }
            //    }
            //}


            //new rally
            if (Controller.Each(5)) {
                var avgPosition = GetAveragePosition(Controller.ownUnits.structures);
                var closestDistance = 99999d;
                Unit closestStructure = null;
                foreach (var structure in Controller.ownUnits.structures) {
                    if (structure.isFlying) continue;
                    var distance = structure.GetClosestDistance(Controller.enemyUnits.structures);
                    if (distance < closestDistance) {
                        if ((closestStructure == null) || (structure.position.Z + 0.25 < closestStructure.position.Z)) {
                            closestDistance = distance;
                            closestStructure = structure;
                        }
                    }
                }
                
                
                if (closestStructure != null) {
                    var offset = Vector3.Normalize(avgPosition - closestStructure.position) * 3;
                    var targetPosition = closestStructure.position + offset;
                    
                    foreach (var armyUnit in Controller.ownUnits.army) {
                        if (armyUnit.order.ability != 0) continue;
                        if (!enlisted.ContainsKey(armyUnit.tag)) continue;

                        var ensign = enlisted[armyUnit.tag];
                        if (!ensign.IsAvailable()) continue;
                    
                        armyUnit.Attack(targetPosition);                    
                    }
                }
            }

            
            string squadName = "ATTACKSQUAD";             
            var checkSquad = GetSquad(squadName);   
            if (Controller.Each(5 * 1)) {
                if (Controller.GetUnits(Units.CYCLONE).Count >= 6) {
                    var attackSquad = CreateSquad(squadName);

                    foreach (var armyUnit in Controller.ownUnits.army) {
                        if (!enlisted.ContainsKey(armyUnit.tag)) continue;

                        var ensign = enlisted[armyUnit.tag];
                        if (InSquad(armyUnit)) continue;

                        //we only want hellbats, since hellions keep on running away
                        armyUnit.ToHellbat();

                        attackSquad.AddUnit(armyUnit);
                    }

                    Logger.Info("[COMMANDER] Launching coordinated attack");
                    attackSquad.Attack();
                }
            }
                  
            squadName = "PRIMARY";             
            checkSquad = GetSquad(squadName);   
            if (checkSquad != null) {
                var squadUnits = checkSquad.GetUnits();

                //is there a massive fight going on? let's scan       
                if ((Controller.Each(1.5f)) && (Controller.frame - lastScan > 275) && (squadUnits.Count >= 10) && (Controller.ownUnits.army.Count >= 10) && (Controller.ownUnits.army.Count >= 15)) {
                    Vector3 avgEnemyPosition = Vector3.Zero;
                    foreach (var unit in Controller.enemyUnits.army)
                        avgEnemyPosition += unit.position;
                    avgEnemyPosition /= Controller.enemyUnits.army.Count;
                
                    Vector3 avgOwnPosition = Vector3.Zero;
                    foreach (var unit in squadUnits)
                        avgOwnPosition += unit.position;
                    avgOwnPosition /= squadUnits.Count;                

                    if (Vector3.Distance(avgEnemyPosition, avgOwnPosition) < 20) {
                        //SCAN
                        var offset = (avgEnemyPosition - avgOwnPosition) * 1.5f;
                        var targetLocation = avgOwnPosition + offset;

                        foreach (var rc in Controller.ownUnits.resourceCenters) {
                            if (rc.energy < 50) continue;
                            rc.Scan(targetLocation);
                            lastScan = Controller.frame;
                            break;
                        }                    
                    }
                }

                
                //remove squads
                if (squadUnits.Count < 5) {
                    RemoveSquad(squadName, true);
                }

                if ((checkSquad != null) && (checkSquad.IsStuck())) {
                    RemoveSquad(squadName, false);
                }

                if ((Controller.Each(60)) && (Controller.currentSupply == 200)) {
                    RemoveSquad(squadName, false);
                }
            }


            if (Controller.Each(15 * 1) && (checkSquad == null)) {
                var requiredArmy = Math.Max(Controller.GetMinutes() * 8, 80);
                
                if ((Controller.currentSupply >= 195) || (Controller.armySupply > requiredArmy)) {
                    var attackSquad = CreateSquad(squadName);

                    foreach (var armyUnit in Controller.ownUnits.army) {
                        if ((armyUnit.integrity < 1) && (armyUnit.IsMechanical())) continue;
                        if (!enlisted.ContainsKey(armyUnit.tag)) continue;

                        var ensign = enlisted[armyUnit.tag];
                        if (!ensign.IsAvailable()) continue;

                        if (InSquad(armyUnit)) continue;

                        //we only want hellbats, since hellions keep on running away
                        armyUnit.ToHellbat();

                        attackSquad.AddUnit(armyUnit);
                    }

                    Logger.Info("[COMMANDER] Launching coordinated attack");
                    attackSquad.Attack();
                }
            }

            


            squadName = "SECONDARY";             
            var harassSquad = GetSquad(squadName);   
            if (harassSquad == null)
                harassSquad = CreateSquad(squadName);            

            if (harassSquad.GetMemberCount() == 0)
                harassSquad.StandDown();


            if (Controller.Each(20 * 1) && (harassSquad.GetMemberCount() == 0)) {    
                var maxMembers = random.Next(3, 10);
                List<Unit> members = new List<Unit>();
                foreach (var hellion in Controller.ownUnits.hellions.Union(Controller.ownUnits.hellions)) {
                    if (hellion.integrity < 1) continue;

                    var ensign = enlisted[hellion.tag];
                    if (!ensign.IsAvailable()) continue;

                    if (InSquad(hellion)) continue;

                    members.Add(hellion);

                    if (members.Count >= maxMembers) 
                        break;
                }
                    
                
                if (members.Count >= maxMembers) {
                    foreach (var member in members) {
                        member.ToHellion();
                        harassSquad.AddUnit(member);
                    }

                    Logger.Info("[COMMANDER] Launching hellion harassment");
                    harassSquad.Harass();
                }
            }


        }
    }
}


