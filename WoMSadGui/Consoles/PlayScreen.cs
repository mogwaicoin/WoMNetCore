﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SadConsole;
using SadConsole.Controls;
using SadConsole.Surfaces;
using WoMFramework.Game;
using WoMFramework.Game.Enums;
using WoMFramework.Game.Interaction;
using WoMFramework.Game.Model.Mogwai;
using WoMSadGui.Dialogs;
using WoMWallet.Node;
using Console = SadConsole.Console;
using Keyboard = SadConsole.Input.Keyboard;

namespace WoMSadGui.Consoles
{
    public class TestControls : ControlsConsole
    {
        public Basic BorderSurface;

        public TestControls(int width, int height) : base(width, height)
        {
            BorderSurface = new Basic(width + 2, height + 2, Font);
            BorderSurface.DrawBox(new Rectangle(0, 0, BorderSurface.Width, BorderSurface.Height),
                new Cell(Color.DarkCyan, Color.TransparentBlack));
            BorderSurface.Position = new Point(-1, -1);
            Children.Add(BorderSurface);
        }
    }
    public class PlayScreen : Console
    {
        private readonly MogwaiController _controller;

        private int _glyphX;
        private int _glyphY;
        private int _oldglyphIndex = 185;
        private int _glyphIndex = 185;

        public SadGuiState State { get; set; }

        private readonly AdventureConsole _custom;

        private readonly ScrollingConsole _log;

        private readonly Mogwai _mogwai;

        private readonly TestControls _command1;
        private readonly ControlsConsole _command2;

        public PlayScreen(MogwaiController mogwaiController, int width, int height) : base(width, height)
        {
            _controller = mogwaiController;
            var mogwaiKeys = _controller.CurrentMogwaiKeys ?? _controller.TestMogwaiKeys();
            _mogwai = mogwaiKeys.Mogwai;

            var playStatsConsole = new PlayStatsConsole(_mogwai, 44, 22) { Position = new Point(0, 0) };
            Children.Add(playStatsConsole);

            _custom = new AdventureConsole(mogwaiController, mogwaiKeys, 91, 22) { Position = new Point(46, 0) };
            Children.Add(_custom);

            _log = new ScrollingConsole(85, 13, 100) { Position = new Point(0, 25) };
            Children.Add(_log);

            var playInfoConsole = new PlayInfoConsole(mogwaiController, mogwaiKeys, 49, 14) { Position = new Point(88, 24) };
            Children.Add(playInfoConsole);

            _command1 = new TestControls(86, 1) { Position = new Point(0, 23) };
            _command1.Fill(Color.Transparent, Color.Black, null);
            Children.Add(_command1);

            _command2 = new ControlsConsole(8, 2) { Position = new Point(40, 2) };
            _command2.Fill(Color.Transparent, Color.DarkGray, null);
            playInfoConsole.Children.Add(_command2);

            State = SadGuiState.Play;

            Init();
        }

        public void Init()
        {
            IsVisible = true;
            if (_controller.CurrentMogwai != null)
                _controller.RefreshCurrent(1);

            _command1.BorderSurface.SetGlyph(0, 0, 204, Color.DarkCyan);
            _command1.BorderSurface.SetGlyph(0, 1, 186, Color.DarkCyan);
            _command1.BorderSurface.SetGlyph(0, 2, 200, Color.DarkCyan);

            MenuButton(0, "level", DoAction);
            MenuButton(1, "inven", DoAction);
            MenuButton(2, "adven", DoAction);
            MenuButton(3, "modif", DoAction);
            MenuButton(4, "breed", DoAction);
            MenuButton(5, "shop", DoAction);

            var btnNext = new MogwaiButton(8, 1)
            {
                Position = new Point(0, 0),
                Text = "evolve"
            };
            btnNext.Click += (btn, args) => { DoAction(((Button)btn).Text); };
            _command2.Add(btnNext);

            var btnFast = new MogwaiButton(8, 1)
            {
                Position = new Point(0, 1),
                Text = "evol++"
            };
            btnFast.Click += (btn, args) => { DoAction(((Button)btn).Text); };
            _command2.Add(btnFast);

        }

