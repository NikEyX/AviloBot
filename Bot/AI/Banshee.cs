using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlTypes;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using SC2APIProtocol;

namespace Bot.AI {
    public class Banshee : Template {
        public Banshee(Unit unit) : base(unit) {             
            //every new Banshee is tasked with harassing first
            role = Roles.HARASS;
        }
        
        private static Vector3 lastTargetRC = Vector3.Zero;
        private float lastFrameIntegrity = 1.0f;
        private ulong lastTargetFrame = 0;
        
                
        List<Vector3> circlePath = new List<Vector3>();
        
        private double DegreeToRadian(double angle) {
            return Math.PI * angle / 180.0;
        }




        private void Harass() {
            var unit = GetUnit();
            if (unit == null) return;         
            
            //retreat if low health
            var minHealth = 50;
            if (Controller.After(10 * 60))
                minHealth = 125;

            if (unit.health <= minHealth) {                
                Log("Retreating");
                Retreat();
                return;
            }

            if (circlePath.Count == 0) {
                var closestRC = unit.GetClosestUnit(Controller.enemyUnits.resourceCenters);
                if ((closestRC == null) || (unit.GetDistance(closestRC) > 75)) {
                    //move towards enemy base as long as we didn't find a CC yet
                    if ((Controller.Each(1f)) && (unit.integrity >= 1.0)) {
                        var targetPosition = lastTargetRC;
                        foreach (var kv in Controller.enemyLocations) {
                            if (Units.ResourceCenters.Contains(kv.Value)) {
                                if (kv.Key != lastTargetRC) {
                                    lastTargetRC = kv.Key;
                                    targetPosition = kv.Key;
                                    break;
                                }
                            }
                        }
                        
                        //var offset = new Vector3(random.Next(-50, 50), random.Next(-50, 50), 0);
                        var offset = Vector3.Zero;
                        circlePath.Add(targetPosition + offset);
                        Log("Moving towards enemy location: {0}", targetPosition);
                    }
                    return;
                }
                else {
                    Log("Determining circle path around RC");
                    var radius = random.Next(9, 12);
                    for (var i = 0; i < 18; i++) {
                        var ang = i * 20;
                        var pathPos = new Vector3();
                        pathPos.X = (float) (closestRC.position.X + radius * Math.Sin(DegreeToRadian(ang)));
                        pathPos.Y = (float) (closestRC.position.Y + radius * Math.Cos(DegreeToRadian(ang)));
                        pathPos.Z = closestRC.position.Z;
                        circlePath.Add(pathPos);
                    }
                }                
            }

            if (unit.cloak == CloakState.CloakedDetected) {
                Log("Uncloaked through detection. Should retreat...");
            }

            //find ideal target
            Unit targetUnit = null;
            var closestDistance = 50d;
            foreach (var enemyUnit in Controller.enemyUnits.workersAndArmy) {
                var distance = unit.GetDistance(enemyUnit);
                    
                //using distance as proxy for attractiveness

                //prefer hurt units
                if (enemyUnit.integrity < 1.0f) {
                    if (enemyUnit.health + enemyUnit.shield <= 60) distance -= 5;
                    if (enemyUnit.health + enemyUnit.shield <= 40) distance -= 5;
                    if (enemyUnit.health + enemyUnit.shield <= 20) distance -= 5;
                }

                //prefer workers
                if ((Units.Workers.Contains(enemyUnit.unitType)) || (enemyUnit.unitType == Units.MULE))
                    distance -= 10;
                
                
                //prefer anti air units over ground units
                if (enemyUnit.airAttackRange > 0)
                    distance -= 5;

                //prefer energy units
                if (enemyUnit.energy > 0)
                    distance -= 5;
                
                
                if (distance < closestDistance) {
                    closestDistance = distance;
                    targetUnit = enemyUnit;
                    lastTargetFrame = Controller.frame;
                }
            }
            
            
            //avoid anti air structures
            foreach (var enemyUnit in Controller.enemyUnits.staticAirDefense) {
                if (unit.buildProgress < 0.9) continue;
                if (unit.GetDistance(enemyUnit) > enemyUnit.airAttackRange + 3) continue;

                var offset = Vector3.Normalize(unit.position - enemyUnit.position);
                unit.Move(unit.position + offset * 10);
                Logger.Info("[{0}] Avoiding defensive structure: {1}", unit, enemyUnit);
                return;
            }
            
            //avoid raven
            foreach (var enemyUnit in Controller.GetUnits(Units.RAVEN, Alliance.Enemy)) {
                if (unit.GetDistance(enemyUnit) > enemyUnit.detectRange + 3) continue;

                var offset = Vector3.Normalize(unit.position - enemyUnit.position);
                unit.Move(unit.position + offset * 10);
                Logger.Info("[{0}] Avoiding Raven: {1}", unit, enemyUnit);
                return;
            }
            
            //we haven't targeted anything in a while... attack a building or whatever
            if (Controller.frame - lastTargetFrame > 23 * 20) {
                if (targetUnit == null) {                    
                    foreach (var enemyUnit in Controller.enemyUnits.structures) {
                        if (Units.StaticAirDefense.Contains(enemyUnit.unitType)) continue;

                        targetUnit = enemyUnit;

                        if (enemyUnit.integrity < 1)
                            break;
                    }
                }
            }

            if ((unit.order.targetTag != 0) && (unit.weaponCooldown <= 0)) {
                return;
            }

            //on cooldown, let's circle around
            if ((targetUnit == null) || (unit.weaponCooldown > 0)) {
                Vector3 closestPathPoint = unit.GetClosestPosition(circlePath);

                unit.Move(closestPathPoint);
                
                if (unit.GetDistance(closestPathPoint, ignoreZ: true) < 5) {    
                    if (circlePath.Contains(closestPathPoint))
                        circlePath.Remove(closestPathPoint);
                }
            }
            else {
                if (unit.order.targetTag != targetUnit.tag)
                    unit.Attack(targetUnit);               
            }
        }
        

        private void ManageDefense() {
            if (Throttle(5)) return;
            
            var unit = Controller.GetUnitByTag(tag);
            if (unit == null) return;

            //are we already attacking something? nevermind then
            if (unit.order.ability == Abilities.ATTACK) return;

            var targetUnit = GetPriorityTarget(unit, 100);
            if (targetUnit == null) return;

            //not anywhere close to our buildings
            if (targetUnit.GetClosestDistance(Controller.ownUnits.structures) > 20) return;
            
            //early game
            if (!Controller.IsLateGame()) {
                //let's not walk down the ramp...mhhkay?
                if (targetUnit.position.Z < unit.position.Z - 0.25f)
                    return;
            }
            unit.Attack(targetUnit);
        }

        private void DefensiveCloak() {     
            var unit = GetUnit();
            if (unit == null) return;
                               
            //should the unit cloak? only if under attack
            if ((unit.integrity < lastFrameIntegrity) && (unit.cloak == CloakState.NotCloaked) && (Controller.IsResearched(Abilities.RESEARCH_BANSHEE_CLOAK)))
                unit.Cloak();
            lastFrameIntegrity = unit.integrity;
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
                    
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestEnemyUnit = enemyUnit;
                }
            }
            
            return closestEnemyUnit;
        }
        


        public override void Act() {
            DefensiveCloak();
            
            if (Throttle(3)) return;
            if (role == Roles.HARASS)
                Harass();
            else if (role == Roles.RETREAT)
                Retreat();
            else
                ManageDefense();


        }
        
        
    }
}