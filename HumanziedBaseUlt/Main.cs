﻿using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;

namespace HumanziedBaseUlt
{
    class Main : Events
    {
        private readonly AIHeroClient me = ObjectManager.Player;
        // ReSharper disable once FieldCanBeMadeReadOnly.Local       

        public Main()
        {
            Listing.config = MainMenu.AddMenu("HumanizedBaseUlts", "humanizedBaseUlts");
            Listing.config.Add("on", new CheckBox("Enabled"));
            Listing.config.Add("min20", new CheckBox("20 min passed"));
            Listing.config.Add("minDelay", new Slider("Minimum ultimate delay", 1000, 0, 2500));
            Listing.config.AddLabel("The time to let the enemy regenerate health in base");

            Listing.config.AddSeparator(20);
            Listing.config.Add("fountainReg", new Slider("Fountain regeneration speed", 89, 50, 100));
            Listing.config.Add("fountainRegMin20", new Slider("Fountain regeneration speed after minute 20", 364, 350, 400));


            Listing.potionMenu = Listing.config.AddSubMenu("Potions");
            Listing.potionMenu.AddLabel("[Regeneration Speed in HP/Sec.]");
            Listing.potionMenu.Add("healPotionRegVal", new Slider("Heal Potion / Cookie", 10, 5, 20));
            Listing.potionMenu.Add("crystalFlaskRegVal", new Slider("Crystal Flask", 10, 5, 20));
            Listing.potionMenu.Add("crystalFlaskJungleRegVal", new Slider("Crystal Flask Jungle", 9, 5, 20));
            Listing.potionMenu.Add("darkCrystalFlaskVal", new Slider("Dark Crystal Flask", 16, 5, 20));


            Listing.snipeMenu = Listing.config.AddSubMenu("Enemy Recall Snipe");
            Listing.snipeMenu.AddLabel("[Premade feature added, too]");

            Listing.snipeMenu.Add("snipeEnabled", new CheckBox("Enabled"));
            AddStringList(Listing.snipeMenu, "minSnipeHitChance", "Minimum Snipe HitChance", 
                new []{ "Impossible", "Low", "Above Average", "Very High"}, 2);
            Listing.snipeMenu.Add("snipeDraw", new CheckBox("Draw Snipe paths"));
            Listing.snipeMenu.Add("snipeCinemaMode", new CheckBox("Cinematic mode ™"));
            Listing.snipeMenu.Add("snipeAccuracy", new Slider("Accuracy", 10));
            Listing.snipeMenu.AddLabel("Decrease to prevent FPS drops");

            Listing.allyconfig = Listing.config.AddSubMenu("Premades");
            foreach (var ally in EntityManager.Heroes.Allies)
            {
                if (Listing.UltSpellDataList.Any(x => x.Key == ally.ChampionName))
                    Listing.allyconfig.Add(ally.ChampionName + "/Premade", new CheckBox(ally.ChampionName, ally.IsMe));
            }


            Listing.MiscMenu = Listing.config.AddSubMenu("Miscellaneous");
            Listing.MiscMenu.AddLabel("[Draven]");
            Listing.MiscMenu.Add("dravenCastBackBool", new CheckBox("Enable 'Draven Cast Back'"));
            Listing.MiscMenu.Add("dravenCastBackDelay", new Slider("Cast Back X ms earlier", 400, 0, 500));

            Listing.MiscMenu.AddSeparator();
            Listing.MiscMenu.Add("allyMessaging", new CheckBox("Send information to premades"));
            Listing.MiscMenu.AddLabel("If only 1 person uses this addon the other one gets infromed via chat whisper");

            AddStringList(Listing.MiscMenu, "damageCalcMethod", "Damage Calculation Method", 
                new [] {"Elobudddy", "Leaguesharp"}, 1);
            Listing.MiscMenu.AddLabel("Elobuddy doesn't contain masteries atm :(");

            Game.OnUpdate += GameOnOnUpdate;
            Teleport.OnTeleport += TeleportOnOnTeleport;
            OnEnemyInvisible += OnOnEnemyInvisible;
            OnEnemyVisible += OnOnEnemyVisible;
        }

