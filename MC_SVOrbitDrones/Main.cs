using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MC_SVOrbitDrones
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        // BepInEx
        public const string pluginGuid = "mc.starvalor.orbitdrones";
        public const string pluginName = "SV Orbit Drones";
        public const string pluginVersion = "0.0.1";

        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));
        }

        [HarmonyPatch(typeof(Weapon), nameof(Weapon.Fire))]
        [HarmonyPrefix]
        private static bool WeaponFire_Pre(Weapon __instance, Transform target, bool ___isDrone, Drone ___drone)
        {
            if (___isDrone && ___drone.owner.IsPlayer() && target.IsPlayer() && __instance.wRef.damageType != DamageType.Repair)
                return false;

            return true;
        }

        [HarmonyPatch(typeof(Drone), "SetActions")]
        [HarmonyPrefix]
        private static void DroneSetActions_Pre(Drone __instance, out bool __state)
        {
            if(__instance.owner.IsPlayer() && __instance.droneType != 1 && __instance.target == GameManager.instance.Player.transform)
            {
                __instance.target = null;
                __state = true;
            }

            __state = false;
        }

        [HarmonyPatch(typeof(Drone), "SetActions")]
        [HarmonyPostfix]
        private static void DroneSetActions_Post(Drone __instance, bool __state)
        {
            if (__instance.owner.IsPlayer() && __instance.ae.active && __instance.target == null && (__state || __instance.returning))
            {
                __instance.target = GameManager.instance.Player.transform;
                __instance.returning = false;
            }
        }
    }
}
