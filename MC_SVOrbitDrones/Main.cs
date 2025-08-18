using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        private const int HP_REGEN_TIME = 3;
        private const float HP_REGEN_PERC = 0.05f;
        private static Dictionary<Drone, float> hpRegenTimers = new Dictionary<Drone, float>();

        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Main));
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
            if(__instance.droneType != 1 && __instance.target == GameManager.instance.Player.transform && __instance.owner != null && __instance.owner.IsPlayer())
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
            if (__instance.ae.active && __instance.target == null && (__state || __instance.returning) && __instance.owner != null && __instance.owner.IsPlayer())
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

                if (hpRegenTimers[__instance] >= HP_REGEN_TIME)
                {
                    __instance.currHP += (__instance.baseHP * (HP_REGEN_PERC * (hpRegenTimers[__instance] / HP_REGEN_TIME)));
                    if(__instance.currHP >= __instance.baseHP)
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
