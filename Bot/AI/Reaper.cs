using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Numerics;
using SC2APIProtocol;

namespace Bot.AI {
    public class Reaper : Template {
        public Reaper(Unit unit) : base(unit) { 
            role = Roles.HARASS;
        }

        private static bool superiorUnits = false; 
                
        List<Vector3> circlePath = new List<Vector3>();
        List<Vector3> scoutLocations = new List<Vector3>();
        
        private double DegreeToRadian(double angle) {
            return Math.PI * angle / 180.0;
        }
        

        private void Scout() {
            var unit = GetUnit();
            if (unit == null) return;

            if (scoutLocations.Count == 0)
                scoutLocations = Controller.GetFreeExpansionLocations();

            //low on health? retreat
            if (unit.integrity < 1) {
                Retreat();
                return;
            }
            
            Vector3 closestPathPoint = unit.GetClosestPosition(scoutLocations);
            if (unit.GetDistance(closestPathPoint, ignoreZ: true) < 7) {    
                if (scoutLocations.Contains(closestPathPoint)) {
                    scoutLocations.Remove(closestPathPoint);
                    return;
                }
            }

            if ((unit.order.ability != Abilities.MOVE) && (unit.order.ability != Abilities.ATTACK)) {
                //move towards the middle first
                var offset = (Controller.mapCenter - unit.position);
                unit.Move(unit.position + Vector3.Normalize(offset) * 30);

                //move towards next expansion
            
                unit.Attack(closestPathPoint, true);
                        
                Log("Scouting: {0}", closestPathPoint);
            }
        }
        

        private void Harass() {
            var unit = GetUnit();
            if (unit == null) return;            

            if (unit.health <= 20) {
                Retreat();
                return;
            }


            if (circlePath.Count == 0) {
                var closestRC = unit.GetClosestUnit(Controller.enemyUnits.resourceCenters);
                if ((closestRC == null) || (unit.GetDistance(closestRC) > 75)) {
                    //move towards enemy base as long as we didn't find a CC yet
                    if ((Controller.Each(1f)) && (unit.integrity >= 1.0)) {
                        var targetPosition = Vector3.Zero;
                        foreach (var kv in Controller.enemyLocations) {
                            if (Units.ResourceCenters.Contains(kv.Value)) {
                                targetPosition = kv.Key;
                                break;
                            }
                        }
                        
                        circlePath.Add(targetPosition);
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

            //find ideal target
            Unit targetUnit = null;
            var closestDistance = 50d;
            foreach (var enemyUnit in Controller.enemyUnits.workersAndArmy) {
                var distance = unit.GetDistance(enemyUnit);
                if (distance < 15) {
                    //needs to be done better, e.g. with unit value
                    if ((enemyUnit.unitType == Units.ROACH) || (enemyUnit.unitType == Units.ADEPT) || (enemyUnit.unitType == Units.STALKER)) {
                        Log("Superior units - Retreating");
                        superiorUnits = true;
                        Retreat();
                        return;
                    }
                }
                    
                //prefer hurt units
                if (enemyUnit.integrity < 1.0f) {
                    if (enemyUnit.health + enemyUnit.shield <= 60) distance -= 5;
                    if (enemyUnit.health + enemyUnit.shield <= 40) distance -= 5;
                    if (enemyUnit.health + enemyUnit.shield <= 20) distance -= 5;
                }

                //prefer workers
                if ((Units.Workers.Contains(enemyUnit.unitType)) || (enemyUnit.unitType == Units.MULE))
                    distance -= 10;

                if (distance < closestDistance) {
                    closestDistance = distance;
                    targetUnit = enemyUnit;
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

        private void DefensiveReaperGrenade() {
            var unit = GetUnit();
            if (unit == null) return;           

            //reaper grenade if threatened
            if ((unit.integrity < 1f) && (unit.GetClosestDistance(Controller.enemyUnits.workersAndArmy) < 3)) {
                unit.ReaperGrenade(unit.position);
            }
        }
        
        
        public override void Act() { 
            if (Throttle(5)) return;

            DefensiveReaperGrenade();

            //don't harass anymore with reaper, just use it for scouting after x minutes
            if ((!superiorUnits) && (Controller.After(4 * 60)))
                superiorUnits = true;
            

            if (role == Roles.HARASS)
                Harass();
            else if (role == Roles.SCOUT) {
                Scout();
            }
            else if (role == Roles.RETREAT) {
                var unit = GetUnit();
                if (unit == null) return;           
                
                //attack again if health is OK
                if (unit.integrity >= 0.8) {
                    if (superiorUnits) {
                        role = Roles.SCOUT;
                        Scout();
                    }
                    else {
                        role = Roles.HARASS;
                        Harass();
                    }
                }
                else
                    Retreat();
            }
            
        }
        
        
    }
}