        private void AddStringList(Menu m, string uniqueId, string displayName, string[] values, int defaultValue)
        {
            var mode = m.Add(uniqueId, new Slider(displayName, defaultValue, 0, values.Length - 1));
            mode.DisplayName = displayName + ": " + values[mode.CurrentValue];
            mode.OnValueChange += delegate (ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
            {
                sender.DisplayName = displayName + ": " + values[args.NewValue];
            };
        }

        private void TeleportOnOnTeleport(Obj_AI_Base sender, Teleport.TeleportEventArgs args)
        {
            var invisEnemiesEntry = Listing.invisEnemiesList.FirstOrDefault(x => x.sender == sender);

            switch (args.Status)
            {
                case TeleportStatus.Start:
                    if (invisEnemiesEntry == null)
                        return;
                    if (Listing.teleportingEnemies.All(x => x.Sender != sender))
                    {
                        Listing.teleportingEnemies.Add(new Listing.PortingEnemy
                        {
                            Sender = (AIHeroClient) sender,
                            Duration = args.Duration,
                            StartTick = args.Start,
                        });
                    }
                    break;
                case TeleportStatus.Abort:
                    var teleportingEnemiesEntry = Listing.teleportingEnemies.First(x => x.Sender.Equals(sender));
                    Listing.teleportingEnemies.Remove(teleportingEnemiesEntry);
                    break;

                case TeleportStatus.Finish:
                    var teleportingEnemiesEntry2 = Listing.teleportingEnemies.First(x => x.Sender.Equals(sender));
                    Core.DelayAction(() => Listing.teleportingEnemies.Remove(teleportingEnemiesEntry2), 10000);
                    break;
            }
        }
              
        /*enemy appear*/
        private void OnOnEnemyVisible(AIHeroClient sender)
        {
            Listing.visibleEnemies.Add(sender);
            var invisEntry = Listing.invisEnemiesList.First(x => x.sender.Equals(sender));
            Listing.invisEnemiesList.Remove(invisEntry);

            var sinpeEntry = Listing.Pathing.enemySnipeProcs.FirstOrDefault(x => x.target == sender);
            sinpeEntry.CancelProcess();
            Listing.Pathing.enemySnipeProcs.Remove(sinpeEntry);
        }
        
        /*enemy disappear*/
        private void OnOnEnemyInvisible(InvisibleEventArgs args)
        {
            Listing.visibleEnemies.Remove(args.sender);
            Listing.invisEnemiesList.Add(args);

            if (Listing.snipeMenu["snipeEnabled"].Cast<CheckBox>().CurrentValue && 
                me.Spellbook.GetSpell(SpellSlot.R).IsReady && me.Mana >= 100)
                Listing.Pathing.enemySnipeProcs.Add(new SnipePrediction(args));
        }

        private void GameOnOnUpdate(EventArgs args)
        {
            Listing.config.Get<CheckBox>("min20").CurrentValue = Game.Time > 1455;

            UpdateEnemyVisibility();
            Listing.Pathing.UpdateEnemyPaths();
            CheckRecallingEnemies();
        }

        Vector3 enemySpawn {
            get { return ObjectManager.Get<Obj_SpawnPoint>().First(x => x.IsEnemy).Position; }
        }

        private void CheckRecallingEnemies()
        {
            if (!Listing.config.Get<CheckBox>("on").CurrentValue)
                return;

            foreach (Listing.PortingEnemy portingEnemy in Listing.teleportingEnemies.OrderBy(x => x.Sender.Health))
            {
                var enemy = portingEnemy.Sender;
                InvisibleEventArgs invisEntry = Listing.invisEnemiesList.First(x => x.sender.Equals(enemy));

                int recallEndTime = portingEnemy.StartTick + portingEnemy.Duration;
                float timeLeft = recallEndTime - Core.GameTickCount;
                float travelTime = Algorithm.GetUltTravelTime(me, enemySpawn);

                float regedHealthRecallFinished = Algorithm.SimulateHealthRegen(enemy, invisEntry.StartTime, recallEndTime);
                float totalEnemyHOnRecallEnd = enemy.Health + regedHealthRecallFinished;

                float aioDmg = Damage.GetAioDmg(enemy, timeLeft, enemySpawn);

                float regenerationDelayTime = Algorithm.SimulateRealDelayTime(enemy, recallEndTime, aioDmg);

                if (aioDmg > totalEnemyHOnRecallEnd)
                {
                    if (regenerationDelayTime < Listing.config.Get<Slider>("minDelay").CurrentValue)
                    {
                        Messaging.ProcessInfo(enemy.ChampionName, Messaging.MessagingType.DelayTooSmall, regenerationDelayTime);
                        continue;
                    }

                    CheckUltCast(enemy, timeLeft, travelTime, aioDmg, regenerationDelayTime);
                }
                else if (aioDmg > 0) /*not enough damage at all (maybe not enough time?)*/
                {
                    //dmg there but not enough
                    Messaging.ProcessInfo(enemy.ChampionName, Messaging.MessagingType.NotEnougDamage, aioDmg);
                }
            }
        }

        private void CheckUltCast(AIHeroClient enemy, float timeLeft, float travelTime, float aioDmg, float regenerationDelayTime)
        {
            Messaging.ProcessInfo(enemy.ChampionName, Messaging.MessagingType.DelayInfo, regenerationDelayTime);

            if (travelTime < timeLeft + regenerationDelayTime)
            {
                Vector3 enemyBaseVec =
                    ObjectManager.Get<Obj_SpawnPoint>().First(x => x.IsEnemy).Position;
                float delay = timeLeft + regenerationDelayTime - travelTime;

                Core.DelayAction(() =>
                {
                    if (Listing.teleportingEnemies.All(x => x.Sender != enemy))
                        return;

                    Player.CastSpell(SpellSlot.R, enemyBaseVec);

                    /*Draven*/
                    if (Listing.config.Get<CheckBox>("dravenCastBackBool").CurrentValue)
                    {
                        int castBackReduction = Listing.config.Get<Slider>("dravenCastBackDelay").CurrentValue;
                        if (me.ChampionName == "Draven")
                            Core.DelayAction(() =>
                            {
                                Player.CastSpell(SpellSlot.R);
                            }, (int)(travelTime - castBackReduction));
                    }
                    /*Draven*/
                },
                (int)delay);
                Debug.Init(enemy, Algorithm.GetLastEstimatedEnemyReg(), aioDmg);
            }


            //Cleaning
            AllyMessaging.SendBaseUltInfoToAllies(timeLeft, regenerationDelayTime);
            Listing.teleportingEnemies.RemoveAll(x => x.Sender.ChampionName != enemy.ChampionName);
        }

        private void UpdateEnemyVisibility()
        {
            foreach (var enemy in EntityManager.Heroes.Enemies)
            {
                if (enemy.IsHPBarRendered && !Listing.visibleEnemies.Contains(enemy))
                {
                    FireOnEnemyVisible(enemy);
                }
                else if (!enemy.IsHPBarRendered && Listing.visibleEnemies.Contains(enemy))
                {
                    FireOnEnemyInvisible(new InvisibleEventArgs
                    {
                        StartTime = Core.GameTickCount,
                        sender = enemy,
                        LastRealPath = Listing.Pathing.GetLastEnemyPath(enemy)
                    });
                }
            }
        }
    }
}
