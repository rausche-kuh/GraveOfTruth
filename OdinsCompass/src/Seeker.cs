using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace OdinsCompass
{
    /// <summary>
    /// Finds where a target group is, for the local player's compass. A prefab that is a location
    /// (in ZoneSystem's list) is asked of the server with the vegvisir's own request, one ask per
    /// prefab, and the nearest answer wins; the server answers whether or not it has the mod, and
    /// answers nothing at all when the location does not exist in the world, which after a few
    /// silent rounds counts as "not in this world". Any other prefab (an ore vein, a totem) is
    /// searched among the objects this client has loaded - everything it has seen this session -
    /// a few sectors per tick so nothing stalls. Asks are rare: a found target is kept until the
    /// player has walked a good share of the way toward it (locations do not move, and which one
    /// is nearest only changes as the player does), an unanswered one is repeated on SeekInterval
    /// until silence counts, then every RetryInterval.
    /// </summary>
    internal sealed class Seeker
    {
        internal enum State
        {
            /// <summary>Nothing chosen.</summary>
            Idle,
            /// <summary>Asked, no answer yet.</summary>
            Seeking,
            /// <summary>A position is known.</summary>
            Found,
            /// <summary>A location group the server stays silent about: not in this world.</summary>
            Lost,
            /// <summary>A world object group with nothing among the loaded objects.</summary>
            NoneNear,
        }

        private const string PinPrefix = "OdinsCompass:";
        /// <summary>Silent rounds after which a location group counts as absent.</summary>
        private const int SilentRounds = 3;
        /// <summary>How long a group that seems absent, or has nothing nearby, waits before the next ask.</summary>
        private const float RetryInterval = 30f;
        /// <summary>
        /// A found target is asked for again once the player has moved this share of the way
        /// toward it since the ask, at least ReaskMin metres and at most ReaskMax.
        /// </summary>
        private const float ReaskShare = 0.25f;
        private const float ReaskMin = 20f;
        private const float ReaskMax = 200f;

        /// <summary>What "far" means for the pings and the streak density.</summary>
        internal const float LocationRange = 2000f;
        internal const float VeinRange = 200f;

        /// <summary>The one seeker answers are routed to: the local player's.</summary>
        private static Seeker current;

        internal TargetGroup Group { get; private set; }

        private readonly List<string> locations = new List<string>();
        private readonly List<string> objects = new List<string>();

        private float timer;
        private int round;
        private Vector3 askPos;

        private int answerRound = -1;
        private Vector3 answerPos;

        private int objectRound = -1;
        private Vector3 objectPos;
        private bool objectFound;
        // The scan in progress: which prefab of the group, where in the sector list.
        private int scanPrefab = -1;
        private int scanIndex;
        private readonly List<ZDO> scanned = new List<ZDO>();
        private float scanBest;
        private bool scanHit;

        internal Seeker()
        {
            current = this;
        }

        internal void Release()
        {
            if (current == this)
            {
                current = null;
            }
        }

        /// <summary>Whether the group holds a location - the range and the words depend on it.</summary>
        internal bool SeeksLocations => locations.Count > 0;

        internal float Range => SeeksLocations ? LocationRange : VeinRange;

        /// <summary>Points the seeker at a group (null for none) and starts over.</summary>
        internal void Choose(TargetGroup group)
        {
            Group = group;
            locations.Clear();
            objects.Clear();
            round = 0;
            answerRound = -1;
            objectRound = -1;
            objectFound = false;
            scanPrefab = -1;
            timer = float.MaxValue; // ask on the next tick
            if (group == null)
            {
                return;
            }
            foreach (string prefab in group.Prefabs)
            {
                switch (Kind(prefab))
                {
                    case PrefabKind.Location: locations.Add(prefab); break;
                    case PrefabKind.Object: objects.Add(prefab); break;
                }
            }
        }

        /// <summary>The best known position, or null while nothing is known.</summary>
        internal Vector3? Position(Vector3 from)
        {
            bool haveAnswer = answerRound >= 0 && round - answerRound < SilentRounds;
            bool haveObject = objectFound && round - objectRound <= 1;
            if (haveAnswer && haveObject)
            {
                return Utils.DistanceXZ(from, answerPos) <= Utils.DistanceXZ(from, objectPos) ? answerPos : objectPos;
            }
            if (haveAnswer)
            {
                return answerPos;
            }
            if (haveObject)
            {
                return objectPos;
            }
            return null;
        }

        internal State Current(Vector3 from)
        {
            if (Group == null)
            {
                return State.Idle;
            }
            if (Position(from).HasValue)
            {
                return State.Found;
            }
            if (round < SilentRounds)
            {
                return State.Seeking;
            }
            return SeeksLocations ? State.Lost : State.NoneNear;
        }

        /// <summary>Called every status effect tick with the wearer's position.</summary>
        internal void Update(float dt, Vector3 from)
        {
            if (Group == null)
            {
                return;
            }
            timer += dt;
            if (timer >= OdinsCompassPlugin.SeekInterval.Value && Due(from))
            {
                timer = 0f;
                round++;
                askPos = from;
                Ask(from);
            }
            ContinueScan(from);
        }

        /// <summary>
        /// Whether it is time to ask again. Never asked: yes. A target is known: only after
        /// walking a good share of the way toward it since the last ask. Nothing known: on the
        /// interval until silence has counted, then rarely - and for world objects also after
        /// walking a bit, since walking loads new ground.
        /// </summary>
        private bool Due(Vector3 from)
        {
            if (round == 0)
            {
                return true;
            }
            float walked = Utils.DistanceXZ(from, askPos);
            Vector3? known = Position(from);
            if (known.HasValue)
            {
                float reask = Mathf.Clamp(Utils.DistanceXZ(askPos, known.Value) * ReaskShare, ReaskMin, ReaskMax);
                return walked >= reask;
            }
            if (round < SilentRounds || timer >= RetryInterval)
            {
                return true;
            }
            return !SeeksLocations && walked >= ReaskMin;
        }

        /// <summary>
        /// The vegvisir's request, sent straight over the routed RPC: Game.DiscoverClosestLocation
        /// is the same call behind a log line per ask.
        /// </summary>
        private void Ask(Vector3 from)
        {
            if (ZRoutedRpc.instance != null && ZNet.instance != null)
            {
                foreach (string name in locations)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC("RPC_DiscoverClosestLocation", name, from, PinPrefix + round, 0, false, false);
                }
            }
            if (objects.Count > 0 && scanPrefab < 0)
            {
                scanPrefab = 0;
                scanIndex = 0;
                scanned.Clear();
                scanHit = false;
                scanBest = float.MaxValue;
            }
        }

        /// <summary>
        /// A slice of the object search per tick: ZDOMan walks up to 400 sectors per call and
        /// says when it is through. When every prefab of the group has been walked, the nearest
        /// hit becomes this round's object position.
        /// </summary>
        private void ContinueScan(Vector3 from)
        {
            if (scanPrefab < 0 || ZDOMan.instance == null)
            {
                return;
            }
            if (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(objects[scanPrefab], scanned, ref scanIndex))
            {
                return;
            }
            foreach (ZDO zdo in scanned)
            {
                float distance = Utils.DistanceXZ(from, zdo.GetPosition());
                if (distance < scanBest)
                {
                    scanBest = distance;
                    objectPos = zdo.GetPosition();
                    scanHit = true;
                }
            }
            scanned.Clear();
            scanIndex = 0;
            scanPrefab++;
            if (scanPrefab >= objects.Count)
            {
                scanPrefab = -1;
                objectRound = round;
                objectFound = scanHit;
            }
        }

        private void Answer(string pinName, Vector3 pos)
        {
            int answered;
            if (!int.TryParse(pinName.Substring(PinPrefix.Length), out answered))
            {
                return;
            }
            if (answered > answerRound)
            {
                answerRound = answered;
                answerPos = pos;
            }
            else if (answered == answerRound && Utils.DistanceXZ(askPos, pos) < Utils.DistanceXZ(askPos, answerPos))
            {
                answerPos = pos;
            }
        }

        private enum PrefabKind { Unknown, Location, Object }

        private static readonly Dictionary<string, PrefabKind> kinds = new Dictionary<string, PrefabKind>();

        /// <summary>
        /// Location or world object, decided once per name. ZoneSystem's location list exists on
        /// clients too (definitions only - the instances live on the server, which is why they
        /// are asked for); anything else must at least be a prefab the network scene knows.
        /// </summary>
        private static PrefabKind Kind(string prefab)
        {
            PrefabKind kind;
            if (kinds.TryGetValue(prefab, out kind))
            {
                return kind;
            }
            kind = PrefabKind.Unknown;
            if (ZoneSystem.instance != null && ZoneSystem.instance.m_locationsByHash.ContainsKey(prefab.GetStableHashCode()))
            {
                kind = PrefabKind.Location;
            }
            else if (ZNetScene.instance != null && ZNetScene.instance.GetPrefab(prefab) != null)
            {
                kind = PrefabKind.Object;
            }
            else if (ZoneSystem.instance == null || ZNetScene.instance == null)
            {
                // Too early to tell; do not remember that.
                return kind;
            }
            else
            {
                Debug.LogWarning("[OdinsCompass] '" + prefab + "' is neither a location nor a prefab of this game; the compass cannot seek it");
            }
            kinds[prefab] = kind;
            return kind;
        }

        /// <summary>
        /// The server's answer to a vegvisir style ask. The compass's own asks carry its prefix in
        /// the pin name and are taken here - no pin, no map, no forced look; every other answer,
        /// a real vegvisir's, passes through untouched.
        /// </summary>
        [HarmonyPatch(typeof(Game), "RPC_DiscoverLocationResponse")]
        private static class Answers
        {
            private static bool Prefix(string pinName, Vector3 pos)
            {
                if (pinName == null || !pinName.StartsWith(PinPrefix))
                {
                    return true;
                }
                current?.Answer(pinName, pos);
                return false;
            }
        }
    }
}
