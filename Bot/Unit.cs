using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Net.Mime;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using SC2APIProtocol;
using Action = SC2APIProtocol.Action;

namespace Bot {
    public class Unit {
        public class Order {
            public uint ability = 0;
            public float progress = 0;
            public Vector3 targetPosition = new Vector3();
            public ulong targetTag = 0;

            public Order() { }
            public Order(UnitOrder originalOrder) {
                ability = originalOrder.AbilityId;
                progress = originalOrder.Progress;
                targetTag = originalOrder.TargetUnitTag;
                if (originalOrder.TargetWorldSpacePos != null) {
                    targetPosition.X = originalOrder.TargetWorldSpacePos.X;
                    targetPosition.Y = originalOrder.TargetWorldSpacePos.Y;
                    targetPosition.Z = originalOrder.TargetWorldSpacePos.Z;
                }                
            }
        }

        private SC2APIProtocol.Unit original;
        
        public Vector3 position { get; private set; }
        public Order order { get; private set; }
        public int orderCount { get; private set; }
        public int assignedHarvesters { get; private set; } 
        public int idealHarvesters { get; private set; }
        public ulong tag  { get; private set; }
        public float buildProgress { get; private set; }
        public ulong addOnTag { get; private set; }
        public uint unitType { get; private set; }
        public int owner { get; private set; }  
        public float radius { get; private set; }
        public float detectRange { get; private set; }
        public float radarRange { get; private set; }
        public float energy { get; private set; }
        public float health { get; private set; }
        public float shield { get; private set; }
        public float weaponCooldown { get; private set; }
        public float groundAttackRange { get; private set; }
        public float airAttackRange { get; private set; }
        public CloakState cloak { get; private set; }
        public bool isFlying { get; private set; }
        public bool isBlip { get; private set; }

        public List<uint> buffs;
        public int vespeneContents { get; private set; }
        public int usedSupply { get; private set; }
        public List<PassengerUnit> passengers;
        public DisplayType displayType;
        
        public float integrity { get; private set; }

        public Unit(SC2APIProtocol.Unit original) {
            this.original = original;            
            this.position = new Vector3(original.Pos.X, original.Pos.Y, original.Pos.Z);
            
            this.tag = original.Tag;            
            this.order = (original.Orders.Count > 0) ? new Order(original.Orders[0]) : new Order();
            this.orderCount = original.Orders.Count;
            this.buildProgress = original.BuildProgress;
            this.unitType = original.UnitType;
            this.health = original.Health;
            this.shield = original.Shield;
            this.integrity = (original.Health + original.Shield) / (original.HealthMax + original.ShieldMax);
            this.assignedHarvesters = original.AssignedHarvesters;
            this.idealHarvesters = original.IdealHarvesters;
            this.addOnTag = original.AddOnTag;
            this.owner = original.Owner;
            this.energy = original.Energy;
            this.radius = original.Radius;
            this.weaponCooldown = original.WeaponCooldown;
            this.radarRange = original.RadarRange;
            this.detectRange = original.DetectRange;
            this.cloak = original.Cloak;
            this.isFlying = original.IsFlying;
            this.buffs = new List<uint>(original.BuffIds);
            this.vespeneContents = original.VespeneContents;
            this.passengers = original.Passengers.ToList();
            this.usedSupply = 1;            
            this.airAttackRange = 0;
            this.groundAttackRange = 1;
            this.displayType = original.DisplayType;
            this.isBlip = original.IsBlip;            

            if (unitType == Units.REAPER) {
                groundAttackRange = 5;
            }
            else if (unitType == Units.MARINE) {
                groundAttackRange = 5;
                airAttackRange = 5;
            }
            else if (unitType == Units.BANSHEE) {
                groundAttackRange = 6;
                usedSupply = 3;
            }
            else if (Units.SiegeTanks.Contains(unitType)) {
                groundAttackRange = 13;
                usedSupply = 3;
            }
            else if (Units.Vikings.Contains(unitType)) {
                airAttackRange = 9;
                usedSupply = 2;
            }
            else if (unitType == Units.ROACH) {
                groundAttackRange = 4;
            }
            else if (unitType == Units.QUEEN) {
                groundAttackRange = 5;
                airAttackRange = 8;
            }
            else if (unitType == Units.HYDRALISK) {
                groundAttackRange = 5;
                airAttackRange = 5;
            }
            else if (unitType == Units.STALKER) {
                groundAttackRange = 6;
                airAttackRange = 6;
            }
            else if (unitType == Units.SENTRY) {
                groundAttackRange = 5;
                airAttackRange = 5;
            }
            else if (unitType == Units.ADEPT) {
                groundAttackRange = 4;
            }
            else if (unitType == Units.SPORE_CRAWLER) {
                airAttackRange = 7;
            }
            else if (unitType == Units.MISSILE_TURRET) {
                airAttackRange = 7;
            }
            else if (unitType == Units.BUNKER) {
                airAttackRange = 6;
                groundAttackRange = 6;
            }
            else if (unitType == Units.PHOTON_CANNON) {
                airAttackRange = 7;
                groundAttackRange = 7;
            }
        }
                

