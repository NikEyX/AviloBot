using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.PerformanceData;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using SC2APIProtocol;

namespace Bot.AI {
    public class Viking : Template {
        public Viking(Unit unit) : base(unit) { }
        
        private Unit GetPriorityTarget(Unit unit, float maxDistance) {
            Unit closestEnemyUnit = null;
            double closestDistance = maxDistance;
                
            foreach (var enemyUnit in Controller.enemyUnits.all) {
                var distance = unit.GetDistance(enemyUnit);
                if (distance > maxDistance) continue;
                
                if (unit.integrity < 1) {
                    distance -= (5 + (1.0f - unit.integrity) * 5);
                }


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
                return;
            }

            var targetUnit = GetPriorityTarget(unit, 250);
            if (targetUnit == null) return;

            //not anywhere close to our buildings
            var closestDistance = targetUnit.GetClosestDistance(Controller.ownUnits.structures);
            if (closestDistance > 20) return;
            
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

        private void ActOnEnemy() {      
            if (Throttle(5)) return;
            
            //var unit = Controller.GetUnitByTag(tag);
            //if (unit == null) return;

            //var closestEnemyUnit = unit.GetClosestUnit(Controller.enemyUnits.army);
            //if (closestEnemyUnit == null) return;
            //if (closestEnemyUnit.unitType == Units.REAPER) return;

        }

        
        
        public override void Act() {
            if (role == Roles.ATTACK) {
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