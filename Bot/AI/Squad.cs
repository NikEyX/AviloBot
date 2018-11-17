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
    public class Squad {
        protected static readonly Random random = new Random();     
        protected readonly ulong frameOffset;
        
        private string name = "UNDEFINED";
        private List<ulong> members = new List<ulong>();
        private Vector3 lastAveragePosition = Vector3.Zero;
        private List<Vector3> scoutLocations = new List<Vector3>();

        private Roles role = Roles.NONE;

        
        
        public Squad(string name) {
            this.name = name;
            this.frameOffset = (ulong) random.Next(23);
        }
        
        protected bool Throttle(int frames) {
            return (Controller.frame + frameOffset) % (ulong) frames != 0;
        }

        protected void Log(string line, params object[] parameters) {
            var formattedLine = String.Format("[SQUAD:{0}] ", name);
            Logger.Info(formattedLine + line, parameters);
        }

        public int GetMemberCount() {
            return members.Count;
        }


        public bool IsMember(Unit unit) {
            return members.Contains(unit.tag);
        }

        public void Retreat() {
            if (role == Roles.RETREAT) return;

            var units = GetUnits();
            foreach (var unit in units) {
                var ensign = Commander.GetEnsign(unit.tag);
                ensign.Retreat();
            }
            role = Roles.RETREAT;            
        }

        public void StandDown() {
            if (role == Roles.NONE) return;

            var units = GetUnits();
            foreach (var unit in units) {
                var ensign = Commander.GetEnsign(unit.tag);
                ensign.SetRole(Roles.NONE);
            }
            role = Roles.NONE;
        }

        public void Dismiss(bool retreat) {
            var memberCount = members.Count;
            if (retreat) 
                Retreat();
            else
                StandDown();            
            
            Log("Squad with {0} members dismissed", memberCount);
        }


        public void AddUnit(Unit unit) {
            if (!members.Contains(unit.tag))
                members.Add(unit.tag);
        }

        public List<Unit> GetUnits() {
            var units = new List<Unit>();
            foreach (var unitTag in members.ToArray()) {
                var unit = Controller.GetUnitByTag(unitTag);
                if (unit == null) {
                    members.Remove(unitTag);
                    continue;
                }
                units.Add(unit);
            }
            return units;
        }
        
            
        public void Attack(bool queue=false) {
            var units = GetUnits();
            if (units.Count == 0) return;

            var targetPosition = units[0].GetClosestPosition(Controller.enemyLocations.Keys.ToList());

            foreach (var unit in units) {
                var ensign = Commander.GetEnsign(unit.tag);
                ensign.SetRole(Roles.ATTACK);
            }
            this.role = Roles.ATTACK;

            Controller.Attack(units, targetPosition, queue);            
        }
       
        public void Harass() {
            var units = GetUnits();
            if (units.Count == 0) return;
            if (Controller.enemyLocations.Count == 0) return;
            var enemyLocation = Controller.enemyLocations.Keys.First();

            if (role != Roles.HARASS) {

                Vector3 movePosition = Vector3.Zero;
                var closestDistance = 999999d;
                for (int attempt=0; attempt < 100; attempt++) {
                    var attemptedPosition = new Vector3(random.Next((int) Controller.mapCenter.X * 2), random.Next((int) Controller.mapCenter.Y * 2), 0);

                    var ownDistance = Vector3.Distance(attemptedPosition, Controller.startLocation);
                    var enemyDistance = Vector3.Distance(attemptedPosition, enemyLocation);
                    var distance = -(ownDistance + enemyDistance);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        movePosition = attemptedPosition;
                    }
                }

                
                foreach (var unit in units) {
                    var ensign = Commander.GetEnsign(unit.tag);
                    ensign.SetRole(Roles.HARASS);
                }
                this.role = Roles.HARASS;
                
                Log("Moving to: {0}", movePosition);
                Controller.Move(units, movePosition, false);
                return;
            }

            //i.e. doing nothing
            if (units[0].order.ability == 0) {
                if (scoutLocations.Count == 0)
                    scoutLocations = Controller.expansionLocations.ToList();

                var closestExp = units[0].GetClosestPosition(scoutLocations);
                var closestExpDistance = units[0].GetDistance(closestExp);

                //var distanceFromHome = Vector3.Distance(Controller.startLocation, closestExp);

                if (closestExpDistance < 3)
                    scoutLocations.Remove(closestExp);
                else {
                    Controller.Move(units, closestExp, false);
                }                
            }

        }

        private Vector3 GetAveragePosition(List<Unit> units) {
            Vector3 averagePosition = Vector3.Zero;
            if (units.Count == 0) return averagePosition;

            foreach (var unit in units)
                averagePosition += unit.position;
            return averagePosition / units.Count;
        }

        public void Regroup() {
            if (Throttle(23)) return;            
            if (members.Count < 2) return;

            var units = GetUnits();

            List<Unit> groundUnits = new List<Unit>();
            List<Unit> airUnits = new List<Unit>();
            foreach (var unit in units) {
                if (unit.isFlying)
                    airUnits.Add(unit);
                else {
                    groundUnits.Add(unit);
                }
            }

            Vector3 averageGroundPosition = GetAveragePosition(groundUnits);
            Vector3 averageAirPosition = GetAveragePosition(airUnits);
                
            

            groundUnits.Sort((x, y) => Vector3.DistanceSquared(x.position, averageGroundPosition).CompareTo(Vector3.DistanceSquared(y.position, averageGroundPosition)));                                    

            //for median:
            List<Unit> medianUnits = new List<Unit>();
            foreach (var unit in groundUnits) {
                for (int i=0; i < unit.usedSupply; i++)
                    medianUnits.Add(unit);
            }             
            Vector3 regroupPosition = medianUnits[(int) medianUnits.Count / 2].position;

            var radiusSquared = Math.Pow(Math.Max(members.Count / 2f, 5), 2);

            bool regrouped = false;


            var radius = Math.Max(5, groundUnits.Count / 2);

            //regrouping of ground units
            float regroupPercentage = 0f;
            foreach (var unit in groundUnits) {
                var unitPosition = unit.position;
                unitPosition.Z = 0;

                if (unit.InRange(regroupPosition, radius))
                    regroupPercentage += 1;
            }
            
            regroupPercentage /= groundUnits.Count;

            var requiredPercentage = Math.Min(0.9f, (groundUnits.Count - 2f) / groundUnits.Count);

            if (regroupPercentage < requiredPercentage) {
                if (random.Next(100) < 75)
                    Controller.Move(groundUnits, regroupPosition, false);
                else
                    Controller.Attack(groundUnits, regroupPosition, false);
                Log("Regrouping {0} ground units ({1}% regrouped)", groundUnits.Count, String.Format("{0:0.0}", regroupPercentage * 100.0));

                //FIX THIS: IT WON'T WORK IF A UNIT IS TRAPPED!
                regrouped = true;
            }

            
            //regrouping of air units
            if (airUnits.Count > 0) {
                if (Vector3.DistanceSquared(averageAirPosition, regroupPosition) > 7 * 7) {
                    if (random.Next(100) < 50)
                        Controller.Move(airUnits, regroupPosition, false);
                    else
                        Controller.Attack(airUnits, regroupPosition, false);
                    Log("Regrouping {0} air units", airUnits.Count);                    
                    regrouped = true;
                }
            }            

            if (regrouped)
                Attack(true);
            else
                Attack(false);
        }


        public bool IsStuck() {    
            if (!Controller.Each(60)) return false;

            var units = GetUnits();
            var currentAvgPosition = GetAveragePosition(units);
            if (Vector3.Distance(currentAvgPosition, lastAveragePosition) < 10) {
                return true;
            }
            else {
                lastAveragePosition = currentAvgPosition;
                return false;
            }
        }

        public void Act() {
            if (role == Roles.ATTACK)
                Regroup();
            else if (role == Roles.HARASS) {
                if (Throttle(23 * 5)) {
                    Harass();
                }
            }
        }

        
    }
}