        public double GetDistance(Unit targetUnit, bool ignoreZ=false) {
            if (ignoreZ) {
                var ownPosition = new Vector2(original.Pos.X, original.Pos.Y);
                var targetPosition = new Vector2(targetUnit.position.X, targetUnit.position.Y);                                
                return Vector2.Distance(ownPosition, targetPosition);
            }
            else
                return Vector3.Distance(position, targetUnit.position);                
        }

        public double GetDistance(Vector3 targetLocation, bool ignoreZ=false) {
            if (ignoreZ) {
                var ownPosition = new Vector2(original.Pos.X, original.Pos.Y);
                var targetPosition = new Vector2(targetLocation.X, targetLocation.Y);                                
                return Vector2.Distance(ownPosition, targetPosition);
            }
            else
                return Vector3.Distance(position, targetLocation);        
        }
        
        public bool InRange(Vector3 targetLocation, int radius) {            
            var squaredRadius = radius * radius;            

            var ownPosition = new Vector2(original.Pos.X, original.Pos.Y);
            var targetPosition = new Vector2(targetLocation.X, targetLocation.Y);
            
            return (Vector2.DistanceSquared(ownPosition, targetPosition) <= squaredRadius);
        }
        

        public bool InRange(List<Unit> units, int radius) {           
            var squaredRadius = radius * radius;
            foreach (var unit in units) {
                var distance = Vector3.DistanceSquared(this.position, unit.position);
                if (distance <= squaredRadius) 
                    return true;
            }
            return false;
        }

        
        public bool IsMechanical() { 
            return Units.Mechanical.Contains(unitType);
        }

        public bool IsUsable() {
            if (isFlying) return false;
            if (buildProgress < 1.0f) return false;
            
            if (HasReactor()) {
                if (orderCount >= 2) return false;
            }
            else 
                if (order.ability != 0) return false;            

            return (!Controller.HasActionAttached(this));
        }
        
        public void Train(uint unitType, bool queue=false) {            
            if ((!queue) && (order.ability != 0)) return;

            Logger.Info("Training: {0}", Units.GetName(unitType));
            
            var ability = Units.ToAbility[unitType];
            var action = Controller.CreateRawUnitCommand(ability);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            action.ActionRaw.UnitCommand.QueueCommand = queue;
            Controller.AddAction(action);
        }

        public bool IsCarryingResources() {
            if (!Units.Workers.Contains(unitType)) return false;
            foreach (var buff in buffs) {
                if (Buffs.CarryMinerals.Contains(buff)) return true;
                if (Buffs.CarryVespene.Contains(buff)) return true;
            }
            return false;
        }

        public void Research(int researchType) {
            if (!IsUsable()) return;
            
            Logger.Info("Researching: {0}", researchType);

            var action = Controller.CreateRawUnitCommand(researchType);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }
        
        

        public void Scan(Vector3 targetLocation) {
            if (unitType != Units.ORBITAL_COMMAND) return;            
            if (this.energy < 50) return;
            
            Logger.Info("Scanning: {0}", targetLocation);

            var action = Controller.CreateRawUnitCommand(Abilities.SCANNER_SWEEP);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            action.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.X = targetLocation.X;
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = targetLocation.Y;
            Controller.AddAction(action);
        }
        