        private void MenuButton(int buttonPosition, string buttonText, Action<string> buttonClicked)
        {
            var xBtn = 0;
            var xSpBtn = 1;
            var mBtnSize = 7;

            var button = new MogwaiButton(mBtnSize, 1)
            {
                Position = new Point(xBtn + buttonPosition * (mBtnSize + xSpBtn), 0),
                Text = buttonText
            };
            button.Click += (btn, args) => { buttonClicked(((Button)btn).Text); };
            _command1.Add(button);
            _command1.SetGlyph(xBtn + mBtnSize + buttonPosition * (mBtnSize + xSpBtn), 0, 186, Color.DarkCyan);
            _command1.BorderSurface.SetGlyph(xBtn + mBtnSize + 1 + buttonPosition * (mBtnSize + xSpBtn), 0, 203, Color.DarkCyan);
            for (int i = 0; i < mBtnSize; i++)
            {
                _command1.BorderSurface.SetGlyph(xBtn + i + 1 + buttonPosition * (mBtnSize + xSpBtn), 2, 205, Color.DarkCyan);
            }
            _command1.BorderSurface.SetGlyph(xBtn + mBtnSize + 1 + buttonPosition * (mBtnSize + xSpBtn), 2, 202, Color.DarkCyan);
        }

        private void DoAction(string actionStr)
        {
            MogwaiOptionDialog dialog;
            switch (actionStr)
            {
                case "evolve":
                    Evolve();
                    break;

                case "evol++":
                    Evolve(true);
                    break;

                case "adven":
                    dialog = new MogwaiOptionDialog("Adventure", "Choose the Adventure?", DoAdventureAction, 40, 12);
                    dialog.AddRadioButtons("adventureAction", new List<string[]> {
                            new[] {"testroom", "Test Room"},
                            new[] {"chamber", "Chamber"},
                            new[] {"dungeon", "Dungeon"},
                            new[] {"battle", "Battle"},
                            new[] {"quest", "Quest"}
                        });
                    dialog.Show(true);
                    break;
                case "level":
                    dialog = new MogwaiOptionDialog("Leveling", "Currently up for leveling?", DoAdventureAction, 40, 12);
                    dialog.AddRadioButtons("levelingAction", new List<string[]> {
                        new[] {"levelclass", "Class levels."},
                        new[] {"levelability", "Ability levels."}
                    });
                    dialog.Show(true);
                    break;
            }

        }

        private void DoAdventureAction(string actionStr)
        {
            switch (actionStr)
            {
                case "testroom":
                    LogInConsole(
                        _controller.Interaction(new AdventureAction(AdventureType.TestRoom, DifficultyType.Average,
                            _mogwai.CurrentLevel))
                            ? "Successful sent mogwai to test room! Wait for interaction locks."
                            : "Failed to send mogwai to test room!");
                    break;
                case "levelclass":
                    var dialog = new MogwaiOptionDialog("Leveling", "Currently up for leveling?", DoClassLevel, 40, 17);
                    dialog.AddRadioButtons("levelingAction", new List<string[]> {
                        new[] { "Barbarian", "Barbarian"},
                        new[] { "Bard", "Bard"},
                        new[] { "Cleric", "Cleric"},
                        new[] { "Druid", "Druid"},
                        new[] { "Fighter", "Fighter"},
                        new[] { "Monk", "Monk"},
                        new[] { "Paladin", "Paladin"},
                        new[] { "Ranger", "Ranger"},
                        new[] { "Rogue", "Rogue"},
                        new[] { "Sorcerer", "Sorcerer"},
                        new[] { "Wizard", "Wizard"}
                    });
                    dialog.Show(true);
                    break;
                default:
                    var warning = new MogwaiDialog("NotImplemented", $"DoAdventureAction {actionStr}!", 40, 6);
                    warning.AddButton("ok");
                    warning.Button.Click += (btn, args) =>
                    {
                        warning.Hide();
                    };
                    warning.Show(true);
                    break;
            }

        }

        private void DoClassLevel(string classTypeStr)
        {
            if (!_mogwai.CanLevelClass(out _))
            {
                LogInConsole("Mogwai can\'t class levelno level ups!");
                return;
            }

            if (_mogwai.Classes.Count >= 2)
            {
                LogInConsole("Mogwais of this generation can\'t level more then two classes!");
                return;
            }

            if (!Enum.TryParse<ClassType>(classTypeStr, true, out var classType))
            {
                LogInConsole($"Invalid class, please check {classTypeStr}!");
                return;
            }

            if (!_controller.Interaction(new LevelingAction(LevelingType.Class, classType, _mogwai.CurrentLevel,
                _mogwai.GetClassLevel(classType))))
            {
                LogInConsole("Failed to class leveling!");
                return;
            }

            LogInConsole("Successful sent mogwai out for class leveling.");
        }

