﻿using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Database.Tables.World;
    using Game;
    using Packets;
    using Packets.Game.Server;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Structures;

    public class CreatureManager
    {
        private static CreatureManager _instance;
        private static readonly object InstanceLock = new object();
        public const int CreatureLocationUpdateTime = 1500;
        public readonly Dictionary<uint, CreatureType> LoadedCreatureTypes = new Dictionary<uint, CreatureType>();           // list of loaded Creatures
        public readonly Dictionary<uint, Creature> LoadedCreatures = new Dictionary<uint, Creature>();

        public static CreatureManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new CreatureManager();
                    }
                }

                return _instance;
            }
        }

        private CreatureManager()
        {
        }

        // 1 creature to n client's
        public void CellIntroduceCreatureToClients(MapChannel mapChannel, Creature creature, List<MapChannelClient> playerList)
        {
            foreach (var player in playerList)
                CreateCreatureOnClient(player, creature);
        }

        // n creatures to 1 client
        public void CellIntroduceCreaturesToClient(MapChannel mapChannel, MapChannelClient mapClient, List<Creature> creaturList)
        {
            foreach (var creature in creaturList)
                CreateCreatureOnClient(mapClient, creature);
        }

        public Creature CreateCreature(uint dbId, SpawnPool spawnPool)
        {
            // check is creature in database
            if (!LoadedCreatures.ContainsKey(dbId))
            {
                Logger.WriteLog(LogType.Error, $"Creature with dbId={dbId}, isn't in database");
                return null;
            }

            // crate creature
            var creature = new Creature
            {
                Actor = new Actor(),
                AppearanceData = LoadedCreatures[dbId].AppearanceData,
                CreatureType = LoadedCreatures[dbId].CreatureType,
                DbId = LoadedCreatures[dbId].DbId,
                Faction = LoadedCreatures[dbId].Faction,
                Level = LoadedCreatures[dbId].Level,
                MaxHitPoints = LoadedCreatures[dbId].MaxHitPoints,
                NameId = LoadedCreatures[dbId].NameId,
                SpawnPool = spawnPool
            };
            // set creature stats
            var creatureStats = CreatureStatsTable.GetCreatureStats(dbId);
            if (creatureStats != null)
            {
                creature.Actor.Attributes.Add(Attributes.Body, new ActorAttributes(Attributes.Body, creatureStats.Body, creatureStats.Body, creatureStats.Body, 5, 1000));
                creature.Actor.Attributes.Add(Attributes.Mind, new ActorAttributes(Attributes.Mind, creatureStats.Mind, creatureStats.Mind, creatureStats.Mind, 5, 1000));
                creature.Actor.Attributes.Add(Attributes.Spirit, new ActorAttributes(Attributes.Spirit, creatureStats.Spirit, creatureStats.Spirit, creatureStats.Spirit, 5, 1000));
                creature.Actor.Attributes.Add(Attributes.Health, new ActorAttributes(Attributes.Health, creatureStats.Health, creatureStats.Health, creatureStats.Health, 5, 1000));
                creature.Actor.Attributes.Add(Attributes.Chi, new ActorAttributes(Attributes.Chi, 0, 0, 0, 0, 0));
                creature.Actor.Attributes.Add(Attributes.Power, new ActorAttributes(Attributes.Power, 0, 0, 0, 0, 0));
                creature.Actor.Attributes.Add(Attributes.Aware, new ActorAttributes(Attributes.Aware, 0, 0, 0, 0, 0));
                creature.Actor.Attributes.Add(Attributes.Armor, new ActorAttributes(Attributes.Armor, creatureStats.Armor, creatureStats.Armor, creatureStats.Armor, 5, 1000));
                creature.Actor.Attributes.Add(Attributes.Speed, new ActorAttributes(Attributes.Speed, 1, 1, 1, 0, 0));
                creature.Actor.Attributes.Add(Attributes.Regen, new ActorAttributes(Attributes.Regen, 0, 0, 0, 0, 0));
            }

            if (spawnPool != null)
                SpawnPoolManager.Instance.IncreaseAliveCreatureCount(spawnPool);

            return creature;
        }

        public void CreateCreatureOnClient(MapChannelClient mapClient, Creature creature)

        {
            if (creature == null)
                return;

            var entityData = new List<PythonPacket>
            {
                // PhysicalEntity
                new WorldLocationDescriptorPacket(creature.Actor.Position, creature.Actor.Rotation),
                new IsTargetablePacket(true),
                // Creature augmentation
                new CreatureInfoPacket(creature.NameId, false, new List<int>()),    // ToDo add creature flags
                // Actor augmentation
                new AppearanceDataPacket(creature.AppearanceData),
                new LevelPacket(creature.Level),
                new AttributeInfoPacket(creature.Actor.Attributes),
                new TargetCategoryPacket(creature.Faction),
                new UpdateAttributesPacket(creature.Actor.Attributes, 0),
                new IsRunningPacket(false)
        };

            mapClient.Player.Client.SendPacket(5, new CreatePhysicalEntityPacket(creature.Actor.EntityId, creature.CreatureType.ClassId, entityData));

            // NPC augmentation
            if (creature.CreatureType.NpcData != null)
            {
                /*
             * NPCInfo
             * NPCConversationStatus
             * Converse
             * Train
             */
                //UpdateConversationStatus(mapClient.Client, creature);
                mapClient.Player.Client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.Vending, new List<int> { 106 }));
                //mapClient.Player.Client.SendPacket(creature.Actor.EntityId, new NPCInfoPacket(726));
                //mapClient.Player.Client.SendPacket(creature.Actor.EntityId, new ConversePacket());
                //mapClient.Player.Client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.Vending, new List<int> { 10 }));
            }
        }

        public Creature FindCreature(uint creatureId)
        {
            return LoadedCreatures[creatureId];
        }

        public void CreatureInit()
        {
            var creatureList = CreatureTable.LoadCreatures();

            foreach (var data in creatureList)
            {
                var appearanceData = CreatureAppearanceTable.GetCreatureAppearance(data.DbId);
                var tempAppearanceData = new Dictionary<EquipmentSlots, AppearanceData>();

                if (appearanceData != null)
                    foreach (var t in appearanceData)
                        tempAppearanceData.Add((EquipmentSlots)t.SlotId, new AppearanceData { SlotId = t.SlotId, ClassId = t.ClassId, Color = new Color(t.Color) });

                var creature = new Creature
                {
                    DbId = data.DbId,
                    CreatureType = LoadedCreatureTypes[data.CreatureType],
                    Faction = data.Faction,
                    Level = data.Level,
                    MaxHitPoints = data.MaxHitPoints,
                    NameId = data.NameId,
                    AppearanceData = tempAppearanceData

                };

                LoadedCreatures.Add(creature.DbId, creature);
            }

            Logger.WriteLog(LogType.Initialize, $"Loaded {LoadedCreatures.Count} Creatures");
        }

        public void CreatureTypesInit()
        {
            var creatureTypeData = CreatureTypesTable.LoadCreatureTypes();

            foreach (var data in creatureTypeData)
            {
                var creatureType = new CreatureType
                {
                    DbId = data.DbId,
                    ClassId = data.ClassId
                };

                /*  ToDo
                if (creatureData.IsAuctioner > 0)
                    creature.CreatureType.AuctionerData = new AuctionerData();
                */

                if (data.IsHarvestable > 0)
                    creatureType.LootData = new CreatureLootData();

                if (data.IsNpc > 0)
                    creatureType.NpcData = new CreatureNpcData();

                if (data.IsVendor > 0)
                    creatureType.VendorData = new CreatureVendorData();

                LoadedCreatureTypes.Add(creatureType.DbId, creatureType);
            }

            Logger.WriteLog(LogType.Initialize, $"Loaded {LoadedCreatureTypes.Count} CreatureTypes");

        }

        public void SetLocation(Creature creature, Position position, Quaternion rotation)
        {
            // set spawnlocation
            creature.Actor.Position = position;
            creature.Actor.Rotation = rotation;
            //allocate pathnodes
            //creature->pathnodes = (baseBehavior_baseNode*)malloc(sizeof(baseBehavior_baseNode));
            //memset(creature->pathnodes, 0x00, sizeof(baseBehavior_baseNode));
            //creature->lastattack = GetTickCount();
            //creature->lastresttime = GetTickCount();
        }

        #region NPC

        public void NpcInit()
        {

        }

        public void RequestNpcConverse(Client client, RequestNPCConversePacket packet)
        {
            var creature = EntityManager.Instance.GetCreature((uint)packet.EntityId);

            if (creature == null)
                return;

            // ToDo create DB structures, and replace constant data with dinamic

            // Greeting = 0
            var greetingId = 19;

            // ForceTopic = 1
            var forceTopicType = new ForceTopic(ConversationType.MissionReward, 429);

            // DispensableMissions = 2
            var missionId = 429;
            var missionLevel = 1;
            var groupType = 1;
            var credits = new List<Curency>
            {
                new Curency(CurencyType.Credits, 200),
                new Curency(CurencyType.Prestige, 100)
            };
            var fixedItems = new List<RewardItem>
            {
                new RewardItem(17131, 27120, 1, new List<int>{900620 }, 2),
                new RewardItem(17131, 27120, 1, new List<int>{900007 }, 2)
            };
            var selectableRewards = new List<RewardItem>
            {
                new RewardItem(28, 3147, 20, new List<int>(), 1),
                new RewardItem(28, 3147, 50, new List<int>(), 1)
            };
            var fixedReward = new FixedReward(credits, fixedItems);
            var selectableReward = new List<RewardItem>(selectableRewards);
            var rewardInfo = new RewardInfo(fixedReward, selectableReward);
            var missionObjectives = new List<MissionObjectives> { new MissionObjectives(4), new MissionObjectives(5) };
            var itemRequired = new List<RewardItem> { new RewardItem(26544) };
            var missionInfo = new MissionInfo(missionLevel, rewardInfo, missionObjectives, itemRequired, groupType);
            var dispensableMissions = new List<DispensableMissions>
            {
                new DispensableMissions(missionId, missionInfo),
                new DispensableMissions(298, missionInfo)
            };

            // CompletableMissions = 3
            var completeableMissions = new List<CompleteableMissions>
            {
                new CompleteableMissions(missionId, rewardInfo),
                new CompleteableMissions(298, rewardInfo)
            };

            // MissionReminder = 4
            var remindableMissions = new List<int> { 298, 429, 430 };

            // ObjectiveAmbient = 5
            var ambientObjectives = new List<AmbientObjectives>
            {
                new AmbientObjectives(missionId, 5, 1),
                new AmbientObjectives(missionId, 4, 1)
            };

            // ObjectiveComplete = 6
            var objectiveComplete = new List<CompleteableObjectives>
            {
                new CompleteableObjectives(missionId, 5, 1),
                new CompleteableObjectives(missionId, 4, 1)
            };

            // RewardableMission = 7 (mission without objectives ???)
            var revardableMissions = new List<RewardableMissions>
            {
                new RewardableMissions(298, rewardInfo),
                new RewardableMissions(missionId, rewardInfo),
            };

            // ObjectiveChoice = 8,
            // EndConversation = 9,
            // Training = 10,
            var training = new TrainingConverse(true, 1);
            // Vending = 11,
            var vendor = new ConvoDataDict
            {
                VendorPackageIds = new List<int> { 106 }
            };
            // ImportantGreering = 12,
            // Clan = 13,
            // Auctioner = 14,
            var auctioneer = new ConvoDataDict
            {
                IsAuctioneer = true
            };
            // ForcedByScript = 15

            var testConvoDataDict = new Dictionary<ConversationType, ConvoDataDict>
            {
                { ConversationType.MissionDispense, new ConvoDataDict(dispensableMissions) },
                //{ ConversationType.ObjectiveComplete, new ConvoDataDict(objectiveComplete) },
                { ConversationType.MissionComplete, new ConvoDataDict(completeableMissions) },
                //{ ConversationType.MissionReward, new ConvoDataDict(revardableMissions) },
                //{ ConversationType.Greeting, new ConvoDataDict(greetingId) },
                //{ ConversationType.MissionReminder, new ConvoDataDict(remindableMissions) },
                //{ ConversationType.ObjectiveAmbient, new ConvoDataDict(ambientObjectives) },
                //{ ConversationType.Training, new ConvoDataDict(training) }
                { ConversationType.Auctioneer, auctioneer },
                { ConversationType.Vending, vendor }
            };

            client.SendPacket(creature.Actor.EntityId, new ConversePacket(testConvoDataDict));
        }

        public void UpdateConversationStatus(Client client, Creature creature)
        {
            var npcData = creature.CreatureType.NpcData;
            var statusSet = false;

            foreach (var entry in npcData.RelatedMissions)
            {
                var missionLogEntry = MissionManager.Instance.FindPlayerMission(client, entry.MissionIndex);
                var mission = MissionManager.Instance.GetById(missionLogEntry.MissionIndex);

                if (missionLogEntry != null)
                {
                    if (mission == null)
                        continue;

                    if (missionLogEntry.State >= mission.StateCount)
                        continue;

                    // search for objective or mission related updates
                    var scriptlineStart = mission.StateMapping[missionLogEntry.State];
                    var scriptlineEnd = mission.StateMapping[missionLogEntry.State + 1];

                    for (var i = scriptlineStart; i < scriptlineEnd; i++)
                    {
                        var scriptline = mission.ScriptLines[i];

                        if (scriptline.Command == MissionScriptCommand.CompleteObjective)
                        {
                            if (creature.CreatureType.DbId == scriptline.Value1) // same NPC?
                            {
                                // objective already completed?
                                if (missionLogEntry.MissionData[scriptline.StorageIndex] == 1)
                                    continue;

                                // send objective completable flag
                                client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.ObjectivComplete, new List<int> { })); // status - complete objective

                                statusSet = true;

                                break;
                            }
                            else if (scriptline.Command == MissionScriptCommand.Collector)
                            {
                                if (creature.CreatureType.DbId == scriptline.Value1) // same NPC?
                                {
                                    // mission already completed?
                                    if (missionLogEntry.State != (mission.StateCount - 1))
                                        continue;

                                    // send mission completable flag
                                    client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.MissionComplete, new List<int> { })); // status - complete objective

                                    statusSet = true;

                                    break;
                                }
                            }
                        }
                    }
                }
                else if (MissionManager.Instance.IsCompletedByPlayer(client, mission.MissionIndex) == false)
                {
                    // check if the npc is actually the mission dispenser and not only a objective related npc
                    if (MissionManager.Instance.IsCreatureMissionDispenser(MissionManager.Instance.GetByIndex(mission.MissionIndex), creature))
                    {
                        // mission available overwrites any other converse state
                        client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.Available, new List<int> { })); // status - available

                        statusSet = true;

                        break;
                    }
                }
            }
            // is NPC vendor?
            if (creature.CreatureType.VendorData != null && statusSet == false)
            {
                // creature->npcData.isVendor
                client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.Available, new List<int> {creature.CreatureType.VendorData.VendorPackageId })); // status - vending

                statusSet = true;
            }
            // no status set yet? Send NONE conversation status
            if (statusSet == false)
            {
                // no other status, set NONE status
                client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.None, null));// status - none

                statusSet = true;
            }
        }
        #endregion

        #region Auctioneer
        public void RequestNPCOpenAuctionHouse(Client client, long entityId)
        {
            client.SendPacket((uint)entityId, new OpenAuctionHousePacket());
        }
        #endregion

        #region Vendor
        public void RequestNPCVending(Client client, RequestNPCVendingPacket packet)
        {
            client.SendPacket((uint)packet.EntityId, new VendPacket());
        }

        public void RequestCancelVendor(Client client, long entityId)
        {
            Logger.WriteLog(LogType.Debug, "ToDo RequestCancelVendor");
        }
        #endregion
    }
}