        public void Cloak() {
            if (!Controller.IsResearched(Abilities.RESEARCH_BANSHEE_CLOAK)) return;
            
            //no point in cloaking with less
            if (this.energy < 30) return;
            
            Logger.Info("Cloaking: {0}", this);

            var action = Controller.CreateRawUnitCommand(Abilities.CLOAK);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }
        
        public void Salvage() {
            if (unitType != Units.BUNKER) return;
            Logger.Info("Salvaging: {0}", this);

            var action = Controller.CreateRawUnitCommand(Abilities.SALVAGE_BUNKER);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            action.ActionRaw.UnitCommand.QueueCommand = true;
            Controller.AddAction(action);
        }

        public void Unload() {
            if (unitType != Units.BUNKER) return;
            Logger.Info("Unloading: {0}", this);

            var action = Controller.CreateRawUnitCommand(Abilities.UNLOAD_BUNKER);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }
        
        public void ToHellbat() {
            if (unitType != Units.HELLION) return;
            if (order.ability == Abilities.TRANSFORM_TO_HELLBAT) return;
            if (Controller.CountStructures(Controller.ownUnits.armories, true) < 1) return;
            
            Logger.Info("Transforming to Hellbat: {0}", this);

            var action = Controller.CreateRawUnitCommand(Abilities.TRANSFORM_TO_HELLBAT);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }
        
        public void ToHellion() {
            if (unitType != Units.HELLBAT) return;
            if (order.ability == Abilities.TRANSFORM_TO_HELLION) return;
            if (Controller.CountStructures(Controller.ownUnits.armories, true) < 1) return;
            
            Logger.Info("Transforming to Hellion: {0}", this);

            var action = Controller.CreateRawUnitCommand(Abilities.TRANSFORM_TO_HELLION);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }

        public void ToThorAP() {
            if (unitType != Units.THOR) return;            
            Logger.Info("Switching mode to Armor Piercing: {0}", this);

            var action = Controller.CreateRawUnitCommand(Abilities.THOR_SWITCH_AP);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }

        
        public void ToThorNormal() {
            if (unitType != Units.THOR) return;            
            Logger.Info("Switching mode to Normal: {0}", this);

            var action = Controller.CreateRawUnitCommand(Abilities.THOR_SWITCH_NORMAL);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }

        public void ReaperGrenade(Vector3 target) {
            //Logger.Info("{0} throwing reaper grenade", this);

            var action = Controller.CreateRawUnitCommand(Abilities.REAPER_GRENADE);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            action.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.X = target.X;
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = target.Y;
            Controller.AddAction(action);
        }

        public void Lift() {
            if (isFlying) return;

            var action = Controller.CreateRawUnitCommand(Abilities.LIFT);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }
        
        
        public void Siege() {
            var action = Controller.CreateRawUnitCommand(Abilities.SIEGE_TANK);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }
        
        public void Unsiege() {
            var action = Controller.CreateRawUnitCommand(Abilities.UNSIEGE_TANK);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            Controller.AddAction(action);
        }

        
        public bool HasTechlab() {
            if (addOnTag == 0) return false;
            if (isFlying) return false;
            var addon = Controller.GetUnitByTag(addOnTag);
            if (addon.unitType == Units.BARRACKS_TECHLAB) return true;
            if (addon.unitType == Units.FACTORY_TECHLAB) return true;
            if (addon.unitType == Units.STARPORT_TECHLAB) return true;
            return false;
        }
        
        public bool HasReactor() {
            if (addOnTag == 0) return false;
            if (isFlying) return false;
            var addon = Controller.GetUnitByTag(addOnTag);
            if (addon.unitType == Units.BARRACKS_REACTOR) return true;
            if (addon.unitType == Units.FACTORY_REACTOR) return true;
            if (addon.unitType == Units.STARPORT_REACTOR) return true;
            return false;
        }
        

