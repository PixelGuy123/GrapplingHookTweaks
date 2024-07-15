using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrapplingHookTweaks.Plugin
{
	[BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
	public class BasePlugin : BaseUnityPlugin
	{
		private void Awake()
		{
			Harmony h = new(ModInfo.PLUGIN_GUID);
			h.PatchAll();
		}

		public static bool disableWindowBreakFeature = false;
		public static bool disableDoorOpeningFeature = false;
	}

	static class ModInfo
	{
		public const string PLUGIN_GUID = "pixelguy.pixelmodding.baldiplus.grapplinghooktweaks";

		public const string PLUGIN_NAME = "Grappling Hook Tweaks";

		public const string PLUGIN_VERSION = "1.0.1";
	}

	// *********** Grappling Hook Patches ***********

	[HarmonyPatch(typeof(ITM_GrapplingHook))]
	public class GrapplingHookPatch
	{

		[HarmonyPatch("OnEntityMoveCollision")]
		[HarmonyPrefix]
		private static bool CheckForPassableObjects(ITM_GrapplingHook __instance, PlayerManager ___pm, ref RaycastHit hit, LayerMaskObject ___layerMask, bool ___locked, float ___speed, EnvironmentController ___ec) // anything in here should make the grappling hook pass so it doesn't lock in it
		{

			var comp = __instance.GetComponent<GrapplingHookExtraComponent>();
			if (!BasePlugin.disableWindowBreakFeature)
			{
				if (!___locked && ___layerMask.Contains(hit.collider.gameObject.layer) && hit.transform.parent.CompareTag("Window") && !comp.interactedTransforms.Contains(hit.transform.parent))
				{ // Window stuff
					var w = hit.transform.parent.GetComponent<Window>();
					if (w != null)
					{
						w.Break(true);
						comp.interactedTransforms.Add(hit.transform.parent);
						OnWindowBreak?.Invoke(___pm, w);

						__instance.transform.position += __instance.transform.forward * ___speed * ___ec.EnvironmentTimeScale;
						return false;
					}
				}
			}

			if (!BasePlugin.disableDoorOpeningFeature)
			{
				var clickable = hit.transform.parent.GetComponent<IClickable<int>>(); // IClickable stuff
				clickable ??= hit.transform.GetComponent<IClickable<int>>(); // If clickable is on the obj itself


				if (clickable != null && !nonAllowedClickables.Contains(clickable.GetType()) && !comp.usedClickables.Contains(clickable) && !clickable.ClickableHidden()
					&& (!clickable.ClickableRequiresNormalHeight() || (clickable.ClickableRequiresNormalHeight() && !___pm.plm.Entity.Squished)))
				{
					clickable.Clicked(___pm.playerNumber);
					comp.usedClickables.Add(clickable);
					if (!hit.collider.isTrigger)
						__instance.transform.position += __instance.transform.forward * ___speed * ___ec.EnvironmentTimeScale;

					return false;
				}
			}

			return true;
		}

		public static event WindowBreakHandler OnWindowBreak; // In case mods need to do something with this (like Times, with school property rule)

		public delegate void WindowBreakHandler(PlayerManager pm, Window window);

		readonly static HashSet<Type> nonAllowedClickables = [typeof(WaterFountain), typeof(Pickup), typeof(MathMachine), typeof(HideableLocker)];


		[HarmonyPatch("Use")]
		[HarmonyPrefix]
		private static void AddComponentThere(ITM_GrapplingHook __instance) =>
			__instance.gameObject.AddComponent<GrapplingHookExtraComponent>();

		public static void AddNonAllowedClickable(Type t) => nonAllowedClickables.Add(t); // Useful for mods
	}

	class GrapplingHookExtraComponent : MonoBehaviour
	{
		readonly public List<IClickable<int>> usedClickables = []; // Just to not repeat the same clickable
		readonly public List<Transform> interactedTransforms = [];
	}

	// ********** NPC Patches ************

	[HarmonyPatch(typeof(NPC), "EntityTriggerEnter")]
	internal class GotHitByGrapplingHook
	{
		private static void Prefix(NPC __instance, Collider other)
		{
			if (other.CompareTag("GrapplingHook"))
				__instance.Navigator.Entity.AddForce(new(other.transform.right, 15f, -12f));
		}
	}

	// ******** Gum Patch *******
	[HarmonyPatch(typeof(Gum), "EntityTriggerEnter")]
	internal class GumGotHitByGrapplingHook
	{
		private static bool Prefix(Gum __instance, Collider other, bool ___flying, AudioManager ___audMan, SoundObject ___audSplat, Beans ___beans)
		{
			if (___flying && other.CompareTag("GrapplingHook"))
			{
				__instance.Hide();
				___beans.GumHit(__instance, false);
				___audMan.FlushQueue(true);
				___audMan.PlaySingle(___audSplat);
				return false;
			}
			return true;
		}
	}
}
