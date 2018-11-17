using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Bot.AI {
    public class Template {
        public ulong tag;

        protected Roles role = Roles.NONE;
        protected static readonly Random random = new Random();        
        protected readonly ulong frameOffset;


        protected Template(Unit unit) {
            this.tag = unit.tag;
            this.frameOffset = (ulong) random.Next(23);
        }
        
        protected void Log(string line, params object[] parameters) {
            var formattedLine = String.Format("[{0}] ", GetUnit());
            Logger.Info(formattedLine + line, parameters);
        }

        protected bool Throttle(int frames) {
            return (Controller.frame + frameOffset) % (ulong) frames != 0;
        }

        public void SetRole(Roles role) {
            this.role = role;
        }
        
        public bool IsAvailable() {
            return (role == Roles.NONE);
        }
        
        
        //don't forget to call Throttle before calling this method
        public void Retreat() {
            var unit = Controller.GetUnitByTag(tag);
            if (unit == null) return;
            
            role = Roles.RETREAT;

            //avoid enemy unit clusters on retreat
            Vector3 enemyCluster = Vector3.Zero;
            int enemyCounter = 0;

            if (unit.isFlying) {            
                var enemyAntiAir = Controller.enemyUnits.army.Union(Controller.enemyUnits.staticAirDefense);                                
                foreach (var enemyUnit in enemyAntiAir) {
                    if (enemyUnit.buildProgress < 0.95f) continue;
                    if (enemyUnit.airAttackRange <= 0) continue;
                    if (unit.GetDistance(enemyUnit) > enemyUnit.airAttackRange + 5) continue;

                    enemyCluster += enemyUnit.position;
                    enemyCounter += 1;
                }
                enemyCluster /= enemyCounter;
            }
            else {
                var enemyAntiGround = Controller.enemyUnits.army.Union(Controller.enemyUnits.staticGroundDefense);
                foreach (var enemyUnit in enemyAntiGround) {
                    if (enemyUnit.buildProgress < 0.95f) continue;
                    if (enemyUnit.groundAttackRange == 0) continue;
                    if (unit.GetDistance(enemyUnit) > enemyUnit.groundAttackRange + 5) continue;

                    enemyCluster += enemyUnit.position;
                    enemyCounter += 1;
                }
                enemyCluster /= enemyCounter;
            }                        

            //no enemy unit nearby! move straight back
            if (enemyCounter == 0) {      
                var targetLocation = Controller.startLocation + Vector3.One * 3;
                targetLocation.Z = 0;
                
                var distance = unit.GetDistance(targetLocation, true);
                if (distance > 10) {
                    if (targetLocation != unit.order.targetPosition) {
                        Log("Escaped: Returning to home base");
                        unit.Move(targetLocation); 
                    }
                }
                else {      
                    Log("Escaped: Arrived at safe location");
                    role = Roles.NONE;
                }
                return;
            }

            var offset = -(enemyCluster - unit.position);
            unit.Move(unit.position + Vector3.Normalize(offset) * 15);        
        }
        
        public Unit GetUnit() {
            return Controller.GetUnitByTag(tag);
        }



        public virtual void Act() {
            Logger.Info("This is a stub and should be overwritten by the implementing method.");    
        }                

        
    }
}