        public void Land(Vector3 position) {
            if (!isFlying) return;
            
            var worker = Controller.GetAvailableWorker();

            List<Vector3> targetLocations = new List<Vector3>();
            for (var attempt = 0; attempt < 30 * 30; attempt++) {
                Vector3 targetLocation = new Vector3();
                targetLocation.X = position.X + (attempt % 30) - 15;
                targetLocation.Y = (float) (position.Y + Math.Floor((double) attempt / 30) - 15);                
                targetLocations.Add(targetLocation);
            }
            
            targetLocations.Sort((x,y) => Vector3.DistanceSquared(position, x).CompareTo(Vector3.DistanceSquared(position, y)));

            foreach (var targetLocation in targetLocations) {
//                if (!Controller.CanPlace(worker.tag, Units.COMMAND_CENTER, targetLocation)) continue;
                
                if (!Controller.CanPlace(tag, Units.COMMAND_CENTER, targetLocation)) continue;

                var action = Controller.CreateRawUnitCommand(Abilities.LAND);
                action.ActionRaw.UnitCommand.UnitTags.Add(tag);

                action.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
                action.ActionRaw.UnitCommand.TargetWorldSpacePos.X = targetLocation.X;
                action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = targetLocation.Y;
                Controller.AddAction(action);
                break;
            }
            
        }

        public bool AtExpansion() {
            if (isFlying) return false;

            var distance = GetClosestDistance(Controller.expansionLocations);
            return (distance < 10);
        }


        private Unit FindMineralPatch() {            
            var mineralFields = Controller.GetUnits(Units.MineralFields, alliance:Alliance.Neutral);

            Unit anyResource = null; 
            foreach (var mf in mineralFields) {
                foreach (var rc in Controller.ownUnits.resourceCenters) {
                    if (rc.isFlying) continue;
                    if (mf.GetDistance(rc) < 10) return mf;
                    anyResource = mf;
                }
            }
            return anyResource;
        }

        public void CallDownMule() {
            if (energy < 50) return;
            
            var target = FindMineralPatch();            
            if (target == null) return;
            
            SC2APIProtocol.Action action;
            action = Controller.CreateRawUnitCommand(Abilities.CALL_DOWN_MULE);            
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            
            action.ActionRaw.UnitCommand.TargetUnitTag = target.tag;
            action.ActionRaw.UnitCommand.QueueCommand = false;
            
            Logger.Info("Calling down MULE @ {0}", this);
            Controller.AddAction(action);
        }
        
        public void GatherResources() {
            if (!Units.Workers.Contains(unitType))
                throw new Exception("Cannot gather resources with: {0}" + this);

            Unit target = null;

            
            //should we send it to gas?
            if (target == null) {
                foreach (var refinery in Controller.ownUnits.refineries) {
                    if (refinery.buildProgress < 1.0f) continue;
                    if (refinery.assignedHarvesters < refinery.idealHarvesters) {
                        target = refinery;
                    }
                }
            }

            //or minerals instead?
            if (target == null)
                target = FindMineralPatch();
                
            if (target == null)
                return;
            
            Logger.Info("Sending {0} to gather resources", this);
            Smart(target);
        }

        public void OperateDepot(bool raise) {
            if ((unitType != Units.SUPPLY_DEPOT) && (unitType != Units.SUPPLY_DEPOT_LOWERED))
                throw new Exception("Cannot operate depot with: {0}" + this);            

            if (raise) {
                if (unitType != Units.SUPPLY_DEPOT_LOWERED) return;                
            }
            else {
                if (unitType != Units.SUPPLY_DEPOT) return;
            }

            Action action = Controller.CreateRawUnitCommand(raise ? Abilities.DEPOT_RAISE : Abilities.DEPOT_LOWER);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            action.ActionRaw.UnitCommand.QueueCommand = false;
            Controller.AddAction(action);
        }

        
        
        public void Attack(Vector3 target) {
            List<Unit> units = new List<Unit>() { this };
            Controller.Attack(units, target);
        }
        
        public void Attack(Vector3 target, bool queue=false) {
            List<Unit> units = new List<Unit>() { this };
            Controller.Attack(units, target, queue);
        }
        
        public void Attack(Unit target) {
            List<Unit> units = new List<Unit>() { this };
            Controller.Attack(units, target);
        }

        public void Stop() {
            List<Unit> units = new List<Unit>() { this };
            Controller.Stop(units);
        }
        
        public void Smart(Unit targetUnit) {
            List<Unit> units = new List<Unit>() { this };
            Controller.Smart(units, targetUnit);
        }
        
        
        public void Move(Vector3 target, bool queue=false) {
            List<Unit> units = new List<Unit>() { this };
            Controller.Move(units, target, queue);
        }
        
        
        
