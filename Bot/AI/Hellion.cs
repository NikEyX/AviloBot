using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.PerformanceData;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using SC2APIProtocol;

namespace Bot.AI {
    public class Hellion : Template {
        public Hellion(Unit unit) : base(unit) { }
        
        private Unit GetPriorityTarget(Unit unit, float maxDistance) {
            Unit closestEnemyUnit = null;
            double closestDistance = maxDistance;
                
            foreach (var enemyUnit in Controller.enemyUnits.all) {
                var distance = unit.GetDistance(enemyUnit);
                if (distance > maxDistance) continue;
                
                if (Units.Workers.Contains(enemyUnit.unitType))
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

            var targetUnit = GetPriorityTarget(unit, 100);
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
            
            unit.Attack(targetUnit);
        }

        private void ActOnEnemy() {      
            if (Throttle(5)) return;
            
            var unit = Controller.GetUnitByTag(tag);
            if (unit == null) return;

            var closestEnemyUnit = unit.GetClosestUnit(Controller.enemyUnits.army);
            if (closestEnemyUnit == null) return;

            if (unit.GetDistance(closestEnemyUnit) > 10) {
                //if (unit.unitType == Units.HELLBAT)
                //    unit.ToHellion();
                return;
            }

            if (closestEnemyUnit.unitType == Units.REAPER) return;
            if (Units.Workers.Contains(closestEnemyUnit.unitType)) return;
            if (closestEnemyUnit.isFlying) return;

            if (unit.unitType == Units.HELLION)
                unit.ToHellbat();
        }

        
        
        public override void Act() {
            if (role == Roles.ATTACK) {
                ActOnEnemy();
                ManageAttack();
            }
            else if (role == Roles.HARASS) {
                ActOnEnemy();
                ManageAttack();
            }
            else if (role == Roles.RETREAT) {
                Retreat();
            }
            else {
                ActOnEnemy();
                ManageDefense();
            }
        }
    }
}