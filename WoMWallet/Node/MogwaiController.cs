﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using WoMFramework.Game.Enums;
using WoMFramework.Game.Interaction;
using WoMFramework.Game.Model.Mogwai;
using WoMFramework.Tool;
using WoMWallet.Tool;

namespace WoMWallet.Node
{
    public class MogwaiController
    {
        private MogwaiWallet Wallet { get; }

        public bool IsWalletUnlocked => Wallet.IsUnlocked;

        public bool IsWalletCreated => Wallet.IsCreated;

        public Block.Block WalletLastBlock => Wallet.LastBlock;

        public string DepositAddress => Wallet.IsUnlocked ? Wallet.Deposit.Address : string.Empty;

        public Dictionary<string, MogwaiKeys> MogwaiKeysDict => Wallet.MogwaiKeyDict;

        public List<MogwaiKeys> MogwaiKeysList => Wallet.MogwaiKeyDict.Values.ToList();

        public List<MogwaiKeys> TaggedMogwaiKeys { get; set; }

        public int CurrentMogwaiKeysIndex { get; set; }

        public Mogwai CurrentMogwai => CurrentMogwaiKeys?.Mogwai;

        public MogwaiKeys CurrentMogwaiKeys
        {
            get
            {
                if (Wallet.MogwaiKeyDict.Count > CurrentMogwaiKeysIndex)
                {
                    return MogwaiKeysList[CurrentMogwaiKeysIndex];
                }
                return null;
            }
        }

        public string WalletMnemonicWords => Wallet.MnemonicWords;

        public bool HasMogwayKeys => MogwaiKeysDict.Count > 0;

        private Timer _timer;

        public MogwaiController()
        {
            Wallet = new MogwaiWallet();
            TaggedMogwaiKeys = new List<MogwaiKeys>();
            CurrentMogwaiKeysIndex = 0;
        }

