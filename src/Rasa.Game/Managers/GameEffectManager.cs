﻿namespace Rasa.Managers
{
    using Data;
    using Database.Tables.Character;
    using Database.Tables.World;
    using Game;
    using Structures;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Packets.Game.Server;

    public class GameEffectManager
    {
        private static GameEffectManager _instance;
        private static readonly object InstanceLock = new object();
        public static GameEffectManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new GameEffectManager();
                    }
                }

                return _instance;
            }
        }

        private GameEffectManager()
        {
        }

        public void AddToList(Actor actor, GameEffect gameEffect)
        {
            // fast and simple prepend list 
            gameEffect.Next = actor.ActiveEffects;
            if (gameEffect.Next != null)
                gameEffect.Next.Previous = gameEffect;
            gameEffect.Previous = null;
            actor.ActiveEffects = gameEffect;
        }

        public void AttachSprint(MapChannel mapChannel, PlayerData player, int effectLevel, int duration)
        {
            mapChannel.CurrentEffectId++; // generate new effectId
            var effectId = mapChannel.CurrentEffectId;
            // effectId -> The id used to identify the effect when sending/receiving effect related data (similiar to entityId, just for effects)
            // typeId -> The id used to lookup the effect class and animation
            // level -> The sub id of the effect, some effects have multiple levels (especially the ones linked with player abilities)
            // create effect struct
            var gameEffect = new GameEffect();
            // setup struct
            gameEffect.Duration = duration; // 5 seconds (test)
            gameEffect.EffectTime = 0; // reset timer
            gameEffect.TypeId = 247;    // EFFECT_TYPE_SPRINT;
            gameEffect.EffectId = effectId;
            gameEffect.EffectLevel = effectLevel;
            // add to list
            AddToList(player.Actor, gameEffect);
            // ToDO send on cell domain
            player.Client.SendPacket(player.Actor.EntityId, new GameEffectAttachedPacket {
                EffectTypeId = gameEffect.TypeId,
                EffectId = gameEffect.EffectId,
                EffectLevel = gameEffect.EffectLevel,
                SourceId = (int)player.Actor.EntityId,
                Announced = true,
                Duration = gameEffect.Duration,
                DamageType = 0,
                AttrId = 1,
                IsActive = true,
                IsBuff = true,
                IsDebuff = false,
                IsNegativeEffect = false
            });
            // do ability specific work
            UpdateMovementMod(mapChannel, player);
        }

        public void UpdateMovementMod(MapChannel mapChannel, PlayerData player)
        {
            var movementMod = 1.0d;
            // check for sprint
            var gameeffect = player.Actor.ActiveEffects;
            while (gameeffect != null)
            {
                if (gameeffect.TypeId == 247) // ToDO curently hardcoded EFFECT_TYPE_SPRINT
                {
                    // apply sprint bonus
                    movementMod += 0.10d;
                    movementMod += gameeffect.EffectLevel * 0.10d;
                    break;
                }
                // next
                gameeffect = gameeffect.Next;
            }
            // todo: other modificators?
            // ToDO send on cell domain
            player.Client.SendPacket(player.Actor.EntityId, new MovementModChangePacket( movementMod) );
        }
    }
}