﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GoRogue;
using GoRogue.MapViews;
using WoMFramework.Game.Enums;
using WoMFramework.Game.Generator;
using WoMFramework.Game.Generator.Dungeon;
using WoMFramework.Game.Model.Actions;
using WoMFramework.Game.Model.Mogwai;
using WoMFramework.Game.Random;

namespace WoMFramework.Game.Model
{
    public abstract partial class Entity
    {
        public int GetRequirementValue(RequirementType requirementType, object addValue = null)
        {
            switch (requirementType)
            {
                case RequirementType.Strength:
                    return Strength;
                case RequirementType.Dexterity:
                    return Dexterity;
                case RequirementType.Consititution:
                    return Constitution;
                case RequirementType.Intelligence:
                    return Inteligence;
                case RequirementType.Wisdom:
                    return Wisdom;
                case RequirementType.Charisma:
                    return Charisma;
                case RequirementType.Skill:
                    throw new NotImplementedException();
                case RequirementType.Level:
                    return CurrentLevel;
                case RequirementType.CasterLevel:
                    var casterClasses = Classes.Where(p => p.CanCast);
                    return casterClasses.Any() ? casterClasses.Max(p => p.ClassLevel) : 0;
                case RequirementType.FighterLevel:
                    var fighterClasses = Classes.Where(p => !p.CanCast);
                    return fighterClasses.Any() ? fighterClasses.Max(p => p.ClassLevel) : 0;
                case RequirementType.BaseAttack:
                    return BaseAttackBonus[0];
                default:
                    throw new ArgumentOutOfRangeException(nameof(requirementType), requirementType, null);
            }
        }
    }

    public abstract partial class Entity : Combatant
    {
        public readonly Dictionary<ModifierType, int> MiscMod;

        public readonly Dictionary<ModifierType, int> TempMod;

        public int Gender { get; set; }
        public string GenderStr => ((GenderType)Gender).ToString();

        public SizeType SizeType { get; set; }

        #region abilities

        private int strength;
        public int Strength { get => strength + MiscMod[ModifierType.Strength] + TempMod [ModifierType.Strength]; set => strength = value; }
        public int StrengthMod => Modifier(Strength);

        private int dexterity;
        public int Dexterity { get => dexterity + MiscMod[ModifierType.Dexterity] + TempMod[ModifierType.Dexterity]; set => dexterity = value; }
        public int DexterityMod => Modifier(Dexterity);

        private int constitution;
        public int Constitution { get => constitution + MiscMod[ModifierType.Constitution] + TempMod[ModifierType.Constitution]; set => constitution = value; }
        public int ConstitutionMod => Modifier(Constitution);

        private int inteligence;
        public int Inteligence { get => inteligence + MiscMod[ModifierType.Inteligence] + TempMod[ModifierType.Inteligence]; set => inteligence = value; }
        public int InteligenceMod => Modifier(Inteligence);

        private int wisdom;
        public int Wisdom { get => wisdom + MiscMod[ModifierType.Wisdom] + TempMod[ModifierType.Wisdom]; set => wisdom = value; }
        public int WisdomMod => Modifier(Wisdom);

        private int charisma;
        public int Charisma { get => charisma + MiscMod[ModifierType.Charisma] + TempMod[ModifierType.Charisma]; set => charisma = value; }
        public int CharismaMod => Modifier(Charisma);

        private int Modifier(int ability) => (int)Math.Floor((ability - 10) / 2.0);

        #endregion

        public int CurrentLevel { get; set; } = 1;

        // base speed
        public int BaseSpeed { get; set; }

        // calculate encumbarance and stuff like that ...
        public int Speed => BaseSpeed + MiscMod[ModifierType.Speed] + TempMod[ModifierType.Speed];

        public int NaturalArmor { get; set; }
        // armorclass = 10 + armor bonus + shield bonus + dex modifier + size modifier + natural armor + deflection + misc modifier
        public int ArmorClass => 10 + Equipment.ArmorBonus + Equipment.ShieldBonus + DexterityMod + SizeType.Modifier() + NaturalArmor + MiscMod[ModifierType.ArmorClass] + TempMod[ModifierType.ArmorClass];

        // hitpoints
        public int HitPointDice { get; set; }
        public int[] HitPointDiceRollEvent { get; set; }
        public List<int> HitPointLevelRolls { get; }
        public int MaxHitPoints => HitPointDice + HitPointLevelRolls.Sum();
        public int CurrentHitPoints { get; set; }

