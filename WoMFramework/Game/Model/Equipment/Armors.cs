﻿using System;
using System.Collections.Generic;
using System.Linq;
using WoMFramework.Game.Enums;
using WoMFramework.Tool;

namespace WoMFramework.Game.Model
{
    public class Armors
    {
        private const string DefaultArmorFile = "ArmorBuilders.json";

        private static Armors _instance;

        private readonly List<ArmorBuilder> _armorBuilders;

        private Armors(string path = DefaultArmorFile)
        {
            // load armors file
            if (!Caching.TryReadFile(path, out _armorBuilders))
            {
                throw new Exception("couldn't find the armorbuilders database file.");
            }

            // only  for testing purpose
            //var _armorsFile = _armorBuilders.Select(p => p.Build()).ToList();
        }

        public static Armors Instance => _instance ?? (_instance = new Armors());

        public Armor ByName(string armorName)
        {
            var armorBuilder = _armorBuilders.FirstOrDefault(p => p.Name == armorName);
            if (armorBuilder == null)
            {
                throw new Exception($"Unknown armor please check database '{armorName}'");
            }
            return armorBuilder.Build();
        }

        public List<ArmorBuilder> AllBuilders()
        {
            return _armorBuilders;
        }

    }
}