        public void Repair(Unit target) {
            var action = Controller.CreateRawUnitCommand(Abilities.REPAIR);
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            action.ActionRaw.UnitCommand.TargetUnitTag = target.tag;
            Controller.AddAction(action);
        }
        
        
        public void Rally(Vector3 target) {
            var action = Controller.CreateRawUnitCommand(Abilities.RALLY);
            action.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.X = target.X;
            action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = target.Y;
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);                       
            Controller.AddAction(action);
        }

        
        public void ConstructAddOn(uint constructionType) {
            if (isFlying) return;
            if (addOnTag != 0) return;
            if (buildProgress < 1.0) return;

            if (!Controller.CanConstruct(constructionType))
                return;
            
            //DON'T DO IT IF ENEMIES ARE PRESENT
            foreach (var enemyUnit in Controller.enemyUnits.all) {
                if (GetDistance(enemyUnit) < 10) return;
            }
            
            

            SC2APIProtocol.Action action = new SC2APIProtocol.Action();
            action.ActionRaw = new ActionRaw();
            action.ActionRaw.UnitCommand = new ActionRawUnitCommand();
            action.ActionRaw.UnitCommand.UnitTags.Add(tag);
            
            Vector3 targetLocation = new Vector3();

            for (int attempt = 0; attempt < 500; attempt++) {
                var rotator = (attempt % 2) * 2 - 1;                
                targetLocation.X = position.X - (attempt % 10);
                targetLocation.Y = (float) (position.Y - Math.Floor((double) attempt / 10) * rotator);

                if (Controller.CanPlace(tag, Units.TECHLAB, targetLocation)) {                    
                    action.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
                    action.ActionRaw.UnitCommand.TargetWorldSpacePos.X = targetLocation.X;
                    action.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = targetLocation.Y;
                    break;
                }

            }

            if (constructionType == Units.TECHLAB) {                
                action.ActionRaw.UnitCommand.AbilityId = Abilities.BUILD_TECHLAB;
                Controller.AddAction(action);   
                Logger.Info("Constructing Techlab on: {0} @ {1}", this, targetLocation);
            } 
            else if (constructionType == Units.REACTOR) {
                action.ActionRaw.UnitCommand.AbilityId = Abilities.BUILD_REACTOR;
                Controller.AddAction(action);
                Logger.Info("Constructing Reactor on: {0} @ {1}", this, targetLocation);
            }
            else 
                throw new Exception("Ill-defined construction type: " + constructionType);            
        }

        
        public Vector3 GetClosestPosition(List<Vector3> positions) {
            var closestPosition = position;
            var closestDistance = 99999999d;

            foreach (var targetPosition in positions) {
                var distance = GetDistance(targetPosition);
                if (distance < closestDistance) {
                    closestPosition = targetPosition;
                    closestDistance = distance;
                }
            }
            return closestPosition;
        }

        public Vector3 GetClosestPosition(List<Unit> units) {
            var closestPosition = position;
            var closestDistance = 99999999d;

            foreach (var unit in units) {
                var targetPosition = unit.position;
                var distance = GetDistance(targetPosition);
                if (distance < closestDistance) {
                    closestPosition = targetPosition;
                    closestDistance = distance;
                }
            }
            return closestPosition;
        }

        public double GetClosestDistance(List<Vector3> positions) {
            var closestDistance = 99999999d;

            foreach (var targetPosition in positions) {
                var distance = GetDistance(targetPosition);
                if (distance < closestDistance) {
                    closestDistance = distance;
                }
            }
            return closestDistance;
        }


        public double GetClosestDistance(List<Unit> units) {
            var closestPosition = position;
            var closestDistance = 99999999d;

            foreach (var unit in units) {
                var distance = GetDistance(unit);
                if (distance < closestDistance) {
                    closestDistance = distance;
                }
            }
            return closestDistance;
        }

        public Unit GetClosestUnit(List<Unit> units) {
            Unit closestUnit = null;
            var closestDistance = 99999999d;

            foreach (var targetUnits in units) {
                var distance = GetDistance(targetUnits);
                if (distance < closestDistance) {
                    closestUnit = targetUnits;
                    closestDistance = distance;
                }
            }
            return closestUnit;
        }


        public override string ToString() {
            return String.Format("{0}<{1}>", Units.GetName(unitType), tag);
        }
    }
}