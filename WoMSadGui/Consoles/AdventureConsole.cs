﻿using System;
using System.Collections.Generic;
using System.Linq;
using GoRogue;
using Microsoft.Xna.Framework;
using SadConsole;
using SadConsole.Surfaces;
using WoMFramework.Game;
using WoMFramework.Game.Generator;
using WoMFramework.Game.Generator.Dungeon;
using WoMWallet.Node;
using WoMFramework.Game.Model.Mogwai;
using WoMFramework.Game.Model.Monster;
using Console = SadConsole.Console;
using ConsoleEntity = SadConsole.Entities.Entity;

namespace WoMSadGui.Consoles
{
    public class AdventureConsole : MogwaiConsole
    {
        private static readonly Cell UnknownAppearance = new Cell(new Color(25,25,25), Color.Black, 219);
        private static readonly Cell UnclearAppearance = new Cell(new Color(50,205,50), new Color(50,205,50,150), 219);
        private static readonly Cell StoneTileAppearance = new Cell(Color.DarkGray, Color.Black, 46);
        private static readonly Cell StoneWallAppearance = new Cell(Color.DarkGray, Color.Black, 35);

        private readonly Font AdventureFont;

        private readonly MogwaiController _controller;
        private readonly MogwaiKeys _mogwaiKeys;
        private readonly Mogwai _mogwai;

        private readonly Console _mapConsole;
        private readonly int mapViewWidth = 60;
        private readonly int mapViewHeight = 30;


        private readonly Console _statsConsole;

        private readonly Dictionary<int, ConsoleEntity> _entities = new Dictionary<int, ConsoleEntity>();

        public static TimeSpan GameSpeed = TimeSpan.Zero;

        public static TimeSpan ActionDelay = TimeSpan.FromSeconds(0.05);

        private readonly SadConsole.Entities.EntityManager _entityManager;
        public DateTime LastUpdate;

        public Adventure Adventure { get; private set; }

        public AdventureConsole(MogwaiController mogwaiController, MogwaiKeys mogwaiKeys, int width, int height) : base("Custom", "", width, height)
        {
            _controller = mogwaiController;
            _mogwaiKeys = mogwaiKeys;
            _mogwai = _mogwaiKeys.Mogwai;

            AdventureFont = Global.LoadFont("Cheepicus12.font").GetFont(Font.FontSizes.One);

            //_mapConsole = new Console(75, 75) { Position = new Point(17, 2) };
            _mapConsole = new Console(150, 150) {Position = new Point(-16, 0)};
            _mapConsole.Font = AdventureFont;
            
            Children.Add(_mapConsole);
            _entityManager = new SadConsole.Entities.EntityManager {Parent = _mapConsole};

            _statsConsole = new MogwaiConsole("stats", "", 21, 6) { Position = new Point(70, 16) };
            _statsConsole.Fill(DefaultForeground, new Color(100,0,200,150), null);
            Children.Add(_statsConsole);

            _mapConsole.IsVisible = false;

        }

        public bool IsStarted()
        {
            return _mapConsole.IsVisible;
        }

        public void Start(Adventure adventure)
        {
            _mapConsole.ViewPort = new Microsoft.Xna.Framework.Rectangle(0, 0, mapViewWidth, mapViewHeight);
            _mapConsole.Children.Clear();
            _entities.Clear();

            Adventure = adventure;

            DrawExploMap();
            //DrawMap();

            _statsConsole.Print(7, 0, "Rounds", Color.MonoGameOrange);
            _statsConsole.Print(7, 1, "Exploration", Color.Gainsboro);
            _statsConsole.Print(7, 2, "Monster", Color.Gainsboro);
            _statsConsole.Print(7, 3, "Boss", Color.Gainsboro);
            _statsConsole.Print(7, 4, "Treasure", Color.Gainsboro);
            _statsConsole.Print(7, 5, "Portal", Color.Gainsboro);

            // Draw entities (Mogwais, Monsters, etc.)
            foreach (var entity in Adventure.Map.GetEntities())
            {
                if (!entity.IsStatic)
                {
                    DrawEntity(entity);
                }
            }

            _mapConsole.IsVisible = true;
            LastUpdate = DateTime.Now;
        }

