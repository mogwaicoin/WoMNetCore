﻿using System.Collections.Generic;
using System.Linq;
using GoRogue;
using WoMFramework.Game.Generator.Dungeon;
using WoMFramework.Game.Interaction;
using WoMFramework.Game.Model;
using WoMFramework.Game.Model.Mogwai;
using WoMFramework.Game.Model.Monster;

namespace WoMFramework.Game.Generator
{
    public enum AdventureState
    {
        Preparation, Running, Failed, Extended, Completed,
        Won,
        Lost
    }

    public enum AdventureStats
    {
        Explore, Monster, Boss, Treasure, Portal
    }

    public abstract class Adventure
    {
        public abstract Map Map { get; set; }

        public Dictionary<int, Entity> Entities { get; } = new Dictionary<int, Entity>();

        public List<Entity> EntitiesList => Entities.Values.ToList();

        public List<Monster> MonstersList => Entities.Values.OfType<Monster>().ToList();

        public List<Mogwai> HeroesList => Entities.Values.OfType<Mogwai>().ToList();

        public AdventureState AdventureState { get; set; }

        public Queue<AdventureLog> AdventureLogs { get; set; } = new Queue<AdventureLog>();

        public Queue<LogEntry> LogEntries { get; set; } = new Queue<LogEntry>();

        public Dictionary<AdventureStats, double> AdventureStats { get; }

        public bool IsActive => AdventureState == AdventureState.Preparation
                             || AdventureState == AdventureState.Extended;

        public Reward Reward { get; set; }

        private int _nextId;
        public int NextId => _nextId++;

        public abstract int GetRound { get; }

        protected Adventure()
        {
            AdventureState = AdventureState.Preparation;
            AdventureStats = new Dictionary<AdventureStats, double>
            {
                [Generator.AdventureStats.Explore] = 0,
                [Generator.AdventureStats.Monster] = 0,
                [Generator.AdventureStats.Boss] = 0,
                [Generator.AdventureStats.Treasure] = 0,
                [Generator.AdventureStats.Portal] = 0
            };
        }

        public abstract void EvaluateAdventureState();

        public abstract void CreateEntities(Mogwai mogwai, Shift shift);

        public abstract void Enter(Mogwai mogwai, Shift shift);

        public abstract void Prepare(Mogwai mogwai, Shift shift);

        public abstract bool HasNextFrame();

        public abstract void NextFrame();

        public void Enqueue(AdventureLog entityCreated)
        {
            LogEntries.Enqueue(new LogEntry(LogType.AdventureLog, entityCreated.AdventureLogId.ToString()));
            AdventureLogs.Enqueue(entityCreated);
        }
    }

    public class AdventureLog
    {
        public enum LogType
        {
            Info,
            Move,
            Attack,
            Died,
            Entity,
            Looted
        }

        public static int _index = 0;

        public int AdventureLogId { get; }
        public LogType Type { get; }
        public Coord SourceCoord { get; }
        public HashSet<Coord> SourceFovCoords { get; }
        public Coord TargetCoord { get; }
        public int Source { get; }
        public int Target { get; }
        public bool Flag { get; }

        public AdventureLog(LogType type, int source, Coord sourceCoord, HashSet<Coord> sourceFovCoords = null, int target = 0, Coord targetCoord = null, bool flag = true)
        {
            AdventureLogId = _index++;
            Type = type;
            Source = source;
            Target = target;
            SourceCoord = sourceCoord;
            SourceFovCoords = sourceFovCoords;
            TargetCoord = targetCoord;
            Flag = flag;
        }

        public static AdventureLog EntityCreated(AdventureEntity entity)
        {
            return new AdventureLog(LogType.Entity, entity.AdventureEntityId, entity.Coordinate, entity is Combatant combatant ? combatant.FovCoords : null);
        }

        public static AdventureLog EntityRemoved(AdventureEntity entity)
        {
            return new AdventureLog(LogType.Entity, entity.AdventureEntityId, entity.Coordinate, flag: false);
        }

        public static AdventureLog EntityMoved(Combatant entity, Coord destination)
        {
            return new AdventureLog(LogType.Move, entity.AdventureEntityId, entity.Coordinate, entity.FovCoords, 0, destination);
        }

        public static AdventureLog Attacked(Combatant entity, AdventureEntity target)
        {
            return new AdventureLog(LogType.Attack, entity.AdventureEntityId, entity.Coordinate, entity.FovCoords, target.AdventureEntityId, target.Coordinate);
        }

        public static AdventureLog Died(Combatant entity)
        {
            return new AdventureLog(LogType.Died, entity.AdventureEntityId, entity.Coordinate, entity.FovCoords);
        }
    }

    public enum CombatState
    {
        None, Initiation, Engaged 
    }
}