        public void Evolve(bool fast = false)
        {
            if (_custom.IsStarted())
            {
                _custom.Stop();
            }

            if (!fast)
            {
                if (_mogwai.Evolve(out _))
                {
                    if (_mogwai.Adventure != null)
                    {
                        _custom.Start(_mogwai.Adventure);
                    }
                    else
                    {
                        
                    }
                    UpdateLog();
                }
            }
            else if (_mogwai.PeekNextShift != null)
            {
                _log.Reset();
                for (var i = 0; i < 500; i++)
                {
                    if (_mogwai.PeekNextShift != null && _mogwai.PeekNextShift.IsSmallShift && _mogwai.Evolve(out _))
                    {
                        UpdateLog();
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void UpdateLog()
        {
            foreach (var entry in _mogwai.CurrentShift.History.LogEntries)
            {
                _log.MainCursor.Print(entry.ToString());
                _log.MainCursor.NewLine();
            }
        }

        public void PushLog(LogEntry logEntry)
        {
            _log.MainCursor.Print(logEntry.ToString());
            _log.MainCursor.NewLine();
        }

        internal SadGuiState GetState()
        {
            return State;
        }


        public override bool ProcessKeyboard(Keyboard state)
        {
            if (state.IsKeyReleased(Keys.Enter))
            {
                State = SadGuiState.Selection;
                return true;
            }

            if (state.IsKeyReleased(Keys.Q))
            {
                _glyphIndex++;
                Print(_glyphX, _glyphY, $"[c:sg {_glyphIndex}:1] ", Color.DarkCyan);
                Print(_glyphX + 2, _glyphY, $"{_glyphIndex}", Color.Yellow);
                return true;
            }

            if (state.IsKeyReleased(Keys.A))
            {
                _glyphIndex--;
                Print(_glyphX, _glyphY, $"[c:sg {_glyphIndex}:1] ", Color.DarkCyan);
                Print(_glyphX + 2, _glyphY, $"{_glyphIndex}", Color.Yellow);
                return true;
            }

            if (state.IsKeyReleased(Keys.Right))
            {
                Print(_glyphX, _glyphY, $"[c:sg {_oldglyphIndex}:1] ", Color.DarkCyan);
                _glyphX++;
                _oldglyphIndex = GetGlyph(_glyphX, _glyphY);
                Print(_glyphX, _glyphY, $"[c:sg {_glyphIndex}:1] ", Color.DarkCyan);
                return true;
            }

            if (state.IsKeyReleased(Keys.Left))
            {
                Print(_glyphX, _glyphY, $"[c:sg {_oldglyphIndex}:1] ", Color.DarkCyan);
                _glyphX--;
                _oldglyphIndex = GetGlyph(_glyphX, _glyphY);
                Print(_glyphX, _glyphY, $"[c:sg {_glyphIndex}:1] ", Color.DarkCyan);
                return true;
            }

            if (state.IsKeyReleased(Keys.Up))
            {
                Print(_glyphX, _glyphY, $"[c:sg {_oldglyphIndex}:1] ", Color.DarkCyan);
                _glyphY--;
                _oldglyphIndex = GetGlyph(_glyphX, _glyphY);
                Print(_glyphX, _glyphY, $"[c:sg {_glyphIndex}:1] ", Color.DarkCyan);
                return true;
            }

            if (state.IsKeyReleased(Keys.Down))
            {
                Print(_glyphX, _glyphY, $"[c:sg {_oldglyphIndex}:1] ", Color.DarkCyan);
                _glyphY++;
                _oldglyphIndex = GetGlyph(_glyphX, _glyphY);
                Print(_glyphX, _glyphY, $"[c:sg {_glyphIndex}:1] ", Color.DarkCyan);
                return true;
            }
            Print(0, 0, $"x:{_glyphX} y:{_glyphY} ind:{_glyphIndex}", Color.Cyan);
            return false;
        }

        public override void Update(TimeSpan delta)
        {
            if (IsVisible)
            {
                if (_custom.Adventure != null)
                {
                    _custom.UpdateGame();
                }
            }

            base.Update(delta);
        }

        public void LogInConsole(string msg)
        {
            _log.MainCursor.Print(msg);
            _log.MainCursor.NewLine();
        }
    }
}
