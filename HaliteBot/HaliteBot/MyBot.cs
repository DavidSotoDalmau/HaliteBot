using System;
using System.Collections.Generic;
using System.Linq;
using Halite2.hlt;

namespace Halite2
{
    public class MyBot
    {

        public static void Main(string[] args)
        {
            string name = args.Length > 0 ? args[0] : "Sharpie";

            Networking networking = new Networking();
            GameMap gameMap = networking.Initialize(name);
            bool bUndocked = false;
            List<int> targets = new List<int>();
            List<Move> moveList = new List<Move>();
            bool finalattack = false;
            bool firstTime = true;
            int turno = 1;
            int nomoves = 0;
            int iUndocked = 0;
            Planet target = null;
            for (; ; )
            {
                bUndocked = false;
                moveList.Clear();
                gameMap.UpdateMap(Networking.ReadLineIntoMetadata());
                targets.Clear();
                if ((AllShipsDocked(gameMap) && AllPlanetsOwned(gameMap)) || finalattack || nomoves > 20)
                {
                    #region FinalAttack




                    targets.Insert(0, 0);
                    foreach (Ship ship in gameMap.GetMyPlayer().GetShips().Values)
                    {
                        if (ship.GetDockingStatus() != Ship.DockingStatus.Undocked && (GetOwnedPlanets(gameMap) - iUndocked > GetMaxUnOwnPlanets(gameMap) || GetOwnedPlanets(gameMap) < GetMaxUnOwnPlanets(gameMap)))
                        {
                            iUndocked++;
                            moveList.Add(new UndockMove(ship));
                            finalattack = true;
                            continue;
                        }

                        var nearestEnemy = GetNearestEnemyDockedShip(ship, gameMap);

                        if (inBounds(nearestEnemy, gameMap) && nearestEnemy != null)
                        {

                            ThrustMove AttackNearer = Navigation.NavigateShipToDock(gameMap, ship, nearestEnemy,
                                Constants.MAX_SPEED);
                            if (AttackNearer != null)
                            {
                                targets.Add(nearestEnemy.GetId());
                                moveList.Add(AttackNearer);
                                continue;
                            }
                        }
                        var nearestPlanet = GetNearestPlanet(ship, gameMap, targets);
                        DockProcess(nearestPlanet, targets, ship, moveList, gameMap);

                    }
                    #endregion
                }
                else
                {

                    foreach (Ship ship in gameMap.GetMyPlayer().GetShips().Values)
                    {
                        #region UnDockIfNoResources
                        if (ship.GetDockingStatus() != Ship.DockingStatus.Undocked)
                        {
                            if (gameMap.GetPlanet(ship.GetDockedPlanet()).RemainingProduction > 0 ||
                                (gameMap.GetPlanet(ship.GetDockedPlanet()).RemainingProduction == 0 &&
                                 gameMap.GetPlanet(ship.GetDockedPlanet()).GetDockedShips().Count <= 1))
                            {
                                continue;
                            }

                            if (!bUndocked)
                            {
                                bUndocked = true;
                                moveList.Add(new UndockMove(ship));
                                continue;

                            }
                        }
                        #endregion

                        var nearestPlanet = GetNearestPlanet(ship, gameMap, targets);
                        if (nearestPlanet == null)
                        {
                            #region NoFreePlannetsNormalAttack




                            nearestPlanet = AttackNearestPlanet(ship, gameMap);
                            try
                            {
                                target = (Planet)nearestPlanet;
                            }
                            catch (Exception)
                            {
                                //Ignore
                            }
                            var nearestEnemy = GetNearestEnemyShip(ship, gameMap);
                            if (nearestEnemy.GetDistanceTo(ship) <= nearestPlanet.GetDistanceTo(ship))
                            {
                                if (inBounds(nearestEnemy, gameMap))
                                {
                                    ThrustMove newThrustMove = Navigation.NavigateShipToDock(gameMap, ship, nearestEnemy, Constants.MAX_SPEED);
                                    if (newThrustMove != null)
                                    {
                                        moveList.Add(newThrustMove);
                                        continue;
                                    }
                                }
                            }
                            nearestEnemy = GetNearestEnemyDockedShip(nearestPlanet, gameMap);
                            if (inBounds(nearestEnemy, gameMap) && nearestEnemy != null)
                            {
                                ThrustMove AttackNearer = Navigation.NavigateShipToDock(gameMap, ship, nearestEnemy,
                                    Constants.MAX_SPEED);
                                if (AttackNearer != null)
                                {
                                    moveList.Add(AttackNearer);
                                    continue;
                                }
                            }

                            if (inBounds(nearestPlanet, gameMap))
                            {
                                ThrustMove AttackNearer = Navigation.NavigateShipTowardsTarget(gameMap, ship,
                                    nearestPlanet,
                                    Constants.MAX_SPEED, true, 60, 0);

                                if (AttackNearer != null)
                                {
                                    moveList.Add(AttackNearer);
                                    continue;
                                }
                            }
                            #endregion
                        }
                        else
                        {
                            #region DockInPlanetIfNoOwner
                            DockProcess(nearestPlanet, targets, ship, moveList, gameMap);
                            #endregion
                        }


                    }

                }

                if (moveList.Count == 0)
                {
                    nomoves++;
                }
                else
                {
                    nomoves = 0;
                }
                turno++;
                Networking.SendMoves(moveList);
            }
        }