        public void RefreshCurrent(int minutes)
        {
            Update();
            _timer?.Close();
            _timer = new Timer(minutes * 60 * 1000);
            _timer.Elapsed += OnTimedRefreshCurrent;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        private async void OnTimedRefreshCurrent(object sender, ElapsedEventArgs e)
        {
            await Blockchain.Instance.CacheBlockhashesAsyncNoProgressAsync();
            Update(false);
        }

        public void RefreshAll(int minutes)
        {
            Update();
            _timer?.Close();
            _timer = new Timer(minutes * 60 * 1000);
            _timer.Elapsed += OnTimedRefreshAll;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        private async void OnTimedRefreshAll(object sender, ElapsedEventArgs e)
        {
            await Blockchain.Instance.CacheBlockhashesAsyncNoProgressAsync();
            Update();
        }

        private const int _parallelRestCalls = 3;
        public async void Refresher()
        {
            await Task.Run(() =>
            {
                while (!MogwaiKeysUpdateQueue.IsEmpty)
                {
                    List<MogwaiKeys> mogwaiKeysList = new List<MogwaiKeys>();
                    for (int i = 0; i < _parallelRestCalls; i++)
                    {
                        if (MogwaiKeysUpdateQueue.TryDequeue(out var nextMogwaiKeys))
                        {
                            mogwaiKeysList.Add(nextMogwaiKeys);
                        }
                    }
                    // wait untill all are finished
                    Task.WaitAll(mogwaiKeysList.Select(p => p.Update()).ToArray());
                }
            });
        }

        public int QueueSize => MogwaiKeysUpdateQueue.Count;
        private ConcurrentQueue<MogwaiKeys> MogwaiKeysUpdateQueue = new ConcurrentQueue<MogwaiKeys>();
        private void Update(bool all = true)
        {
            if (MogwaiKeysUpdateQueue.IsEmpty)
            {
                if (all)
                {
                    // deposit address
                    MogwaiKeysUpdateQueue.Enqueue(Wallet.Deposit);

                    // add mogwaikeys
                    foreach (var mogwaiKey in Wallet.MogwaiKeyDict.Values)
                    {
                        if (!mogwaiKey.IsUnwatched)
                        {
                            MogwaiKeysUpdateQueue.Enqueue(mogwaiKey);
                        }
                    }
                }
                else
                {
                    MogwaiKeysUpdateQueue.Enqueue(CurrentMogwaiKeys);
                }

                Refresher();
            }

            // update wallet
            Wallet.Update();
        }

        public void Next()
        {
            if (CurrentMogwaiKeysIndex + 1 < Wallet.MogwaiKeyDict.Count)
            {
                CurrentMogwaiKeysIndex++;
            }
        }

        public void Previous()
        {
            if (CurrentMogwaiKeysIndex > 0)
            {
                CurrentMogwaiKeysIndex--;
            }
        }

        public void Tag()
        {
            if (TaggedMogwaiKeys.Contains(CurrentMogwaiKeys))
            {
                TaggedMogwaiKeys.Remove(CurrentMogwaiKeys);
            }
            else
            {
                TaggedMogwaiKeys.Add(CurrentMogwaiKeys);
            }
        }

        public void ClearTag()
        {
            TaggedMogwaiKeys.Clear();
        }

        public void CreateWallet(string password)
        {
            Wallet.Create(password);
        }

        public void UnlockWallet(string password)
        {
            Wallet.Unlock(password);
        }

        public decimal GetDepositFunds()
        {
            if (!IsWalletUnlocked)
            {
                return -1;
            }
            return Wallet.Deposit.Balance;
        }

        public void PrintMogwaiKeys()
        {
            if (!IsWalletUnlocked)
            {
                return;
            }
            Caching.Persist("mogwaikeys.txt", new { Wallet.Deposit.Address, Wallet.MogwaiKeyDict.Keys });
        }

        public void NewMogwaiKeys()
        {
            if (!IsWalletUnlocked)
            {
                return;
            }
            Wallet.GetNewMogwaiKey(out _);
        }

        public bool SendMog(int amount)
        {
            if (!IsWalletUnlocked)
            {
                return false;
            }

            var mogwaiKeysList = TaggedMogwaiKeys.Count > 0 ? TaggedMogwaiKeys : new List<MogwaiKeys> { CurrentMogwaiKeys };
            if (!Blockchain.Instance.SendMogs(Wallet.Deposit, mogwaiKeysList.Select(p => p.Address).ToArray(), amount, 0.0001m, out _))
            {
                return false;
            }

            mogwaiKeysList.ForEach(p => p.MogwaiKeysState = MogwaiKeysState.Wait);
            return true;
        }

        public bool BindMogwai()
        {
            if (!IsWalletUnlocked)
            {
                return false;
            }

            if (!Blockchain.Instance.BindMogwai(CurrentMogwaiKeys, out _))
            {
                return false;
            }

            CurrentMogwaiKeys.MogwaiKeysState = MogwaiKeysState.Create;
            return true;
        }

        public async Task EvolveMogwai()
        {
            await Task.Run(() =>
            {
                if (CurrentMogwai == null)
                {
                    return;
                }

                CurrentMogwaiKeys.IsLocked = true;

                var mogwai = CurrentMogwai;
                while (mogwai.Evolve())
                {
                    while (mogwai.CanEvolveAdventure)
                    {
                        mogwai.EvolveAdventure();
                    }
                }

                CurrentMogwaiKeys.IsLocked = false;
            });

        }

        public bool Interaction(Interaction interaction)
        {
            if (!IsWalletUnlocked)
            {
                return false;
            }

            if (!Blockchain.Instance.Interaction(CurrentMogwaiKeys, interaction, out var txId))
            {
                return false;
            }

            CurrentMogwaiKeys.InteractionLock.Add(txId, interaction);
            return true;
        }

        public void WatchToggle()
        {
            if (!IsWalletUnlocked)
            {
                return;
            }

            var mogwaiKeysList = TaggedMogwaiKeys.Count > 0 ? TaggedMogwaiKeys : new List<MogwaiKeys> { CurrentMogwaiKeys };
            Wallet.Unwatch(mogwaiKeysList, !CurrentMogwaiKeys.IsUnwatched);
        }

        public MogwaiKeys TestMogwaiKeys()
        {
            //return GetKeysFor("M9whXm2mjrhySAH6D81Lk5mthw1mUEZpvi", "MGJ9VoguyKeRTYmpURgztife4N7PnuDH6u");

            var lvlBarbarian = new LevelingAction(LevelingType.Class, ClassType.Barbarian, 0, 1);
            var lvlCleric = new LevelingAction(LevelingType.Class, ClassType.Cleric, 0, 1);
            var dungAction = new AdventureAction(AdventureType.Dungeon, DifficultyType.Easy, 2);
            var healAction = new SpecialAction(SpecialType.Heal, SpecialSubType.None, CostType.Medium);
            var revivingAction = new SpecialAction(SpecialType.Reviving, SpecialSubType.None, CostType.High);

            var pubMogAddressHex =
                HexHashUtil.ByteArrayToString(Base58Encoding.Decode("MJHYMxu2kyR1Bi4pYwktbeCM7yjZyVxt2i"));
            int blockHeight = 84659;
            int index = 0;
            var shifts = new Dictionary<long, Shift>
            {
                {
                    blockHeight, new Shift(index++, 1530914381, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "00000000090d6c6b058227bb61ca2915a84998703d4444cc2641e6a0da4ba37e",
                        2, "163d2e383c77765232be1d9ed5e06749a814de49b4c0a8aebf324c0e9e2fd1cf",
                        1.00m,
                        0.0001m)
                },
                {
                    blockHeight, new Shift(index++, 1530914381, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "00000000090d6c6b058227bb61ca2915a84998703d4444cc2641e6a0da4ba37e",
                        2, "163d2e383c77765232be1d9ed5e06749a814de49b4c0a8aebf324c0e9e2fd1cf",
                        lvlCleric.GetValue1(),
                        lvlCleric.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163de3671ba28b41ecab6f5d1cf9bb43174cc97bf2164d2e39")
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163de3671ba28b41ecab6f5d1cf9bb43174cc97bf2164d2e39")
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163de3671ba28b41ecab6f5d1cf9bb43174cc97bf2164d2e39")
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163de3671ba28b41ecab6f5d1cf9bb43174cc97bf2164d2e39")
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163de3671ba28b41ecab6f5d1cf9bb43174cc97bf2164d2e39")
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163de3671ba28b41ecab6f5d1cf9bb43174cc97bf2164d2e39")
                },
                {
                    blockHeight, new Shift(index++, 1539810141, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
                        1, "cbcd39553f9727c434343222f1bd8025608edcfbcfc262c0c2afe9fc3f0bcb29",
                        lvlBarbarian.GetValue1(),
                        lvlBarbarian.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, 1539815141, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
                        1, "bbcd39553a9727c434343242f9bd8025608edcfbcfc262c0c2afe9fc3f0bff29",
                        healAction.GetValue1(),
                        healAction.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, 1540417599, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "00000000b552e9c7c51ed793f87c51b2cc945fbb222efd6cec17666a5ecd18a5",
                        1, "cb03db5029f426ed517fdfffd6a90c99a3111f2254f41f9a8f56320076b1f0e3",
                        dungAction.GetValue1(),
                        dungAction.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163de3671ba28b41ecab6f547219bb43174cc97bf2164d2e39")
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163dc3671ba28b41ecab6f547219bb43174cc97bf2164d2e30")
                },
                {
                    blockHeight, new Shift(index++, 1555309745, "32ab20cfbef0ccddfe5c79e726f5fc48b151106f196f7ccb71",
                        blockHeight++, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
                        1, "bbcd39553a9727c434343242f9bd8025608edcfbcfc262c0c2afe9fc3f0bdf29",
                        revivingAction.GetValue1(),
                        revivingAction.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, 1539815141, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
                        1, "bbcd39553a9727c434343242f9bd8025608edcfbcfc262c0c2afe9fc3f0bff29",
                        healAction.GetValue1(),
                        healAction.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, 1539810141, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
                        1, "cbcd39553f9727c434343222f1bd8025608edcfbcfc262c0c2afe9fc3f0bcb29",
                        lvlCleric.GetValue1(),
                        lvlCleric.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, 1540985507, "32ab20cfbef0ccddfe5c79e726f5fc48b151106f196f7ccb71",
                        blockHeight++, "00000000b2204f48bb8d48542a6ada13bc86dde4d2909563fdb8f46389d1a1d2",
                        1, "01d8c163c4f034f7d1f8d7e94fc9fc9a30bbef1aa41c2e03dab29787588c60c4",
                        dungAction.GetValue1(),
                        dungAction.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163db3671ba28b41ecab6f547219bb43174cc97bf2164d2e31")
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163db3671ba28b41ecab6f547219bb43174cc97bf2164d2e31")
                },
                {
                    blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000033dbfc163db3671ba28b41ecab6f547219bb43174cc97bf2164d2e31")
                },
                {
                    blockHeight, new Shift(index++, 1555309745, "32ab20cfbef0ccddfe5c79e726f5fc48b151106f196f7ccb71",
                        blockHeight++, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
                        1, "bbcd39553a9727c434343242f9bd8025608edcfbcfc262c0c2afe9fc3f0bdf29",
                        revivingAction.GetValue1(),
                        revivingAction.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, 1556309745, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
                        1, "bbcd39553a9727c434343242f9bd8025608edcfbcfc262c0c2afe9fc3f0bff29",
                        healAction.GetValue1(),
                        healAction.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, 1557309745, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
                        1, "cbcd39553f9727c434343222f1bd8025608edcfbcfc262c0c2afe9fc3f0bcb29",
                        lvlCleric.GetValue1(),
                        lvlCleric.GetValue2())
                },
                {
                    blockHeight, new Shift(index++, 1541350482, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                        blockHeight++, "00000000341eea3f27e4ddaf72253cd6a35f54ece70fdeee0906a663f8403a60",
                        1, "302e37f0c69b1b037bc178fce6f8493a76a4c844df5391a0a46e81fcad8657c8",
                        0.01042002m,
                        0.00019003m)
                },
            };

            for (int i = 0; i < 30; i++)
            {
                shifts.Add(blockHeight, new Shift(index++, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                    blockHeight++, "0000000033dbfc163de3671ba28b41ecab6f5d1cf9bb43174cc97bf2164d2e39"));

            }
            for (int i = 0; i < 10; i++)
            {
                shifts.Add(blockHeight, new Shift(index++, 1530914381, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                    blockHeight++, "00000000090d6c6b058227bb61ca2915a84998703d4444cc2641e6a0da4ba37e",
                    2, "163d2e383c77765232be1d9ed5e06749a814de49b4c0a8aebf324c0e9e2fd1cf",
                    lvlBarbarian.GetValue1(),
                    lvlBarbarian.GetValue2()));
            }

            var dungActionNext = new AdventureAction(AdventureType.Dungeon, DifficultyType.Easy, 8);

            shifts.Add(blockHeight, new Shift(index++, 1540417599, "328e6077135a1012eae0c92dc624d1cbc02c69d45200e5f72c",
                    blockHeight++, "00000000b552e9c7c51ed793f87c51b2cc945fbb222efd6cec17666a5ecd18a5",
                    1, "cb03db5029f426ed517fdfffd6a90c99a3111f2254f41f9a8f56320076b1f0e3",
                dungActionNext.GetValue1(),
                dungActionNext.GetValue2()));


            //    var shifts = new Dictionary<long, Shift>
            //    {
            //        {
            //            1001, new Shift(0, 1530914381, pubMogAddressHex,
            //                1001, "00000000090d6c6b058227bb61ca2915a84998703d4444cc2641e6a0da4ba37e",
            //                2, "163d2e383c77765232be1d9ed5e06749a814de49b4c0a8aebf324c0e9e2fd1cf",
            //                1.00m,
            //                0.0001m)
            //        },
            //        {
            //            1002, new Shift(1, 1535295740, pubMogAddressHex,
            //                1002, "0000000033dbfc3cc9f3671ba28b41ecab6f547219bb43174cc97bf23269fa88",
            //                1, "db5639553f9727c42f80c22311bd8025608edcfbcfc262c0c2afe9fc3f0bcb29",
            //                0.01040003m,
            //                0.00001002m)
            //        },
            //        {
            //            1003, new Shift(2, pubMogAddressHex,
            //                1003, "0000000033dbfc3cc9f3671ba28b41ecab6f547219bb43174cc97bf2163d2e38")
            //        },
            //        {
            //            1004, new Shift(3, pubMogAddressHex,
            //                1004, "0000000033dbfc163df3671ba28b41ecab6f547219bb43174cc97bf2164d2e38")
            //        },
            //        {
            //            1005, new Shift(4, pubMogAddressHex,
            //                1005, "0000000033dbfc163de3671ba28b41ecab6f547219bb43174cc97bf2164d2e38")
            //        },
            //        {
            //            1006, new Shift(5, pubMogAddressHex,
            //                1006, "0000000033dbfc163dc3671ba28b41ecab6f547219bb43174cc97bf2164d2e38")
            //        },
            //        {
            //            1007, new Shift(6, pubMogAddressHex,
            //                1007, "0000000033dbfc163db3671ba28b41ecab6f547219bb43174cc97bf2164d2e38")
            //        },
            //        {
            //            1008, new Shift(7, pubMogAddressHex,
            //                1008, "0000000033dbfc163def671ba28b41ecab6f547219bb43174cc97bf2164d2e38")
            //        },
            //        {
            //            1009, new Shift(8, pubMogAddressHex,
            //                1009, "0000000033dbfc163dff671ba28b41ecab6f547219bb43174cc97bf2164d2e38")
            //        },
            //        {
            //            1010, new Shift(9, 1555295740, pubMogAddressHex,
            //                1010, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
            //                1, "cbcd39553f9727c434343222f1bd8025608edcfbcfc262c0c2afe9fc3f0bcb29",
            //                lvlAction.GetValue1(),
            //                lvlAction.GetValue2())
            //        },
            //        {
            //            1011, new Shift(10, pubMogAddressHex,
            //                1011, "0000000033dbfc163dff671ba28b41ecab6f547219bb43174cc97bf2164d2e38")
            //        },
            //        {
            //            1012, new Shift(11, 1555299745, pubMogAddressHex,
            //                1012, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
            //                1, "bbcd39553a9727c434343242f9bd8025608edcfbcfc262c0c2afe9fc3f0bcf29",
            //                dungAction.GetValue1(),
            //                dungAction.GetValue2())
            //        },
            //        {
            //            1013, new Shift(12, 1555309745, pubMogAddressHex,
            //                1013, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
            //                1, "bbcd39553a9727c434343242f9bd8025608edcfbcfc262c0c2afe9fc3f0bdf29",
            //                revivingAction.GetValue1(),
            //                revivingAction.GetValue2())
            //        },
            //        {
            //            1014, new Shift(13, 1555329745, pubMogAddressHex,
            //                1014, "0000000044db5c3cc943271b324b31ecab6f547219bb43174cc97bf23269fa88",
            //                1, "bbcd39553a9727c434343242f9bd8025608edcfbcfc262c0c2afe9fc3f0bff29",
            //                healAction.GetValue1(),
            //                healAction.GetValue2())
            //        },
            //        {
            //            1015, new Shift(14, 1539810141, "328b742623b139b29553d0c2cc2c785ed8feff324a37a6bc41",
            //                75422, "000000004eb637ffbdb8674dbe5ff5c339a50b0dac1723f1df44b7b790746a6e",
            //                1, "7876fde772cae5684ad133a007c92f5f209cc91fdc1814fe3dab2c6174557ea7",
            //                0.01042002m,
            //                0.00020002m)
            //        }
            //};

            var mogwai = new Mogwai("MJHYMxu2kyR1Bi4pYwktbeCM7yjZyVxt2i", shifts);

            return new MogwaiKeys
            {
                Mogwai = mogwai,
                Balance = 2.1234m,
                IsUnwatched = false,
                LastUpdated = DateTime.Now,
                MogwaiKeysState = MogwaiKeysState.Bound,
                Shifts = shifts

            };
        }

        private MogwaiKeys GetKeysFor(string key, string mirrorKey)
        {

            var shifts = Blockchain.Instance.GetShifts(mirrorKey);

            var mogwai = new Mogwai(key, shifts);

            return new MogwaiKeys
            {
                Mogwai = mogwai,
                Balance = 2.1234m,
                IsUnwatched = false,
                LastUpdated = DateTime.Now,
                MogwaiKeysState = MogwaiKeysState.Bound,
                Shifts = shifts

            };
        }
    }
}
