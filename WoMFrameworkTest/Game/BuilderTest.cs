﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WoMFramework.Game.Enums;
using WoMFramework.Game.Model;
using WoMFramework.Game.Model.Monster;
using Xunit;

namespace WoMFrameworkTest.Game
{
    public class BuilderTest
    {
        [Fact]
        public void BuilderCreationTest()
        {
            var allMonster = Monsters.Instance.AllBuilders().Select(p => p.Build());
            Assert.Equal(1321, allMonster.Count());
            var allArmor = Armors.Instance.AllBuilders().Select(p => p.Build());
            Assert.Equal(62, allArmor.Count());
            var allWeapons = Weapons.Instance.AllBuilders() .Select(p => p.Build());
            Assert.Equal(212, allWeapons.Count());
        }

        [Fact]
        public void BuilderMonsterTest()
        {
            var allMonster = Monsters.Instance.AllBuilders().Select(p => p.Build());
            Assert.Equal(42, allMonster.Where(p => (p.EnvironmentTypes.Contains(EnvironmentType.Any)
                                                 || p.EnvironmentTypes.Contains(EnvironmentType.Undergrounds))
                                                && p.ChallengeRating <= 0.5).Count());
            Assert.Equal(110, allMonster.Where(p => p.ChallengeRating < 1).Count());
            Assert.Equal( 90, allMonster.Where(p => p.ChallengeRating == 1).Count());
            Assert.Equal(132, allMonster.Where(p => p.ChallengeRating == 2).Count());
            Assert.Equal(126, allMonster.Where(p => p.ChallengeRating == 3).Count());
            Assert.Equal("Chickcharney", allMonster.Where(p => p.ChallengeRating == 3 && p.NaturalArmor == 0).First().Name);

        }
    }
}