        // initiative = dex modifier + misc modifier
        public int Initiative => DexterityMod + MiscMod[ModifierType.Initiative] + TempMod[ModifierType.Initiative];

        #region saving throws

        //saving throw = basesave + abilitymod + misc modifier + magic modifier + temp modifier
        public int FortitudeBaseSave { get; set; }
        public int Fortitude => FortitudeBaseSave + ConstitutionMod + MiscMod[ModifierType.Fortitude] + TempMod[ModifierType.Fortitude];
        public int ReflexBaseSave { get; set; }
        public int Reflex => ReflexBaseSave + DexterityMod + MiscMod[ModifierType.Reflex] + TempMod[ModifierType.Reflex];
        public int WillBaseSave { get; set; }
        public int Will => WillBaseSave + WisdomMod + MiscMod[ModifierType.Will] + TempMod[ModifierType.Will];

        #endregion

        // base attack bonus = class dependent value
        public int[] BaseAttackBonus { get; set; }

        // attackbonus = base attack bonus + strength modifier + size modifier
        public int AttackBonus(int attackIndex) => BaseAttackBonus[attackIndex] + StrengthMod + (int)SizeType + MiscMod[ModifierType.AttackBonus] + TempMod[ModifierType.AttackBonus];

        // attack roll
        public int[] AttackRolls(int attackIndex, int criticalMinRoll = 21)
        {
            var rolls = new List<int>();
            for (var i = 0; i < 3; i++)
            {
                var lastRoll = Dice.Roll(DiceType.D20);
                rolls.Add(lastRoll + AttackBonus(attackIndex));
                if (lastRoll < criticalMinRoll)
                    break;
            }
            return rolls.ToArray();
        }

        // initiative roll
        public int InitiativeRoll => Dice.Roll(DiceType.D20) + Initiative;

        // damage
        public int DamageRoll(Weapon weapon, Dice dice)
        {
            var damage = dice.Roll(weapon.DamageRoll) + (weapon.WeaponEffortType == WeaponEffortType.TwoHanded ? (int)Math.Floor(1.5 * StrengthMod) : StrengthMod);
            return damage < 1 ? 1 : damage;
        }

        public override bool CanAct => (int)HealthState >= 0;
        public override bool IsAlive => HealthState != HealthState.Dead;
        public override bool IsDisabled => HealthState == HealthState.Disabled;
        public override bool IsInjured => HealthState == HealthState.Injured;
        public override bool IsDying => HealthState == HealthState.Dying;
        public override bool IsDead => HealthState == HealthState.Dead;

        // injury and death
        public HealthState HealthState
        {
            get
            {
                if (CurrentHitPoints == MaxHitPoints)
                {
                    return HealthState.Healthy;
                }
                if (CurrentHitPoints > 0)
                {
                    return HealthState.Injured;
                }
                if (CurrentHitPoints == 0)
                {
                    return HealthState.Disabled;
                }
                if (CurrentHitPoints > -10)
                {
                    return HealthState.Dying;
                }

                return HealthState.Dead;
            }
        }

        // equipment
        public Equipment Equipment { get; }

        // wealth
        public Wealth Wealth { get; set; }

        // dice
        public virtual Dice Dice { get; set; }

        //public Classes.Classes CurrentClass => Classes.Count > 0 ? Classes[0] : null;
        public List<Classes.Classes> Classes { get; }
        public int GetClassLevel(ClassType classType)
        {
            var classes = Classes.FirstOrDefault(p => p.ClassType == classType);
            return classes?.ClassLevel ?? 0;
        }

        public EnvironmentType[] EnvironmentTypes { get; set; }

        public List<Feat> Feats { get; set; }

        public List<Spell> Spells { get; set; }

        public List<BaseItem> Inventory { get; }

