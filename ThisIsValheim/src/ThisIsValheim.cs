using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ThisIsValheim
{
    /// <summary>
    /// Doors are no longer opened. They are kicked. The game already has a kick - the bare handed
    /// secondary attack - and it already knows when that kick connects with something; all this
    /// does is notice when the something was a door, and answer with a bang and a door that bursts
    /// open several times faster than it has any right to.
    ///
    /// Nothing about the door itself changes: it is opened through the game's own UseDoor RPC, so
    /// unmodded clients and the server see an ordinary door swing. Only the noise and the speed
    /// are the mod's, and those ride on a routed RPC of their own that unmodded clients ignore.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public class ThisIsValheimPlugin : BaseUnityPlugin
    {
        public const string GUID = "rauschekuh.thisisvalheim";
        public const string NAME = "This Is Valheim!";
        public const string VERSION = "0.1.0";

        /// <summary>Broadcast to every modded client: the bang and the fast swing, per door.</summary>
        private const string KickRpc = "ThisIsValheim_Kick";

        /// <summary>
        /// How long the door's animator is left running fast. The open animation is well under a
        /// second even at normal speed; the window only has to outlast the trip the UseDoor RPC
        /// makes to the door's owner and back, which is why it is this generous.
        /// </summary>
        private const float SwingWindow = 1.5f;

        /// <summary>
        /// How long a door is deaf to further kicks. One swing of the foot sweeps several rays and
        /// can land on the same door more than once, and the door's state takes a round trip to its
        /// owner to come back - without this the second hit would read the door as still shut and
        /// slam it closed again.
        /// </summary>
        private const float KickCooldown = 0.6f;

        /// <summary>The bang goes off at boot height rather than at the door's foot.</summary>
        private const float SoundHeight = 1f;

        /// <summary>
        /// How long a spawned effect is given before it is cleaned up by hand. Most of the game's
        /// effects carry a TimedDestruction and tidy themselves; the odd one is a bare sound meant
        /// to live on a machine that outlives it, and those would otherwise stay forever.
        /// </summary>
        private const float EffectLifetime = 6f;

        /// <summary>
        /// The default kick: a siege ram's piston landing on a gate, the splintering of a broken
        /// building piece, the sawdust that comes off a chopped log, and the thump on the camera
        /// every weapon hit in the game uses. Nothing here is shipped with the mod.
        /// </summary>
        private const string DefaultEffects =
            "sfx_battering_ram_impact,sfx_wood_break,vfx_SawDust,fx_hit_camshake";

        private static ThisIsValheimPlugin instance;
        private static ZRoutedRpc registeredOn;

        /// <summary>The configured effects, resolved once and reused.</summary>
        private static EffectList effects;
        private static bool resolved;

        /// <summary>One running swing per door, so a second kick does not cut the first one short.</summary>
        private static readonly Dictionary<Door, Coroutine> swinging = new Dictionary<Door, Coroutine>();

        /// <summary>When each door was last kicked - see <see cref="KickCooldown"/>.</summary>
        private static readonly Dictionary<Door, float> kicked = new Dictionary<Door, float>();

        // ---- Config --------------------------------------------------------------------------

        private static ConfigEntry<float> swingSpeed;
        private static ConfigEntry<bool> lockedDoors;
        private static ConfigEntry<string> effectPrefabs;

        void Awake()
        {
            instance = this;

            swingSpeed = Config.Bind("Kick", "SwingSpeed", 4f, new ConfigDescription(
                "How many times faster than normal a kicked door swings open.",
                new AcceptableValueRange<float>(1f, 10f)));
            lockedDoors = Config.Bind("Kick", "LockedDoors", false,
                "Whether a door that wants a key can be kicked open without it. Off keeps the " +
                "crypts shut until you have found the key, the way the game intends.");
            effectPrefabs = Config.Bind("Effects", "Prefabs", DefaultEffects,
                "The game's own effect prefabs that go off at the door, by name, separated by " +
                "commas. Drop one to lose that layer, or put fx_GP_Activation in for the sound a " +
                "Forsaken power makes when you call on it. An empty list makes the kick silent.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), GUID);
        }

        /// <summary>ZNet builds a fresh ZRoutedRpc per session, so re-register once per game.</summary>
        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterRpc
        {
            private static void Postfix()
            {
                ZRoutedRpc rpc = ZRoutedRpc.instance;
                if (rpc == null || rpc == registeredOn)
                {
                    return;
                }
                registeredOn = rpc;
                rpc.Register<ZDOID>(KickRpc, RPC_Kick);
            }
        }

        // ---- Noticing the kick ---------------------------------------------------------------

        /// <summary>
        /// Every object a melee swing touches passes through here, before the game has decided
        /// whether it is something that can be damaged - which is exactly the question "would this
        /// kick hit a door", asked by the game itself with the game's own reach and aim. A door
        /// that cannot be broken, like the ones in the dungeons, still comes through.
        /// </summary>
        [HarmonyPatch(typeof(Attack), "AddHitPoint")]
        private static class NoticeKick
        {
            private static void Postfix(Attack __instance, GameObject go)
            {
                try
                {
                    Player player = Player.m_localPlayer;
                    if (go == null || player == null || __instance.m_character != player
                        || !IsKick(__instance.m_attackAnimation))
                    {
                        return;
                    }
                    Door door = go.GetComponentInParent<Door>();
                    if (door != null)
                    {
                        TryKick(door, player);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ThisIsValheim] kick check failed: " + e);
                }
            }
        }

        /// <summary>
        /// Whether an attack is a kick. The bare handed secondary attack is <c>unarmed_kick</c>;
        /// matching the word rather than the whole name means anything else the game or another
        /// mod calls a kick counts too, and a punch or a sword swing never does.
        /// </summary>
        private static bool IsKick(string animation)
        {
            return !string.IsNullOrEmpty(animation)
                && animation.IndexOf("kick", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---- The kick ------------------------------------------------------------------------

        /// <summary>
        /// Boots the door open, or says it could not. Every reason the game would have refused the
        /// door is checked here too, because the kick opens it through <c>Door.Open</c> directly
        /// and that call asks nothing: UseDoor flips the state for whoever sends it, key or no key.
        /// </summary>
        private static bool TryKick(Door door, Player player)
        {
            ZNetView nview = door.m_nview;
            if (nview == null || !nview.IsValid() || door.m_animator == null || Recent(door))
            {
                return false;
            }
            // Only a shut door gets kicked. A door standing open is closed the ordinary way, and
            // one still swinging is left to finish - CanInteract is the game's own version of that.
            if (nview.GetZDO().GetInt(ZDOVars.s_state) != 0 || !door.CanInteract())
            {
                return false;
            }
            // A ward is a ward, and no kick is hard enough. It is not flashed here: the kick that
            // got us this far is a hit like any other, and the game flashes the ward for that.
            if (door.m_checkGuardStone
                && !PrivateArea.CheckAccess(door.transform.position, 0f, flash: false))
            {
                return false;
            }
            if (door.m_keyItem != null && !lockedDoors.Value)
            {
                return false;
            }

            kicked[door] = Time.time;
            // The show is everyone's; the door itself is opened by the game, through its own RPC,
            // so a client without the mod still sees it swing - just quietly and at walking pace.
            Broadcast(nview.GetZDO().m_uid);
            door.Open((player.transform.position - door.transform.position).normalized);
            if (Game.instance != null)
            {
                Game.instance.IncrementPlayerStat(PlayerStatType.DoorsOpened);
            }
            return true;
        }

        /// <summary>Whether this door has been kicked too recently to be kicked again.</summary>
        private static bool Recent(Door door)
        {
            float now = Time.time;
            float when;
            if (kicked.TryGetValue(door, out when) && now - when < KickCooldown)
            {
                return true;
            }
            // Doors are kicked one at a time; anything still in here from a while ago is a door
            // the player has walked away from, or one the world has unloaded underneath us.
            if (kicked.Count > 8)
            {
                List<Door> stale = new List<Door>();
                foreach (KeyValuePair<Door, float> entry in kicked)
                {
                    if (entry.Key == null || now - entry.Value >= KickCooldown)
                    {
                        stale.Add(entry.Key);
                    }
                }
                foreach (Door old in stale)
                {
                    kicked.Remove(old);
                }
            }
            return false;
        }

        private static void Broadcast(ZDOID door)
        {
            if (ZRoutedRpc.instance != null)
            {
                // Everybody includes us, and InvokeRoutedRPC runs it here and now, so the bang
                // lands on the kicker's own screen in the same frame as the kick.
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, KickRpc, door);
            }
            else
            {
                RPC_Kick(0L, door);
            }
        }

        /// <summary>
        /// The flourish, run the same way on every modded client: the bang at the door and the
        /// door's animator wound up for as long as the swing takes. Purely local to each client -
        /// nothing here is networked, so nobody hears the same kick twice.
        /// </summary>
        private static void RPC_Kick(long sender, ZDOID id)
        {
            // A dedicated server has nobody watching and no door instances to speak of.
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                return;
            }
            try
            {
                if (ZNetScene.instance == null)
                {
                    return;
                }
                GameObject go = ZNetScene.instance.FindInstance(id);
                Door door = go != null ? go.GetComponent<Door>() : null;
                // The door is out of this client's loaded zones - there is nothing to see here.
                if (door == null)
                {
                    return;
                }
                Bang(door.transform.position + Vector3.up * SoundHeight);
                Swing(door);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ThisIsValheim] kick effects failed: " + e);
            }
        }

        // ---- The bang ------------------------------------------------------------------------

        /// <summary>Sets the configured effects off in the door's face.</summary>
        private static void Bang(Vector3 pos)
        {
            EffectList list = Effects();
            if (list == null)
            {
                return;
            }
            // Every client spawns its own copy off the one RPC. m_forceDisableInit is the game's
            // own way of saying "no ZDO for this one" - the ZNetView, if the effect has one,
            // destroys itself in Awake, so this copy never leaves the client that made it.
            bool wasDisabled = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            GameObject[] spawned;
            try
            {
                spawned = list.Create(pos, Quaternion.identity);
            }
            finally
            {
                ZNetView.m_forceDisableInit = wasDisabled;
            }
            Sweep(spawned);
        }

        /// <summary>
        /// Puts a timer on anything that did not come with one. An effect prefab that is normally
        /// a child of some machine has no TimedDestruction of its own, because the machine is what
        /// eventually goes away - spawned loose like this it would sit at the door forever.
        /// </summary>
        private static void Sweep(GameObject[] spawned)
        {
            foreach (GameObject go in spawned)
            {
                if (go != null && go.GetComponentInChildren<TimedDestruction>(includeInactive: true) == null)
                {
                    UnityEngine.Object.Destroy(go, EffectLifetime);
                }
            }
        }

        /// <summary>
        /// The configured effect prefabs, looked up once and kept. Nothing is shipped with the
        /// mod: these are the game's own, so they are whatever this build of the game says they
        /// are, and a name that is not in the game at all simply drops out of the list.
        /// </summary>
        private static EffectList Effects()
        {
            if (resolved)
            {
                return effects;
            }
            if (ZNetScene.instance == null)
            {
                return null; // too early - the next kick tries again
            }
            resolved = true;

            List<string> wanted = new List<string>();
            foreach (string name in effectPrefabs.Value.Split(','))
            {
                string trimmed = name.Trim();
                if (trimmed.Length > 0)
                {
                    wanted.Add(trimmed);
                }
            }
            if (wanted.Count == 0)
            {
                return null; // a deliberately silent kick
            }

            List<EffectList.EffectData> found = new List<EffectList.EffectData>();
            List<string> missing = new List<string>();
            foreach (string name in wanted)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(name);
                if (prefab != null)
                {
                    found.Add(new EffectList.EffectData { m_prefab = prefab, m_enabled = true });
                }
                else
                {
                    missing.Add(name);
                }
            }
            // Only the effects that carry a ZNetView are in ZNetScene; the rest live wherever the
            // prefab that uses them loaded them, so they have to be searched for. That is a walk
            // over every object Unity has in memory, which is why it happens once, for all of them
            // at once, and only for the names that were not found the cheap way.
            if (missing.Count > 0)
            {
                foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    int at = missing.IndexOf(go.name);
                    if (at >= 0)
                    {
                        found.Add(new EffectList.EffectData { m_prefab = go, m_enabled = true });
                        missing.RemoveAt(at);
                        if (missing.Count == 0)
                        {
                            break;
                        }
                    }
                }
            }
            foreach (string name in missing)
            {
                Debug.LogWarning("[ThisIsValheim] no effect prefab called " + name);
            }
            if (found.Count == 0)
            {
                Debug.LogWarning("[ThisIsValheim] nothing left to play, kicks will be quiet");
                return null;
            }
            effects = new EffectList { m_effectPrefabs = found.ToArray() };
            return effects;
        }

        // ---- The swing -----------------------------------------------------------------------

        /// <summary>
        /// Winds the door's animator up and lets it down again once the swing is over. The state
        /// change itself is the game's: it arrives through UseDoor whenever the door's owner gets
        /// around to it, which is why the animator is sped up first and held there for a window
        /// rather than for the length of an animation that has not started yet.
        /// </summary>
        private static void Swing(Door door)
        {
            if (instance == null || door.m_animator == null)
            {
                return;
            }
            Coroutine running;
            if (swinging.TryGetValue(door, out running))
            {
                instance.StopCoroutine(running);
            }
            swinging[door] = instance.StartCoroutine(SwingRoutine(door, door.m_animator));
        }

        private static IEnumerator SwingRoutine(Door door, Animator animator)
        {
            animator.speed = Mathf.Max(1f, swingSpeed.Value);
            yield return new WaitForSeconds(SwingWindow);
            // The door may have been unloaded or torn down in the meantime; the animator is what
            // has to be put back, and only while it is still there.
            if (animator != null)
            {
                animator.speed = 1f;
            }
            swinging.Remove(door);
        }
    }
}
