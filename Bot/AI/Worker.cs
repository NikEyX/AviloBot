using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SC2APIProtocol;

namespace Bot.AI {
    public class Worker : Template {

        public Worker(Unit unit) : base(unit) { }

        private static Dictionary<ulong, List<ulong>> repairTargets = new Dictionary<ulong, List<ulong>>();

        private static bool Maintenance() {
            bool cleaned = false;
            foreach (var kv in repairTargets.ToArray())
            {
                var repairTarget = Controller.GetUnitByTag(kv.Key);
                var repairers = kv.Value;

                //did the repair target die, or was it fully repaired?
                if ((repairTarget == null) || (repairTarget.integrity >= 1)) {
                    //foreach (var repairerTag in repairers.ToArray())
                    //    repairTargets[kv.Key].Remove(repairerTag);
                    repairTargets.Remove(kv.Key);
                    cleaned = true;
                    continue;
                }

                //did one of the repairers die or stop repairing?
                foreach (var repairerTag in repairers.ToArray()) {
                    var repairer = Controller.GetUnitByTag(repairerTag);
                    if (repairer == null) {
                        repairTargets[kv.Key].Remove(repairerTag);
                        cleaned = true;
                    }
                    else if (repairer.order.ability != Abilities.REPAIR) {
                        repairer.Repair(repairTarget);
                        cleaned = true;
                    }
                }
            }
            return cleaned;
        }


        private void Scout(Unit unit) {
            if ((unit.health < 30) || (unit.GetClosestDistance(Controller.enemyUnits.army) < 10)) {
                Log("Retreating (like a moron)");
                role = Roles.NONE;
                unit.GatherResources();
                return;
            }

            if (unit.order.ability != 0) return;            

            foreach (var enemyLocation in Controller.enemyLocations.Keys)
                unit.Move(enemyLocation, true);
        }
        
        
//        private void DefendAgainstEarlyHarrass() {
//            if (Controller.ownUnits.army.Count != 0) return;
//            
//            var unit = GetUnit();
//            if (unit == null) return;
//            
//            Unit closestEnemyUnit = null;
//            double closestDistance = 25;           
//                
//            foreach (var enemyUnit in Controller.enemyUnits.workersAndArmy) {
//                foreach (var ownUnit in Controller.ownUnits.structures) {
//                    var distance = ownUnit.GetDistance(enemyUnit);
//                    if (distance > 15f) continue;
//
//                    if (distance < closestDistance) {
//                        closestDistance = distance;
//                        closestEnemyUnit = enemyUnit;
//                    }
//                }
//            }
//            if (closestEnemyUnit == null) return;           
//
//            if (unit.order != Abilities.ATTACK) {
//                Logger.Info("Attacking enemy unit {0} with {1}", closestEnemyUnit, unit);
//                unit.Attack(closestEnemyUnit);                
//            }
//        }


        private bool Repair(Unit unit) {
            //doing something more useful than gathering resources? skip
            if ((unit.order.ability != 0) && (unit.order.ability != Abilities.GATHER_RESOURCES) && (unit.order.ability != Abilities.RETURN_RESOURCES)) return false;            
            if (unit.order.ability == Abilities.REPAIR) return false;

            //make sure we get a worker without resources
            if (unit.IsCarryingResources()) return false;
                
            var mechanicalUnits = Controller.ownUnits.structures.ToList();
            foreach (var targetUnit in Controller.ownUnits.workersAndArmy) {
                if (Units.Mechanical.Contains(targetUnit.unitType))
                    mechanicalUnits.Add(targetUnit);
            }            

            foreach (var targetUnit in mechanicalUnits) {
                if (targetUnit.buildProgress < 1.0f) continue;
                if (targetUnit.integrity >= 1.0f) continue;
                if (unit.GetDistance(targetUnit) > 25) continue;

                //only repair structures/units that are above the worker in Z terms. To avoid repairing off-placed crap
                if (targetUnit.position.Z < unit.position.Z) continue;


                //ok, we need to repair, let's figure out if this is an emergency
                var requiredRepairers = 1;
                if (Controller.IsLateGame()) {
                    foreach (var enemyUnits in Controller.enemyUnits.army) {
                        //we don't repair if it's late game and there is a massive army crushing us... i.e. let's not donate workers
                        if (targetUnit.GetDistance(enemyUnits) < 10)
                            return false;
                    }
                }
                else {
                    foreach (var enemyUnits in Controller.enemyUnits.army) {
                        if (targetUnit.GetDistance(enemyUnits) < 10) {
                            requiredRepairers += 1;
                            if (requiredRepairers >= 4) break;
                        }
                    }

                }

                
                if (!repairTargets.ContainsKey(targetUnit.tag))
                    repairTargets[targetUnit.tag] = new List<ulong>();
                
                if (repairTargets[targetUnit.tag].Count >= requiredRepairers) continue;
                                
                repairTargets[targetUnit.tag].Add(unit.tag);
                
                unit.Repair(targetUnit);
                Log("Repairing: {0}", targetUnit);
                return true;
            }

            return false;
        }

        private void GatherResources(Unit unit) {   
            if ((unit.order.ability == Abilities.GATHER_RESOURCES) || (unit.order.ability == Abilities.RETURN_RESOURCES)) {
                //check for oversaturation
                
                //but only if we actually have slots left
                bool betterSlot = false;
                foreach (var rc in Controller.ownUnits.resourceCenters)
                    if (rc.assignedHarvesters < rc.idealHarvesters)
                        betterSlot = true;

                if (!betterSlot) {
                    foreach (var refinery in Controller.GetUnits(Units.REFINERY))
                        if (refinery.assignedHarvesters < refinery.idealHarvesters)
                            betterSlot = true;
                }

                if (betterSlot) {
                    foreach (var buff in unit.buffs) {
                        if (Buffs.CarryMinerals.Contains(buff)) {
                            var closestRC = unit.GetClosestUnit(Controller.ownUnits.resourceCenters);
                            if (closestRC.assignedHarvesters > closestRC.idealHarvesters)
                                unit.GatherResources();
                        }
                        else if (Buffs.CarryVespene.Contains(buff)) {
                            var closestVespene = unit.GetClosestUnit(Controller.GetUnits(Units.REFINERY));
                            if (closestVespene.assignedHarvesters > closestVespene.idealHarvesters)
                                unit.GatherResources();
                        }
                    }
                }
            }
            else if (unit.order.ability == 0)
                unit.GatherResources();
        }



        public override void Act() { 
            if (Throttle(5)) return;
            if (Maintenance()) return;

            
            var unit = GetUnit();
            if (unit == null) return;            
            
            if (Repair(unit)) return;

            if (role == Roles.SCOUT) 
                Scout(unit);
            else {
                if (random.Next(10) == 0) {
                    //otherwise too many SCVs end up doing the same thing at the same time. 
                    GatherResources(unit);
                }
            }
            

//            DefendAgainstEarlyHarrass(unit);




        }
        
        
    }
}