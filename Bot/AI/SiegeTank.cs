using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Net.Mime;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using SC2APIProtocol;

namespace Bot.AI {
    public class SiegeTank : Template {
        public SiegeTank(Unit unit) : base(unit) { }

        private static Dictionary<ulong, Vector3> designatedDefenders = new Dictionary<ulong, Vector3>();
        private ulong unsiegeFrame = 0;
        private int rangeModifier = random.Next(-1, 2);
        
        private void Defend() {
            if (Controller.Before(60 * 7)) return;
            if (Throttle(23)) return;
            
            var unit = Controller.GetUnitByTag(tag);
            if (unit == null) return;
            
            if (unit.order.ability != 0) return;

            //are we still protecting the right CC?

            bool defender = designatedDefenders.ContainsKey(tag);
            if (defender) {
                if (unit.GetClosestDistance(Controller.ownUnits.resourceCenters) < 5) {
                    if (unit.unitType == Units.SIEGE_TANK) {
                        unit.Siege();
                    }
                    return;
                }
                else {
                    if (unit.unitType == Units.SIEGE_TANK_SIEGED)
                        unit.Unsiege();
                    designatedDefenders.Remove(tag);
                    role = Roles.NONE;
                    return;
                }
            }

            if (unit.unitType == Units.SIEGE_TANK_SIEGED)
                return;

            foreach (var rc in Controller.ownUnits.resourceCenters) {
                if (rc.isFlying) continue;
                if (rc.buildProgress < 1) continue;               
                
                if (rc.GetClosestDistance(Controller.enemyUnits.army) < 10) continue;

                bool defended = false;
                foreach (var kv in designatedDefenders.ToArray()) {
                    var designatedDefender = Controller.GetUnitByTag(kv.Key);
                    if (designatedDefender == null) {
                        designatedDefenders.Remove(kv.Key);
                        continue;                        
                    } 
                        
                    if (rc.GetDistance(kv.Value) <= 5) {
                        defended = true;
                        break;
                    }
                }
                
                if (!defended) {                            
                    var targetPosition = rc.position;
                    targetPosition.X += random.Next(-2, 3);
                    targetPosition.Y += random.Next(-2, 3);
                    Log("Designated defender for: {0}", targetPosition);
                    designatedDefenders[tag] = targetPosition;
                    role = Roles.DEFEND;
                    unit.Move(targetPosition);
                    
//                    Controller.Pause();
                    break;
                }
            }
        }

        
        private void ActOnThreat() {
            if (Throttle(3)) return;

            var unit = Controller.GetUnitByTag(tag);
            if (unit == null) return;

            bool enemyClose = false;
            foreach (var enemyUnit in Controller.enemyUnits.army.Union(Controller.enemyUnits.staticGroundDefense)) {
                if (enemyUnit.isFlying) continue;

                if (unit.GetDistance(enemyUnit) <= unit.groundAttackRange - rangeModifier) {
                    if (role == Roles.DEFEND) {
                        enemyClose = true;
                        break;
                    }

                    if (enemyUnit.position.Z - 0.5 <= unit.position.Z) {
                        enemyClose = true;
                        break;
                    }
                    else {
                        //let's make sure we have vision                        
                        foreach (var visionUnit in Controller.ownUnits.army) {                            
                            if (visionUnit.position.Z < enemyUnit.position.Z - 0.5) continue;
                            if (visionUnit.GetDistance(enemyUnit) > 7)  continue;

                            enemyClose = true;
                            break;
                        }
                    }
                }
            }

            if (enemyClose) {
                unsiegeFrame = Controller.frame + (ulong) random.Next(23 * 3, 23 * 10);
                unit.Siege();
            }
            else {
                bool defender = designatedDefenders.ContainsKey(tag);
                if ((!defender) && (Controller.frame > unsiegeFrame)) 
                    unit.Unsiege();
            }

            if (unit.weaponCooldown < 0.5) {
                var targetUnit = GetPriorityTarget(unit, unit.groundAttackRange);
                if (targetUnit != null)
                    unit.Attack(targetUnit);
            }
        }
        

        private Unit GetPriorityTarget(Unit unit, float maxDistance) {
            Unit closestEnemyUnit = null;
            double closestDistance = maxDistance;
                
            foreach (var enemyUnit in Controller.enemyUnits.all) {
                var distance = unit.GetDistance(enemyUnit);
                if (distance > maxDistance) continue;

                if (enemyUnit.integrity < 1.0) {
                    distance -= 5;
                    distance -= 5 * (1.0 - enemyUnit.integrity);
                }
                    
                if (Units.Workers.Contains(enemyUnit.unitType))
                    distance -= 10;
                    
                if (enemyUnit.unitType == Units.BANELING)
                    distance -= 10;
                
                if (enemyUnit.unitType == Units.SIEGE_TANK)
                    distance -= 5;

                if (enemyUnit.unitType == Units.SIEGE_TANK_SIEGED)
                    distance -= 5;

                if (enemyUnit.energy > 0)
                    distance -= 10;

                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestEnemyUnit = enemyUnit;
                }
            }
            
            return closestEnemyUnit;
        }
        
                
        private void ManageDefense() {
            if (Throttle(5)) return;
            
            var unit = Controller.GetUnitByTag(tag);
            if (unit == null) return;

            //are we already attacking something? nevermind then
            if (unit.order.ability == Abilities.ATTACK) {
                var enemyUnit = Controller.GetUnitByTag(unit.order.targetTag);
                if (enemyUnit != null) {
                    if (enemyUnit.isFlying)
                        if (unit.GetDistance(enemyUnit) <= unit.airAttackRange) return;
                    else
                        if (unit.GetDistance(enemyUnit) <= unit.groundAttackRange) return;
                    
                    //not close to buildings
                    if (enemyUnit.GetClosestDistance(Controller.ownUnits.structures) > 20) 
                        unit.Stop();
                }
            }

            var targetUnit = GetPriorityTarget(unit, 250);
            if (targetUnit == null) return;

            //not anywhere close to our buildings
            var closestDistance = targetUnit.GetClosestDistance(Controller.ownUnits.structures);
            if (closestDistance > 20) return;
            
            if (!Controller.IsLateGame()) {
                //let's not walk down the ramp...mhhkay?
                if (targetUnit.position.Z < unit.position.Z - 0.25f)
                    return;
            }
            
            unit.Attack(targetUnit);
        }

        public override void Act() { 
            Defend();
            ActOnThreat();
            
            //bool defender = designatedDefenders.ContainsKey(tag);
            
            //hack
            if (role == Roles.ATTACK) {
            }
            else if (role == Roles.RETREAT)
                role = Roles.NONE;            
            else if (role == Roles.RETREAT)
                Retreat();
            else {
                ManageDefense();
            }
        }
        
        
    }
}