        public void Stop()
        {
            _mapConsole.IsVisible = false;

            _mapConsole.Children.Clear();
            _entities.Clear();
            _statsConsole.Clear();
        }

        public void DrawMap()
        {
            // TODO: Viewport
            //Resize(Adventure.Map.Width, Adventure.Map.Height, true);

            var wMap = Adventure.Map.TileMap;
            for (var i = 0; i < wMap.Width; i++)
            {
                for (var j = 0; j < wMap.Height; j++)
                {
                    switch (wMap[i, j])
                    {
                        case StoneTile _:
                            _mapConsole[i, j].CopyAppearanceFrom(StoneTileAppearance);
                            _mapConsole.SetGlyph(i, j, 46);
                            break;
                        case StoneWall _:
                            _mapConsole[i, j].CopyAppearanceFrom(StoneWallAppearance);
                            _mapConsole.SetGlyph(i, j, 35);
                            break;
                        default:
                            break;

                    }
                }
            }
        }

        public void DrawExploMap()
        {
            var wMap = Adventure.Map.ExplorationMap;
            for (var i = 0; i < wMap.Width; i++)
            {
                for (var j = 0; j < wMap.Height; j++)
                {
                    switch (wMap[i, j])
                    {
                        case 0:
                        case -1:
                            DrawMapPoint(i, j, false);
                            break;
                        case -9:
                            _mapConsole[i, j].CopyAppearanceFrom(UnknownAppearance);
                            _mapConsole.SetGlyph(i, j, 219);
                            break;
                        case 1:
                            var cell = UnknownAppearance;
                            // colorized locations ...
                            //foreach (var location in Adventure.Map.Locations)
                            //{
                            //    if (!location.Contains(i, j)) continue;
                            //    var index = Adventure.Map.Locations.IndexOf(location);
                            //    if (index == 0)
                            //    {
                            //        cell = new Cell(Color.Crimson);
                            //        break;
                            //    }
                            //    cell = new Cell(new Color(index * 25 % 256,0,index * 50 % 256), Color.Black, 219);
                            //    break;
                            //}
                            _mapConsole[i, j].CopyAppearanceFrom(cell);
                            _mapConsole.SetGlyph(i, j, 176);
                            break;
                        default:
                            //_mapConsole[i, j].CopyAppearanceFrom(UnclearAppearance);
                            //_mapConsole.SetGlyph(i, j, 219);
                            DrawMapPoint(i, j, true);
                            break;
                    }
                }
            }
        }

        public void DrawMapPoint(int x, int y, bool isUnclear)
        {
            var wMap = Adventure.Map.TileMap;
            switch (wMap[x, y])
            {
                case StoneTile _:
                    _mapConsole[x, y].CopyAppearanceFrom(isUnclear ? UnclearAppearance : StoneTileAppearance);
                    _mapConsole.SetGlyph(x, y, 46);
                    break;

                case StoneWall _:
                    _mapConsole[x, y].CopyAppearanceFrom(isUnclear ? UnclearAppearance : StoneWallAppearance);
                    _mapConsole.SetGlyph(x, y, 35);
                    break;

                default:
                    break;

            }
        }