        private static int GetMaxUnOwnPlanets(GameMap gameMap)
        {
            Dictionary<int, int> planestXPlayer = new Dictionary<int, int>();
            foreach (var player in gameMap.GetAllPlayers())
            {
                if (player.GetId() != gameMap.GetMyPlayerId())
                {
                    planestXPlayer.Add(player.GetId(), 0);
                }
            }
            foreach (var nearerEntity in gameMap.GetAllPlanets())
            {
                if (nearerEntity.Value.IsOwned() && nearerEntity.Value.GetOwner() != gameMap.GetMyPlayerId())
                {
                    planestXPlayer[nearerEntity.Value.GetOwner()]++;
                }
            }
            return planestXPlayer.Values.Max();
        }

        private static int GetOwnedPlanets(GameMap gameMap)
        {
            int owned = 0;
            foreach (var nearerEntity in gameMap.GetAllPlanets())
            {
                if (nearerEntity.Value.IsOwned() && nearerEntity.Value.GetOwner() == gameMap.GetMyPlayerId())
                {
                    owned++;
                }
            }
            return owned;
        }

        private static bool DockProcess(Entity nearestPlanet, List<int> targets, Ship ship, List<Move> moveList,
            GameMap gameMap)
        {
            Planet target = null;
            try
            {
                target = (Planet)nearestPlanet;
            }
            catch (Exception e)
            {
            }

            if (target != null)
            {
                targets.Insert(0, target.GetId());


                if (ship.CanDock((Planet)nearestPlanet) &&
                    (((Planet)nearestPlanet).RemainingProduction > 0 ||
                     !((Planet)nearestPlanet).IsOwned()))
                {
                    moveList.Add(new DockMove(ship, (Planet)nearestPlanet));
                    return true;
                }

                if (inBounds(nearestPlanet, gameMap))
                {
                    ThrustMove newThrustMove2 = Navigation.NavigateShipToDock(gameMap, ship,
                        (Planet)nearestPlanet,
                        Constants.MAX_SPEED);
                    if (newThrustMove2 != null)
                    {
                        moveList.Add(newThrustMove2);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool inBounds(Entity Destination, GameMap gameMap)
        {
            return Destination.GetXPos() < gameMap.GetHeight() - 5 && Destination.GetYPos() < gameMap.GetWidth() - 5 && Destination.GetYPos() > 2 && Destination.GetXPos() > 2;
        }

        public static Entity GetNearestEnemyShip(Entity me, GameMap currentMap)
        {
            Entity Nearest = null;
            double distance = 9999.9;
            foreach (var nearerEntity in currentMap.NearbyEntitiesByDistance(me))
            {
                try
                {
                    if (((Ship)nearerEntity.Value).GetOwner() != currentMap.GetMyPlayerId())
                    {
                        if (nearerEntity.Key < distance)
                        {
                            distance = nearerEntity.Key;
                            Nearest = nearerEntity.Value;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            return Nearest;
        }
        public static bool AllShipsDocked(GameMap currentMap)
        {
            foreach (var nearerEntity in currentMap.GetAllShips())
            {
                if (nearerEntity.GetDockingStatus() != Ship.DockingStatus.Docked && nearerEntity.GetOwner() != currentMap.GetMyPlayerId())
                {
                    return false;
                }
            }
            return true;
        }
        public static bool AllPlanetsOwned(GameMap currentMap)
        {
            foreach (var nearerEntity in currentMap.GetAllPlanets())
            {
                if (!nearerEntity.Value.IsOwned())
                {
                    return false;
                }
            }
            return true;
        }
        public static bool OnePlanetsLast(GameMap currentMap)
        {
            int count = 0;
            foreach (var nearerEntity in currentMap.GetAllPlanets())
            {
                if (!nearerEntity.Value.IsOwned() && nearerEntity.Value.GetXPos() > currentMap.GetWidth() / 2 - 8 && nearerEntity.Value.GetXPos() < currentMap.GetWidth() / 2 + 8 && nearerEntity.Value.GetYPos() > currentMap.GetHeight() / 2 - 8 && nearerEntity.Value.GetYPos() < currentMap.GetHeight() / 2 + 8)
                {
                    count++;
                }
            }

            if (count == 1) return true;
            return false;
        }
        public static Entity GetNearestEnemyDockedShip(Entity me, GameMap currentMap)
        {
            Entity Nearest = null;
            double distance = 9999.9;
            foreach (var nearerEntity in currentMap.NearbyEntitiesByDistance(me))
            {
                try
                {
                    if (((Ship)nearerEntity.Value).GetOwner() != currentMap.GetMyPlayerId() && ((Ship)nearerEntity.Value).GetDockingStatus() != Ship.DockingStatus.Undocked)
                    {
                        if (nearerEntity.Key < distance)
                        {
                            distance = nearerEntity.Key;
                            Nearest = nearerEntity.Value;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            return Nearest;
        }
        public static Entity GetNearestEnemyDockedShipUnitarget(Entity me, GameMap currentMap, List<int> targets)
        {
            Entity Nearest = null;
            double distance = 9999.9;
            foreach (var nearerEntity in currentMap.NearbyEntitiesByDistance(me))
            {
                try
                {
                    if (((Ship)nearerEntity.Value).GetOwner() != currentMap.GetMyPlayerId() && ((Ship)nearerEntity.Value).GetDockingStatus() != Ship.DockingStatus.Undocked && !targets.Contains(((Ship)nearerEntity.Value).GetId()))
                    {
                        if (nearerEntity.Key < distance)
                        {
                            distance = nearerEntity.Key;
                            Nearest = nearerEntity.Value;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            return Nearest;
        }
        public static Entity GetNearestPlanet(Entity me, GameMap currentMap, List<int> targets)
        {
            Entity Nearest = null;
            double distance = 9999.9;
            foreach (var nearerEntity in currentMap.NearbyEntitiesByDistance(me))
            {
                try
                {
                    if ((!((Planet)nearerEntity.Value).IsOwned()) || (((Planet)nearerEntity.Value).GetOwner() == currentMap.GetMyPlayerId() && !((Planet)nearerEntity.Value).IsFull() && ((Planet)nearerEntity.Value).RemainingProduction > 0) && !targets.Contains(((Planet)nearerEntity.Value).GetId()))
                    {
                        if (nearerEntity.Key < distance)
                        {
                            distance = nearerEntity.Key;
                            Nearest = nearerEntity.Value;
                        }
                    }
                }
                catch (Exception)
                {
                }

            }

            return Nearest;
        }
        public static Entity AttackNearestPlanet(Entity me, GameMap currentMap)
        {
            Entity Nearest = null;
            double distance = 9999.9;
            foreach (var nearerEntity in currentMap.NearbyEntitiesByDistance(me))
            {
                try
                {
                    if (((Planet)nearerEntity.Value).GetOwner() != currentMap.GetMyPlayerId() && ((Planet)nearerEntity.Value).GetId() != 0)
                    {
                        if (nearerEntity.Key < distance)
                        {
                            distance = nearerEntity.Key;
                            Nearest = nearerEntity.Value;
                        }
                    }
                }
                catch (Exception)
                {
                }

            }

            return Nearest;
        }
    }
}
