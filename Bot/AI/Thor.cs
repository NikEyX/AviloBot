using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.PerformanceData;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using SC2APIProtocol;

namespace Bot.AI {
    public class Thor : Template {
        public Thor(Unit unit) : base(unit) { 
            if (Controller.enemyRace != Race.Zerg) {
                unit.ToThorAP();
            }
        }
        
        private Unit GetPriorityTarget(Unit unit, float maxDistance) {
            Unit closestEnemyUnit = null;
            double closestDistance = maxDistance;
                
            foreach (var enemyUnit in Controller.enemyUnits.all) {
                var distance = unit.GetDistance(enemyUnit);
                if (distance > maxDistance) continue;

                if (enemyUnit.isFlying) {
                    distance -= 5;
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

            var targetUnit = GetPriorityTarget(unit, 100);
            if (targetUnit == null) return;
            
            //not anywhere close to our buildings
            if (targetUnit.GetClosestDistance(Controller.ownUnits.structures) > 20) return;

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