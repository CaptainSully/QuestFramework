﻿using Harmony;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PurrplingCore.Patching;
using QuestFramework.Framework;
using QuestFramework.Framework.Helpers;
using QuestFramework.Offers;
using QuestFramework.Framework.Messages;
using StardewValley;
using System;
using System.Linq;

namespace QuestFramework.Patches
{
    class NPCPatch : Patch<NPCPatch>
    {
        public override string Name => nameof(NPCPatch);

        QuestManager QuestManager { get; }
        QuestOfferManager ScheduleManager { get; }

        public NPCPatch(QuestManager questManager, QuestOfferManager scheduleManager)
        {
            this.QuestManager = questManager;
            this.ScheduleManager = scheduleManager;
            Instance = this;
        }

        private static bool Before_checkAction(NPC __instance, Farmer who, ref bool __result)
        {
            try
            {
                if (Game1.eventUp || __instance.IsInvisible || __instance.isSleeping.Value || !who.CanMove || who.isRidingHorse())
                    return true;

                if (who.ActiveObject != null && who.ActiveObject.canBeGivenAsGift() && !who.isRidingHorse())
                    return true;

                Instance.QuestManager.AdjustQuest(new TalkMessage(who, __instance));
                Instance.Monitor.VerboseLog($"Checking for new quest from NPC `{__instance.Name}`.");

                if (OffersSpecialOrder(__instance, out SpecialOrder specialOrder))
                {
                    if (!__instance.Dialogue.TryGetValue($"order_{specialOrder.questKey.Value}", out string dialogue))
                    {
                        dialogue = specialOrder.GetDescription();
                    }

                    __result = true;
                    Game1.player.team.specialOrders.Add(SpecialOrder.GetSpecialOrder(specialOrder.questKey.Value, specialOrder.generationSeed.Value));
                    Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Farmer.cs.2011"), 2));
                    Game1.drawDialogue(__instance, dialogue);
                    QuestFrameworkMod.Multiplayer.globalChatInfoMessage("AcceptedSpecialOrder", Game1.player.Name, specialOrder.GetName());

                    return false;
                }

                var schedules = Instance.ScheduleManager.GetMatchedOffers<NpcOfferAttributes>("NPC");
                var schedule = schedules.FirstOrDefault();
                var quest = schedule != null ? Instance.QuestManager.Fetch(schedule.QuestName) : null;

                if (quest != null)
                {
                    if (schedule.OfferDetails.NpcName != __instance.Name)
                        return true;

                    if (quest == null || Game1.player.hasQuest(quest.id))
                        return true;

                    if (string.IsNullOrEmpty(schedule.OfferDetails.DialogueText))
                        return true;

                    Game1.drawDialogue(__instance, $"{schedule.OfferDetails.DialogueText}[quest:{schedule.QuestName.Replace('@', ' ')}]");
                    __result = true;

                    Instance.Monitor.Log($"Getting new quest `{quest.GetFullName()}` to quest log from NPC `{__instance.Name}`.");

                    return false;
                }
            }
            catch (Exception e)
            {
                Instance.LogFailure(e, nameof(Instance.Before_checkAction));
            }

            return true;
        }

        private static bool OffersSpecialOrder(NPC npc, out SpecialOrder specialOrder)
        {
            var orders = from order in Game1.player.team.availableSpecialOrders
                         where order.requester.Value == npc.Name
                            && order.orderType.Value == "QF_NPC"
                            && !Utils.IsSpecialOrderAccepted(order.questKey.Value)
                         select order;

            specialOrder = orders.FirstOrDefault();

            return specialOrder != null;
        }

        private static void After_draw(NPC __instance, SpriteBatch b, int ___textAboveHeadTimer, string ___textAboveHead)
        {
            if (!QuestFrameworkMod.Instance.Config.ShowNpcQuestIndicators)
                return;

            if (!__instance.isVillager() || __instance.IsInvisible || __instance.IsEmoting || __instance.isSleeping.Value || Game1.eventUp)
                return;

            if (___textAboveHeadTimer > 0 && ___textAboveHead != null)
                return;

            if (OffersQuest(__instance) || OffersSpecialOrder(__instance, out SpecialOrder _))
            {
                float yOffset = 4f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0), 2);
                b.Draw(Game1.mouseCursors,
                    Game1.GlobalToLocal(
                        Game1.viewport, new Vector2(__instance.Position.X + __instance.Sprite.SpriteWidth * 2, __instance.Position.Y + yOffset - 92)),
                    new Rectangle(395, 497, 3, 8),
                    Color.White, 0f,
                    new Vector2(1f, 4f), 4f + Math.Max(0f, 0.25f - yOffset / 16f),
                    SpriteEffects.None, 0.6f);
            }
        }

        public static void After_hasTemporaryMessageAvailable(NPC __instance, ref bool __result)
        {
            if (OffersQuest(__instance, hintSecret: true))
            {
                __result = true;
            }
        }

        private static bool OffersQuest(NPC __instance, bool hintSecret = false)
        {
            return Instance.ScheduleManager.GetMatchedOffers<NpcOfferAttributes>("NPC")
                .Any(o => o.OfferDetails.NpcName == __instance.Name
                    && !o.OfferDetails.Secret || hintSecret
                    && !Game1.player.hasQuest(Instance.QuestManager.ResolveGameQuestId(o.QuestName))
                    && !string.IsNullOrEmpty(o.OfferDetails.DialogueText));
        }

        protected override void Apply(HarmonyInstance harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
                prefix: new HarmonyMethod(typeof(NPCPatch), nameof(NPCPatch.Before_checkAction))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.drawAboveAlwaysFrontLayer)),
                postfix: new HarmonyMethod(typeof(NPCPatch), nameof(NPCPatch.After_draw))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.hasTemporaryMessageAvailable)),
                postfix: new HarmonyMethod(typeof(NPCPatch), nameof(NPCPatch.After_hasTemporaryMessageAvailable))
            );
        }
    }
}
