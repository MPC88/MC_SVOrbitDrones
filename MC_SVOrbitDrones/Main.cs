using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace MC_SVOrbitDrones
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        // BepInEx
        public const string pluginGuid = "mc.starvalor.orbitdrones";
        public const string pluginName = "SV Orbit Drones";
        public const string pluginVersion = "0.0.3";

        // Mod
        public static ConfigEntry<int> cfgHPRegenTime;
        public static ConfigEntry<int> cfgHPRegenPercent;
        private static Dictionary<Drone, float> hpRegenTimers = new Dictionary<Drone, float>();

        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));

            cfgHPRegenTime = Config.Bind<int>(
                "Drone regen",
                "Tick rate",
                3,
                "Number of seconds before each % heal tick.");

            cfgHPRegenPercent = Config.Bind<int>(
                "Drone regen",
                "Percent per tick",
                5,
                "% healed per tick");
        }

        #region orbit
        [HarmonyPatch(typeof(Weapon), nameof(Weapon.Fire))]
        [HarmonyPrefix]
        private static bool WeaponFire_Pre(Weapon __instance, Transform target, bool ___isDrone, Drone ___drone)
        {
            if (___isDrone && ___drone != null && ___drone.owner != null && __instance.wRef.damageType != DamageType.Repair && ___drone.owner.IsPlayer() && target.IsPlayer())
                return false;

            return true;
        }

        [HarmonyPatch(typeof(Drone), "SetActions")]
        [HarmonyPrefix]
        private static void DroneSetActions_Pre(Drone __instance, out bool __state)
        {
            if(__instance.droneType != 1 && GameManager.instance.Player != null && __instance.target == GameManager.instance.Player.transform && __instance.owner != null && __instance.owner.IsPlayer())
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
            if (__instance.ae.active && GameManager.instance.Player != null && __instance.target == null && (__state || __instance.returning) && __instance.owner != null && __instance.owner.IsPlayer())
            {
                __instance.target = GameManager.instance.Player.transform;
                __instance.returning = false;
            }
        }
        #endregion

        #region regen
        [HarmonyPatch(typeof(Drone), "Update")]
        [HarmonyPostfix]
        private static void DroneUpdate_Post(Drone __instance)
        {
            if (GameManager.instance.Player == null)
                return;

            if (__instance.droneType != 1 && __instance.target == GameManager.instance.Player.transform && __instance.owner != null && __instance.owner.IsPlayer() && __instance.currHP < __instance.baseHP)
            {
                if(!hpRegenTimers.TryGetValue(__instance, out _))
                    hpRegenTimers.Add(__instance, 0);

                hpRegenTimers[__instance] += Time.deltaTime;
                if (hpRegenTimers[__instance] >= cfgHPRegenTime.Value)
                {
                    float hpRegenPerc = (float)cfgHPRegenPercent.Value / 100;
                    __instance.currHP += (__instance.baseHP * (hpRegenPerc * (hpRegenTimers[__instance] / cfgHPRegenTime.Value)));                    
                    if (__instance.currHP >= __instance.baseHP)
                    {
                        __instance.currHP = __instance.currHP > __instance.baseHP ? __instance.baseHP : __instance.currHP;
                        hpRegenTimers.Remove(__instance);
                    }
                    else
                    {
                        hpRegenTimers[__instance] = 0;
                    }

                    __instance.CallUpdateBar(false);
                }
            }
        }

        [HarmonyPatch(typeof(Drone), nameof(Drone.Dock))]
        [HarmonyPatch(typeof(Drone), nameof(Drone.Die))]
        [HarmonyPrefix]
        private static void DroneDockorDie_Pre(Drone __instance)
        {
            if (__instance.owner != null && __instance.owner.IsPlayer() && hpRegenTimers.TryGetValue(__instance, out _))
                hpRegenTimers.Remove(__instance);
        }
        #endregion

        #region StopPDGunnersFiringAtFriendlyDrones
        [HarmonyPatch(typeof(WeaponTurret), "FindTarget")]
        [HarmonyPrefix]
        private static void WeaponTurretFindTarget_Pre(WeaponTurret __instance, ref List<ScanObject> objs, bool smallObject)
        {
            if (!((SpaceShip)AccessTools.Field(typeof(WeaponTurret), "ss").GetValue(__instance)).ffSys.isPlayer)
                return;

            objs.RemoveAll(obj => obj.GetEntity() as Drone != null && (obj.GetEntity() as Drone).owner != null && (obj.GetEntity() as Drone).owner.IsPlayer());
        }
        #endregion
    }
}
