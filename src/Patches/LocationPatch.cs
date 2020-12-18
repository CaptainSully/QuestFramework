﻿using Harmony;
using Microsoft.Xna.Framework;
using PurrplingCore.Patching;
using QuestFramework.Framework;
using QuestFramework.Framework.Hooks;
using StardewValley;

namespace QuestFramework.Patches
{
    class LocationPatch : Patch<LocationPatch>
    {
        public override string Name => nameof(LocationPatch);

        public ConditionManager ConditionManager { get; }

        public LocationPatch(ConditionManager conditionManager)
        {
            this.ConditionManager = conditionManager;
            Instance = this;
        }

        protected override void Apply(HarmonyInstance harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performTouchAction)),
                postfix: new HarmonyMethod(typeof(LocationPatch), nameof(LocationPatch.After_performTouchAction))
            );
        }

        private static void After_performTouchAction(GameLocation __instance, string fullActionString, Vector2 playerStandingPosition)
        {
            var hookObserver = Instance.ConditionManager.Observers["Tile"] as TileHook;

            if (hookObserver != null)
            {
                hookObserver.CurrentLocation = __instance.Name;
                hookObserver.Position = playerStandingPosition;
                hookObserver.TouchAction = fullActionString;
                hookObserver.Observe();
            }
        }
    }
}