        public void DrawEntity(IAdventureEntity adventureEntity)
        {
            int glyph;
            Color colour;

            switch (adventureEntity)
            {
                case Mogwai _:
                    glyph = 1;
                    colour = Color.LimeGreen;
                    break;
                case Monster _:
                    glyph = 64;
                    colour = Color.Red;
                    break;
                default:
                    throw new NotImplementedException();
            }

            // TODO: rotating symbols for multiple mogwais
            var animated = new Animated("default", 1, 1, AdventureFont);
            var frame = animated.CreateFrame();
            frame[0].Glyph = glyph;
            frame[0].Foreground = colour;

            var pos = adventureEntity.Coordinate;
            var entity = new ConsoleEntity(animated)
            {
                Position = new Point(pos.X, pos.Y)
            };

            entity.IsVisible = false;

            //_mapConsole.Children.Add(entity);
            _entities.Add(adventureEntity.AdventureEntityId, entity);
            _entityManager.Entities.Add(entity);
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateGame()
        {
            if (!_mapConsole.IsVisible || LastUpdate.Add(GameSpeed) >= DateTime.Now)
                return;

            LastUpdate = DateTime.Now;

            // next frame
            if (Adventure.AdventureLogs.Count == 0 && Adventure.HasNextFrame())
            {
                Adventure.NextFrame();
            }

            if (!Adventure.AdventureLogs.TryDequeue(out var adventureLog))
            {
                GameSpeed = TimeSpan.Zero;
                return;
            }
            
            GameSpeed = _mogwai.CanSee(Adventure.Map.GetEntities().FirstOrDefault(p => p.AdventureEntityId == adventureLog.Source)) ? ActionDelay : TimeSpan.Zero;

            while (Adventure.LogEntries.TryDequeue(out var logEntry))
            {
                if (logEntry.LogType != LogType.AdventureLog)
                    ((PlayScreen) Parent).PushLog(logEntry);
            }

            // redraw map
            DrawExploMap();

            //DrawMap();
            adventureLog.SourceFovCoords?.ToList().ForEach(p => _mapConsole[p.X, p.Y].Background = Color.Lerp(Color.DarkGray, Color.Black, 0.75f));
            
            // stats
            _statsConsole.Print(2, 0, Adventure.GetRound.ToString().PadLeft(4), Color.Gold);
            _statsConsole.Print(2, 1, Adventure.AdventureStats[AdventureStats.Explore].ToString("0%").PadLeft(4), Color.Gold);
            _statsConsole.Print(2, 2, Adventure.AdventureStats[AdventureStats.Monster].ToString("0%").PadLeft(4), Color.Gold);
            _statsConsole.Print(2, 3, Adventure.AdventureStats[AdventureStats.Boss].ToString("0%").PadLeft(4), Color.Gold);
            _statsConsole.Print(2, 4, Adventure.AdventureStats[AdventureStats.Treasure].ToString("0%").PadLeft(4), Color.Gold);
            _statsConsole.Print(2, 5, Adventure.AdventureStats[AdventureStats.Portal].ToString("0%").PadLeft(4), Color.Gold);

            switch (adventureLog.Type)
            {
                case AdventureLog.LogType.Info:
                    break;
                case AdventureLog.LogType.Move:
                    MoveEntity(_entities[adventureLog.Source], adventureLog.TargetCoord);
                    break;
                case AdventureLog.LogType.Attack:
                    AttackEntity(adventureLog.TargetCoord);
                    break;
                case AdventureLog.LogType.Died:
                    DiedEntity(_entities[adventureLog.Source]);
                    break;
                case AdventureLog.LogType.Entity:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var entity in Adventure.Map.GetEntities())
            {
                if (entity.IsStatic)
                {
                    continue;
                }

                ConsoleEntity consoleEntity = _entities[entity.AdventureEntityId];

                consoleEntity.IsVisible =
                    (_mogwai.CanSee(entity) || entity is ICombatant combatant && combatant.IsDead) &&
                    _mapConsole.ViewPort.Contains(consoleEntity.Position);
            }
        }

        private void MoveEntity(ConsoleEntity entity, Coord destination)
        {
            entity.Position = new Point(destination.X, destination.Y);
            _mapConsole.ViewPort = new Microsoft.Xna.Framework.Rectangle(destination.X - mapViewWidth/2, destination.Y - mapViewHeight/2, mapViewWidth, mapViewHeight);
            _entityManager.Sync();
        }

        private void AttackEntity(Coord targetCoord)
        {
            var effect = new Animated("default", 1, 1, AdventureFont);

            var frame = effect.CreateFrame();
            effect.CreateFrame();
            frame[0].Glyph = 15;

            effect.AnimationDuration = 1;
            effect.Start();

            var entity = new ConsoleEntity(effect) { Position = new Point(targetCoord.X, targetCoord.Y) };
            _mapConsole.Children.Add(entity);
            
        }

        private void DiedEntity(ConsoleEntity entity)
        {
            var animated = new Animated("default", 1, 1, AdventureFont);
            var frame = animated.CreateFrame();
            frame[0].Glyph = 206;
            frame[0].Foreground = Color.Gold;
            entity.Animation = animated;
        }
    }
}