        /// <summary>
        /// 
        /// </summary>
        protected Entity() : base(false, false, 1)
        {
            // modifiers
            MiscMod = new Dictionary<ModifierType, int>();
            TempMod = new Dictionary<ModifierType, int>();
            foreach (ModifierType modifierType in Enum.GetValues(typeof(ModifierType)))
            {
                TempMod[modifierType] = 0;
                MiscMod[modifierType] = 0;
            }

            // initialize
            HitPointLevelRolls = new List<int>();
            Equipment = new Equipment();
            Wealth = new Wealth();
            Classes = new List<Classes.Classes>();

            EngagedEnemies = new List<Entity>();

            // add basic actions
            CombatActions = new List<CombatAction>
            {
                CombatAction.CreateMove(this)
            };

            // initialize skills list
            Feats = new List<Feat>();

            // initialize spells list
            Spells = new List<Spell>();

            // initialize inventory
            Inventory = new List<BaseItem>();

            // unlooted state
            LootState = LootState.Unlooted;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dice"></param>
        public void LevelClass(Dice dice)
        {
            BaseAttackBonus = CalculateBaseAttackBonus(Classes.Sum(p => p.ClassAttackBonus));
            FortitudeBaseSave = Classes.Sum(p => p.FortitudeBaseSave);
            ReflexBaseSave = Classes.Sum(p => p.ReflexBaseSave);
            WillBaseSave = Classes.Sum(p => p.WillBaseSave);
            HitPointDiceRollEvent = Classes[0].HitPointDiceRollEvent;
            HitPointLevelRolls.Add(dice.Roll(HitPointDiceRollEvent));

            Classes.ForEach(p => p.Learnables.ForEach(q => Learn(q)));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lernable"></param>
        /// <returns></returns>
        public bool Learn(Learnable lernable)
        {
            if (!lernable.CanLearn(this))
            {
                return false;
            }

            return lernable.Learn(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="attackBonus"></param>
        /// <returns></returns>
        private int[] CalculateBaseAttackBonus(int attackBonus)
        {
            var currentBaseAttackBonus = attackBonus;

            var baseAttackBonusList = new List<int> { currentBaseAttackBonus };

            for (var i = currentBaseAttackBonus - 5; i > 0; i = i - 5)
            {
                baseAttackBonusList.Add(i);
            }
            return baseAttackBonusList.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseItem"></param>
        /// <returns></returns>
        public bool AddToInventory(BaseItem baseItem)
        {
            // TODO check if we are not encumbered!

            Inventory.Add(baseItem);

            baseItem.ChangeOwner(this);

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseItem"></param>
        /// <returns></returns>
        public bool RemoveFromInventory(BaseItem baseItem)
        {
            if (!Inventory.Contains(baseItem))
            {
                return false;
            }

            Inventory.Remove(baseItem);
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slotType"></param>
        /// <param name="baseItem"></param>
        /// <param name="slot"></param>
        /// <returns></returns>
        public bool CanEquipeItem(SlotType slotType, BaseItem baseItem, out EquipmentSlot slot, int slotIndex = 0)
        {
            slot = Equipment.Slots.Where(p => p.SlotType == slotType).ElementAtOrDefault(slotIndex);

            return Inventory.Contains(baseItem) 
                   && baseItem.SlotType == slotType 
                   && slot != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slotType"></param>
        /// <param name="baseItem"></param>
        /// <returns></returns>
        public bool EquipeItem(SlotType slotType, BaseItem baseItem, int slotIndex = 0)
        {
            if (!CanEquipeItem(slotType, baseItem, out var slot, slotIndex))
            {
                return false;
            }

            // removed old item
            if (slot.BasicItem != null)
            {
                var oldItem = slot.BasicItem;
                slot.BasicItem = null;

                if (!AddToInventory(oldItem))
                {
                    return false;
                }

                // unlearn old item
                oldItem.UnLearn(this);
            }

            // add new item
            if (!RemoveFromInventory(baseItem))
            {
                return false;
            }

            slot.BasicItem = baseItem;

            // learn new item
            baseItem.Learn(this);

            return true;
        }

        public bool CanEquipeWeapon(SlotType slotType, Weapon weapon, int slotIndex, out WeaponSlot slot)
        {
            slot = Equipment.GetWeaponSlot(slotIndex);

            return Inventory.Contains(weapon) 
                   && weapon.SlotType == slotType 
                   && slot != null;
        }

        public bool EquipeWeapon(Weapon baseItem, Weapon secondaryWeapon = null, int slotIndex = 0)
        {

            if (!CanEquipeWeapon(SlotType.Weapon, baseItem, slotIndex, out var slot))
            {
                return false;
            }

            // removed old item
            if (slot.PrimaryWeapon != null)
            {
                var oldItem = slot.PrimaryWeapon;
                slot.PrimaryWeapon = null;

                if (!AddToInventory(oldItem))
                {
                    return false;
                }

                // unlearn old item
                oldItem.UnLearn(this);
            }

            // add new item
            if (!RemoveFromInventory(baseItem))
            {
                return false;
            }

            slot.PrimaryWeapon = baseItem;

            // learn new item
            baseItem.Learn(this);

            // standard attack
            //CombatActions.Add(CombatAction.CreateWeaponAttack(this, baseItem, false));

            // full attack
            //CombatActions.Add(CombatAction.CreateWeaponAttack(this, baseItem, true));

            slot.PrimaryWeapon = baseItem;

            if (secondaryWeapon != null)
            {
                // TODO implement secondary weapon
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="attackRolls"></param>
        /// <param name="armorClass"></param>
        /// <param name="criticalCount"></param>
        /// <returns></returns>
        private int AttackRoll(int[] attackRolls, int armorClass, out int criticalCount)
        {
            var attack = attackRolls[attackRolls.Length - 1];
            if (attack > armorClass)
            {
                criticalCount = attackRolls.Length - 1;
                return attack;
            }
            criticalCount = attackRolls.Length > 2 ? attackRolls.Length - 2 : 0;
            return attack;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gold"></param>
        public virtual void AddGold(int gold)
        {
            // nothing here ..
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="monster"></param>
        public virtual void AddExp(double exp, Monster.Monster monster = null)
        {
            // nothing here ..
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="healAmount"></param>
        /// <param name="healType"></param>
        public void Heal(int healAmount, HealType healType)
        {
            var missingHealth = MaxHitPoints - CurrentHitPoints;
            if (missingHealth <= 0 || healAmount <= 0)
            {
                return;
            }

            if (missingHealth < healAmount)
            {
                healAmount = missingHealth;
            }

            Mogwai.Mogwai.History.Add(LogType.Heal, $"{Coloring.Name(Name)} restores {Coloring.GetHeal(healAmount)} HP from {healType.ToString()} healing.");
            Adventure?.LogEntries.Enqueue(new LogEntry(LogType.Heal, $"{Coloring.Name(Name)} restores {Coloring.GetHeal(healAmount)} HP from {healType.ToString()} healing."));
            CurrentHitPoints += healAmount;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="damageAmount"></param>
        /// <param name="damageType"></param>
        public void Damage(int damageAmount, DamageType damageType)
        {
            // no damage return or same for dead entities
            if (damageAmount <= 0 || IsDead)
            {
                return;
            }

            Mogwai.Mogwai.History.Add(LogType.Damage, $"{Coloring.Name(Name)} suffers {Coloring.GetDmg(damageAmount)} HP from {damageType.ToString()} damage.");
            var logEntry = LogEntry.Damage(this, damageAmount, damageType);
            Adventure.LogEntries.Enqueue(new LogEntry(LogType.Damage, $"{Coloring.Name(Name)} suffers {Coloring.GetDmg(damageAmount)} HP from {damageType.ToString()} damage."));
            CurrentHitPoints -= damageAmount;

            if (!CanAct)
            {
                Mogwai.Mogwai.History.Add(LogType.Damage, $"{Coloring.Name(Name)} got a deadly hit, healthstate is {Coloring.Red(HealthState.ToString())}.");
                Adventure.LogEntries.Enqueue(new LogEntry(LogType.Damage, $"{Coloring.Name(Name)} got a deadly hit, healthstate is {Coloring.Red(HealthState.ToString())}."));
            }

            if (IsDead)
            {
                Mogwai.Mogwai.History.Add(LogType.Damage, $"{Coloring.Name(Name)} has died, may its soul rest in peace. Its healthstate is {Coloring.Red(HealthState.ToString())}.");
                Adventure.LogEntries.Enqueue(new LogEntry(LogType.Damage, $"{Coloring.Name(Name)} has died, may its soul rest in peace. Its healthstate is {Coloring.Red(HealthState.ToString())}."));
                //Adventure.LogEntries.Enqueue(new LogEntry(LogType.Info, $"{Coloring.Name(Name)} treasure? {this.Treasure}. IsLootable: {IsLootable}; LootState: {LootState}; HasLoot: {HasLoot}"));
                
                Map.DeadEntity(this);                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        public void Loot(AdventureEntity entity)
        {
            // only allow mogwais to loot
            if (!(this is Mogwai.Mogwai mogwai))
            {
                return;
            }

            if (entity.LootState == LootState.None 
             || entity.LootState == LootState.Looted)
            {
                return;
            }

            Mogwai.Mogwai.History.Add(LogType.Info, $"{Coloring.Name(Name)} is looting {Coloring.DarkGrey(entity.Name)}.");
            Adventure?.LogEntries.Enqueue(new LogEntry(LogType.Info, $"{Coloring.Name(Name)} is looting {Coloring.DarkGrey(entity.Name)}."));

            if (entity.Treasure == null)
            {
                Mogwai.Mogwai.History.Add(LogType.Info, $"{Coloring.DarkGrey(entity.Name)} has nothing to loot.");
                Adventure?.LogEntries.Enqueue(new LogEntry(LogType.Info, $"{Coloring.DarkGrey(entity.Name)} has nothing to loot."));
            }
            else
            {
                Mogwai.Mogwai.History.Add(LogType.Info, $"{Coloring.DarkGrey(entity.Name)} has a treasure to loot.");
                Adventure?.LogEntries.Enqueue(new LogEntry(LogType.Info, $"{Coloring.DarkGrey(entity.Name)} has a treasure to loot."));
                mogwai.AddGold(entity.Treasure.Gold);
            }

          entity.LootState = LootState.Looted;
        }

        #region AdventureEntity

        public override bool TakeAction(EntityAction entityAction)
        {
            if (!entityAction.IsExecutable)
            {
                return false;
            }

            // weapon attack
            if (entityAction is WeaponAttack weaponAttack)
            {
                Attack(weaponAttack);
                return true;
            }

            // cast spell
            if (entityAction is SpellCast spellCast)
            {
                return Cast(spellCast);
            }

            // move
            if (entityAction is MoveAction moveAction)
            {
                return Move(moveAction.Destination, true);
            }

            return false;
        }

        #endregion

        #region Combatant

        public bool LootablesInSight(out List<AdventureEntity> lootableEntities)
        {
            lootableEntities = new List<AdventureEntity>();
            foreach (var fovCoord in FovCoords)
            {
                if (Map.EntityMap[fovCoord] != null)
                {
                    foreach (var entity in Map.EntityMap[fovCoord].Entities)
                    {
                        if (entity != null 
                         && this != entity
                         && entity.IsLootable 
                         && entity.HasLoot
                         && (!(entity is Combatant combatant) || combatant.IsDead))
                        {
                            lootableEntities.Add(entity);
                        }
                    }
                }
            }

            return lootableEntities.Any();
        }

        public override bool CanSee(AdventureEntity entity)
        {
            return entity != null && FovCoords.Any(p => entity.Coordinate == p);
        }

        public override bool IsInReach(AdventureEntity entity)
        {
            return Distance.EUCLIDEAN.Calculate(entity.Coordinate - Coordinate) <= 2d;
        }

        /// <summary>
        /// Reset all adventure and combat stats
        /// </summary>
        public override void Reset()
        {
            CombatState = CombatState.None;
            EngagedEnemies.Clear();
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spellCast"></param>
        /// <returns></returns>
        private bool Cast(SpellCast spellCast)
        {
            var spell = spellCast.Spell;
            var target = spellCast.Target as Entity;

            var concentrateRoll = 10;

            var message = $"{Coloring.Name(Name)}({Coloring.Hitpoints(CurrentHitPoints)}) cast " +
                          $"{Coloring.Violet(spell.Name)} on {Coloring.Name(target.Name)} roll {concentrateRoll.ToString()}:";

            if (concentrateRoll > 1 && concentrateRoll > spell.Level)
            {
                Mogwai.Mogwai.History.Add(LogType.Comb, $"{message} {Coloring.Green("success")}!");
                Adventure.LogEntries.Enqueue(new LogEntry(LogType.Comb, $"{message} {Coloring.Green("success")}!"));
                spell.SpellEffect(this, target);
            }
            else
            {
                Mogwai.Mogwai.History.Add(LogType.Comb, $"{message} {Coloring.Red("failed")}!");
                Adventure.LogEntries.Enqueue(new LogEntry(LogType.Comb, $"{message} {Coloring.Red("failed")}!"));
            }

            Adventure.Enqueue(AdventureLog.Attacked(this, target));
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="weaponAttack"></param>
        /// <returns></returns>
        private void Attack(WeaponAttack weaponAttack)
        {
            var attackTimes = weaponAttack.ActionType == ActionType.Full ? BaseAttackBonus.Length : 1;
            var weapon = weaponAttack.Weapon;
            var target = weaponAttack.Target as Entity;

            //Console.WriteLine($"{Name}: is acking {attackTimes} times");

            // all attacks are calculated
            for (var attackIndex = 0; attackIndex < attackTimes; attackIndex++)
            {
                // break when target is null or dead, no more attacks on dead monsters.
                if (target == null || target.IsDead)
                {
                    break;
                }

                //Console.WriteLine($"{weapon.Name} CriticalMinRoll: {weapon.CriticalMinRoll}");
                var attackRolls = AttackRolls(attackIndex, weapon.CriticalMinRoll);
                var attack = AttackRoll(attackRolls, target.ArmorClass, out var criticalCounts);

                var attackStr = criticalCounts > 0 ? "critical" : attack.ToString();
                var attackIndexStr = attackIndex + 1 + (attackIndex == 0 ? "st" : "th");
                var message = $"{Coloring.Name(Name)}({Coloring.Hitpoints(CurrentHitPoints)}) {Coloring.Orange(attackIndexStr)} " +
                              $"{weaponAttack.GetType().Name.ToLower()}[{Coloring.Gainsboro(weaponAttack.ActionType.ToString().Substring(0, 1))}] {Coloring.Name(target.Name)} with {Coloring.DarkName(weapon.Name)} roll {Coloring.Attack(attackStr)}[{Coloring.Armor(target.ArmorClass)}]:";

                if (attack > target.ArmorClass || criticalCounts > 0)
                {
                    var damage = DamageRoll(weapon, Dice);
                    var criticalDamage = 0;
                    if (criticalCounts > 0)
                    {
                        for (var i = 0; i < weapon.CriticalMultiplier - 1; i++)
                        {
                            criticalDamage += DamageRoll(weapon, Dice);
                        }
                    }
                    var criticalStr = criticalDamage > 0 ? $"(+{Coloring.DoCritDmg(criticalDamage)})" : string.Empty;
                    Mogwai.Mogwai.History.Add(LogType.Comb, $"{message} {Coloring.Green("hit for")} {Coloring.DoDmg(damage)}{criticalStr} {Coloring.Green("damage!")}");
                    Adventure.LogEntries.Enqueue(new LogEntry(LogType.Comb, $"{message} {Coloring.Green("hit for")} {Coloring.DoDmg(damage)}{criticalStr} {Coloring.Green("damage!")}"));
                    target.Damage(damage + criticalDamage, DamageType.Weapon);
                }
                else
                {
                    Mogwai.Mogwai.History.Add(LogType.Comb, $"{message} {Coloring.Red("failed")}!");
                    Adventure.LogEntries.Enqueue(new LogEntry(LogType.Comb, $"{message} {Coloring.Red("failed")}!"));
                }
                Adventure.Enqueue(AdventureLog.Attacked(this, target));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="checkForEnemies"></param>
        /// <returns></returns>
        private bool Move(Coord destination, bool checkForEnemies = false)
        {
            // initialise 
            var moveRange = Speed / 5;

            var path = Algorithms.AStar(Coordinate, destination, Map);

            if (path == null)
                return false;

            var i = 1;
            var diagonalCount = 0;

            // TODO: Implement the exact movement rule in the manual
            while (moveRange > 0)
            {
                // check if we have an enemy in fov
                if (checkForEnemies && CombatState == CombatState.None)
                {
                    // check field of view positions for enemies
                    foreach (var entity in Map.EntitiesOnCoords(FovCoords).Where(p => p.IsAlive))
                    {
                        if (entity.Faction == Faction.None) continue;
                        if (Faction != entity.Faction)
                        {
                            CombatState = CombatState.Initiation;
                            entity.CombatState = CombatState.Initiation;
                        }
                    }
                    // break if we have found an enemy
                    if (CombatState == CombatState.Initiation)
                    {
                        break;
                    }
                }

                if (path.Length <= i)
                {
                    break;
                }

                var next = path[i++];
                if (next == destination && !Map.WalkabilityMap[next])
                {
                    break;
                }

                MoveAtomic(next, ref moveRange, ref diagonalCount);
            }

            return true;
        }

        protected bool MoveAtomic(Coord next, ref int moveRange, ref int diagonalCount)
        {
            // Should check the destination is adjacent tile

            // Check it is diagonal or cardinal
            if (Distance.EUCLIDEAN.Calculate(Coordinate, next) > 1)
            {
                diagonalCount++;
                if (diagonalCount % 2 == 1)
                {
                    Map.MoveEntity(this, next);
                    moveRange--;
                }
                else
                {
                    if (moveRange == 1)
                    {
                        var newNext = next.Translate(0, -(next - Coordinate).Y);  // Prefer X direction for now; can be randomised
                        if (Map.WalkabilityMap[newNext])
                            Map.MoveEntity(this, newNext);
                        else
                        {
                            newNext = next.Translate(-(next - Coordinate).X, 0);
                            if (Map.WalkabilityMap[newNext])
                                Map.MoveEntity(this, newNext);
                            else
                            {
                                // we dismiss the last point, as probably it's a diag
                                return true;
                            }
                        }

                        return false;
                    }

                    Map.MoveEntity(this, next);
                    moveRange -= 2;
                }
            }
            else
            {
                Map.MoveEntity(this, next);
                moveRange--;
            }
            return true;
        }

        public FOV privateFOV;
        private Coord[] _lastPath;
        private int _pathIndex = -1;
        private bool _foundLootable;
        private AdventureEntity _currentLoot;

        public void ExploreDungeon(bool checkEnemies)
        {
            // expMap
            // -1 : impassable
            //  0 : uncharted   (WHITE)
            //  1 : observed through FOV (GREY)
            //  2 : Visited or having no explorable tiles (BLACK)

            const int FOVRANGE = 5;

            var expMap = Map.ExplorationMap;

            var moveRange = Speed / 5;
            var diagonalCount = 0;

            //bool foundLootable = false;
            //AdventureEntity currentLoot = null;

            while (moveRange > 0)
            {
                // check if we have an enemy in fov
                if (checkEnemies && CombatState == CombatState.None)
                {
                    // check field of view positions for enemies
                    foreach (var entity in Map.EntitiesOnCoords(FovCoords).Where(p => p.IsAlive))
                    {
                        if (entity.Faction == Faction.None) continue;

                        if (Faction != entity.Faction)
                        {
                            CombatState = CombatState.Initiation;
                            entity.CombatState = CombatState.Initiation;
                        }
                    }
                    // break if we have found an enemy
                    if (CombatState == CombatState.Initiation)
                    {
                        break;
                    }
                }
                // check if we have lootable entities in fov
                if (!_foundLootable && LootablesInSight(out var lootableEntities))
                {
                    // Searching for the nearest reachable loot.
                    Coord[] nearestPath = null;
                    foreach (var loot in lootableEntities)
                    {
                        //System.Console.WriteLine($"Is in reach {loot.Name}? {IsInReach(loot)}");
                        if (IsInReach(loot))
                        {
                            //System.Console.WriteLine($"looting {loot.Name}");
                            Loot(loot);
                            continue;
                        }

                        var path = Algorithms.AStar(Coordinate, loot.Coordinate, Map);


                        if (nearestPath == null)
                        {
                            nearestPath = path;
                            _currentLoot = loot;
                        }
                        else if (path != null && nearestPath.Length > path.Length)
                        {
                            nearestPath = path;
                            _currentLoot = loot;
                        }
                    }

                    if (nearestPath != null)
                    {
                        // Set a path for the objective.
                        _foundLootable = true;
                        _lastPath = nearestPath;
                        _pathIndex = 2;

                        if (!MoveAtomic(nearestPath[1], ref moveRange, ref diagonalCount))
                            return;

                        continue;
                    }
                }
                else if (_foundLootable && IsInReach(_currentLoot))
                {
                    Loot(_currentLoot);
                    _foundLootable = false;
                    _currentLoot = null;
                }

                // check if already have a path
                if (_pathIndex > 0)
                {
                    if (_pathIndex != _lastPath.Length)
                    {
                        if (Coordinate == _lastPath[_pathIndex - 1])
                        {
                            if (!MoveAtomic(_lastPath[_pathIndex++], ref moveRange, ref diagonalCount))
                                return;

                            continue;
                        }
                    }
                    else if (_foundLootable)
                    {
                        // This branch means this entity follows a proper path to the current loot,
                        // and in terms of path there is only 1 step left to the loot
                        // but the one step is diagonal so that it is not reachable from the current coordinate.
                        // To handle this problem, set the next coordinate as one of the cardinally adjacent tiles
                        // of the loot.
                        if (Coord.EuclideanDistanceMagnitude(_currentLoot.Coordinate - Coordinate) == 2)
                        {
                            var loot = _currentLoot.Coordinate;

                            var projected = loot.Translate(0, -(loot - Coordinate).Y); // Prefer X direction for now; can be randomised
                            if (Map.WalkabilityMap[projected])
                                MoveAtomic(projected, ref moveRange, ref diagonalCount);
                            else
                            {
                                projected = loot.Translate(-(loot - Coordinate).X, 0);
                                if (Map.WalkabilityMap[projected])
                                    MoveAtomic(projected, ref moveRange, ref diagonalCount);
                                else
                                {
                                    _foundLootable = false;
                                    _currentLoot = null;
                                }
                            }
                        }
                        else
                        {
                            _foundLootable = false;
                            _currentLoot = null;
                        }
                    }
                }

                _pathIndex = -1;

                // Atomic movement; consider adjacent tiles first
                var adjs = Algorithms.GetReachableNeighbours(Map.WalkabilityMap, Coordinate);
                var max = 0;
                var maxIndex = -1;
                for (var i = 0; i < adjs.Length; i++)
                {
                    // Not consider Black tiles
                    if (expMap[adjs[i]] == 2) continue;

                    var c = Map.ExpectedFovNum[adjs[i].X, adjs[i].Y];
                    // Calculate which adjacent tile has the greatest number of expected FOV tiles
                    if (max < c)
                    {
                        max = c;
                        maxIndex = i;
                    }
                }

                Coord next;

                // If all adjacent tiles are Black, explorer have to find the nearest grey tile.
                if (maxIndex < 0)
                {
                    var minLength = int.MaxValue;

                    Coord[] nearest = null;

                    // Consider grey tiles in FOV first. If not any, check the whole map
                    var inFov = FovCoords
                        .Where(c => expMap[c] == 1)
                        .ToArray();

                    if (inFov.Length > 0)
                    {
                        nearest = inFov
                            .Select(c => Algorithms.AStar(Coordinate, c, Map))
                            .OrderBy(p => p.Length)
                            .First();
                    }
                    else
                    {
                        nearest = expMap.Positions()
                            .Where(c => expMap[c] == 1)
                            .OrderBy(p => Distance.EUCLIDEAN.Calculate(Coordinate, p))
                            .Take(5)
                            .Where(p => p != null)
                            .Select(p => Algorithms.AStar(Coordinate, p, Map))
                            .OrderBy(p => p.Length)
                            .First();
                    }

                    if (nearest?.Length < 2)
                    {
                        for (int i = 0; i < expMap.Width; i++)
                        for (int j = 0; j < expMap.Height; j++)
                            expMap[i, j] = 2;

                        return;
                    }

                    next = nearest[1];

                    // Save the path
                    _lastPath = nearest;
                    _pathIndex = 2;
                }
                else
                {
                    next = adjs[maxIndex];
                }

                if (!MoveAtomic(next, ref moveRange, ref diagonalCount))
                    return;
            }
        }

        public void WalkTo(Coord coord, bool checkForEnemies)
        {
            // expMap
            // -1 : impassable
            //  0 : uncharted   (WHITE)
            //  1 : observed through FOV (GREY)
            //  2 : Visited or having no explorable tiles (BLACK)

            var moveRange = Speed / 5;
            var diagonalCount = 0;

            while (moveRange > 0)
            {
                // check if we have an enemy in fov
                if (checkForEnemies && CombatState == CombatState.None)
                {
                    // check field of view positions for enemies
                    foreach (var entity in Map.EntitiesOnCoords(FovCoords).Where(p => p.IsAlive))
                    {
                        if (entity.Faction == Faction.None) continue;

                        if (Faction != entity.Faction)
                        {
                            CombatState = CombatState.Initiation;
                            entity.CombatState = CombatState.Initiation;
                        }
                    }
                    // break if we have found an enemy
                    if (CombatState == CombatState.Initiation)
                    {
                        break;
                    }
                }

                // check if already have a path
                if (_pathIndex > 0 && _pathIndex != _lastPath.Length)
                {
                    if (Coordinate == _lastPath[_pathIndex - 1])
                    {
                        if (!MoveAtomic(_lastPath[_pathIndex++], ref moveRange, ref diagonalCount))
                            return;

                        continue;
                    }
                }

                _pathIndex = -1;

                var nearest = Algorithms.AStar(Coordinate, coord, Map);

                var next = nearest[1];

                // Save the path
                _lastPath = nearest;
                _pathIndex = 2;

                if (!MoveAtomic(next, ref moveRange, ref diagonalCount))
                    return;
            }
        }

    }
}
