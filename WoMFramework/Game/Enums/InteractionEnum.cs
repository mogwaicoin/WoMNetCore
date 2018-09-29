﻿namespace WoMFramework.Game.Enums
{
    public enum InteractionType
    {
        None = 0,
        Creation = 1,
        Modification = 2,
        Leveling = 3,
        Adventure = 4,
        Duell = 5,
        Breeding = 6,
        Inventory = 7
    }

    public enum CostType
    {
        Standard = 1
    }

    public enum LevelingType
    {
        None = 0,
        Class = 1,
        Ability = 2
    }

    public enum AdventureType
    {
        TestRoom = 0,
        Chamber = 1,
        Dungeon = 2,
        Battle = 3,
        Quest = 4
    }

    public enum DifficultyType
    {
        Easy = -1,
        Average = 0,
        Challenging = 1,
        Hard = 2,
        Epic = 3
    }

}