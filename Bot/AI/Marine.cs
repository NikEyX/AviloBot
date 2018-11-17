﻿using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.PerformanceData;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using SC2APIProtocol;

namespace Bot.AI {
    public class Marine : Template {
        public Marine(Unit unit) : base(unit) { }
        
        private bool ManBunkers() {
            if (Throttle(5)) return false;
            
            var unit = Controller.GetUnitByTag(tag);
            if (unit == null) return false;

            if (Controller.ownUnits.bunkers.Count == 0) return false;

            //no more need for bunkers
            if (Controller.ownUnits.siegeTanks.Count >= 2) return false;

            foreach (var bunker in Controller.ownUnits.bunkers) {
                if (bunker.buildProgress < 1) continue;
                if (bunker.passengers.Count >= 4) continue;
                
                Log("Manning bunker: {0} ({1} passengers)", bunker, bunker.passengers.Count);                
                unit.Smart(bunker);
                return true;
            }

            return false;
        }

        private Unit GetPriorityTarget(Unit unit, float maxDistance) {
            Unit closestEnemyUnit = null;
            double closestDistance = maxDistance;
                
            foreach (var enemyUnit in Controller.enemyUnits.all) {
                var distance = unit.GetDistance(enemyUnit);
                if (distance > maxDistance) continue;

                if (enemyUnit.integrity < 1.0) {
                    distance -= 3;
                    distance -= 3 * (1.0 - enemyUnit.integrity);
                }
                    
                if (enemyUnit.unitType == Units.BANELING)
                    distance -= 5;

                if (enemyUnit.unitType == Units.MUTALISK)
                    distance -= 5;

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
            if (closestDistance > 20)
                return;
            
            //early game
            if (!Controller.IsLateGame()) {
                //let's not walk down the ramp...mhhkay?
                if (targetUnit.position.Z < unit.position.Z - 0.25f)
                    return;
            }
            unit.Attack(targetUnit);
        }

        private void ManageAttack() {
            if (Throttle(5)) return;
            
            var unit = Controller.GetUnitByTag(tag);
            if (unit == null) return;

            var targetUnit = GetPriorityTarget(unit, 15);

            if (targetUnit == null) return;                        
            if (unit.order.ability == Abilities.ATTACK) return;

            unit.Attack(targetUnit);
        }

        
        
        public override void Act() {
            if (ManBunkers()) return;
            
            if (role == Roles.ATTACK) {
                ManageAttack();
            }
            else if (role == Roles.RETREAT)
                Retreat();
            else
                ManageDefense();
        }
        
        
    }
}