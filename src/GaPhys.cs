using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>
    /// Infantry colliders: CharacterController walks on the world only.
    /// Hitboxes stay raycastable but exclude PhysX contacts so they do not
    /// fight the controller or the terrain mesh.
    /// </summary>
    internal static class GaPhys
    {
        private static readonly List<CharacterController> Walkers =
            new List<CharacterController>(16);
        private static readonly Vector3[] AglAt = new Vector3[8];
        private static readonly float[] AglUntil = new float[8];
        private static readonly float[] AglVal = new float[8];
        private static readonly float[] AglMax = new float[8];
        private static int _aglClock;
        private static int _worldMask = -1;
        private static int _walkMask = -1;

        internal static int WalkMask
        {
            get
            {
                if (_walkMask < 0)
                {
                    int m = 0;
                    try
                    {
                        m = PhysicsLayers.StaticsMask
                            | PhysicsLayers.WaterMask
                            | PhysicsLayers.ShipsMask
                            | PhysicsLayers.DefaultMask;
                    }
                    catch { m = 0; }
                    if (m == 0)
                        m = Physics.DefaultRaycastLayers;
                    _walkMask = m;
                }
                return _walkMask;
            }
        }

        internal static int WorldMask
        {
            get
            {
                if (_worldMask < 0)
                    _worldMask = Physics.DefaultRaycastLayers;
                return _worldMask;
            }
        }

        internal const float FallKillM = 32f;

        internal static void Strip(GameObject go)
        {
            if (go == null)
                return;
            Collider col = go.GetComponent<Collider>();
            if (col == null)
                return;
            try { UnityEngine.Object.DestroyImmediate(col); }
            catch { UnityEngine.Object.Destroy(col); }
        }

        internal static void TuneWalker(CharacterController cc)
        {
            if (cc == null)
                return;
            cc.height = 1.8f;
            cc.radius = 0.26f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.28f;
            cc.slopeLimit = 48f;
            cc.skinWidth = 0.12f;
            cc.minMoveDistance = 0.002f;
            cc.detectCollisions = false;
            int walk = WalkMask;
            try
            {
                cc.includeLayers = walk;
                cc.excludeLayers = ~walk;
            }
            catch
            {
                try { cc.excludeLayers = ~walk; }
                catch { }
            }
            Register(cc);
        }

        internal static void TuneTsf(CharacterController cc)
        {
            if (cc == null)
                return;
            cc.height = GaTsf.Height;
            cc.radius = GaTsf.Radius;
            cc.center = new Vector3(0f, GaTsf.Height * 0.5f, 0f);
            cc.stepOffset = 2.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.28f;
            cc.minMoveDistance = 0.01f;
            cc.detectCollisions = false;
            int walk = WalkMask;
            try
            {
                cc.includeLayers = walk;
                cc.excludeLayers = ~walk;
            }
            catch
            {
                try { cc.excludeLayers = ~walk; }
                catch { }
            }
            Register(cc);
        }

        internal static CapsuleCollider Hitbox(GameObject host)
        {
            if (host == null)
                return null;
            CapsuleCollider cap = host.GetComponent<CapsuleCollider>();
            if (cap == null)
                cap = host.AddComponent<CapsuleCollider>();
            cap.height = 1.78f;
            cap.radius = 0.28f;
            cap.center = new Vector3(0f, 0.9f, 0f);
            cap.direction = 1;
            cap.isTrigger = false;
            cap.enabled = true;
            try { cap.excludeLayers = ~0; }
            catch { cap.isTrigger = true; }
            return cap;
        }

        internal static float Agl(Vector3 pos, float maxDist)
        {
            if (maxDist < 2f)
                maxDist = 2f;
            float now = Time.unscaledTime;
            int best = -1;
            float bestD = 2.4f;
            for (int i = 0; i < 8; i++)
            {
                if (now >= AglUntil[i])
                    continue;
                if (maxDist > AglMax[i] + 0.5f)
                    continue;
                float d = (pos - AglAt[i]).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            if (best >= 0)
                return AglVal[best];
            RaycastHit hit;
            float v;
            if (Physics.Raycast(pos + Vector3.up * 0.35f, Vector3.down, out hit,
                maxDist + 0.35f, WorldMask, QueryTriggerInteraction.Ignore))
                v = pos.y - hit.point.y;
            else
                v = maxDist;
            int slot = _aglClock++ & 7;
            AglAt[slot] = pos;
            AglUntil[slot] = now + 0.16f;
            AglVal[slot] = v;
            AglMax[slot] = maxDist;
            return v;
        }

        internal static GaDropBot Bot(Collider col)
        {
            return col == null ? null : Bot(col.transform);
        }

        internal static GaDropBot Bot(Transform t)
        {
            if (t == null)
                return null;
            GaDropBot b = t.GetComponent<GaDropBot>();
            if (b != null)
                return b;
            Transform p = t.parent;
            if (p != null)
            {
                b = p.GetComponent<GaDropBot>();
                if (b != null)
                    return b;
            }
            return t.GetComponentInParent<GaDropBot>();
        }

        internal static GaPawn Pawn(Collider col)
        {
            return col == null ? null : Pawn(col.transform);
        }

        internal static GaPawn Pawn(Transform t)
        {
            if (t == null)
                return null;
            GaPawn p = t.GetComponent<GaPawn>();
            if (p != null)
                return p;
            Transform up = t.parent;
            if (up != null)
            {
                p = up.GetComponent<GaPawn>();
                if (p != null)
                    return p;
            }
            return t.GetComponentInParent<GaPawn>();
        }

        internal static bool RayWalk(Vector3 pos, float maxDist, out RaycastHit hit)
        {
            return Physics.Raycast(pos + Vector3.up * 0.35f, Vector3.down, out hit,
                maxDist + 0.35f, WalkMask, QueryTriggerInteraction.Ignore);
        }

        internal static bool RayDown(Vector3 pos, float maxDist, out RaycastHit hit)
        {
            return Physics.Raycast(pos + Vector3.up * 0.35f, Vector3.down, out hit,
                maxDist + 0.35f, WorldMask, QueryTriggerInteraction.Ignore);
        }

        internal static void Register(CharacterController cc)
        {
            if (cc == null)
                return;
            for (int i = Walkers.Count - 1; i >= 0; i--)
            {
                if (Walkers[i] == null)
                {
                    Walkers.RemoveAt(i);
                    continue;
                }
                if (object.ReferenceEquals(Walkers[i], cc))
                    return;
            }
            Walkers.Add(cc);
        }

        internal static void Unregister(CharacterController cc)
        {
            if (cc == null)
                return;
            Walkers.Remove(cc);
        }

        internal static void ClearWalkers()
        {
            Walkers.Clear();
        }

        internal static bool IsWalkerHit(Collider col)
        {
            if (col == null)
                return false;
            if (Bot(col) != null || Pawn(col) != null)
                return true;
            return GaNet.FromHit(col) != null;
        }
    }

    internal static class GaDeck
    {
        internal static void Carry(Transform pawn, CharacterController cc, ref Transform deck, ref Vector3 local)
        {
            if (pawn == null || deck == null)
                return;
            Vector3 want = deck.TransformPoint(local);
            Vector3 delta = want - pawn.position;
            if (delta.sqrMagnitude > 900f)
            {
                deck = null;
                return;
            }
            if (cc != null && cc.enabled)
                cc.Move(delta);
            else
                pawn.position = want;
        }

        internal static void Sample(Transform pawn, ref Transform deck, ref Vector3 local)
        {
            if (pawn == null)
            {
                deck = null;
                return;
            }
            RaycastHit hit;
            Vector3 o = pawn.position + Vector3.up * 1.2f;
            if (!Physics.Raycast(o, Vector3.down, out hit, 3.6f, GaPhys.WalkMask, QueryTriggerInteraction.Ignore))
            {
                deck = null;
                return;
            }
            if (GaPhys.IsWalkerHit(hit.collider))
                return;
            Transform d = Of(hit.collider);
            if (d == null)
            {
                deck = null;
                return;
            }
            deck = d;
            local = d.InverseTransformPoint(pawn.position);
        }

        internal static Transform Of(Collider col)
        {
            if (col == null)
                return null;
            try
            {
                Ship ship = col.GetComponentInParent<Ship>();
                if (ship != null && !ship.disabled)
                    return ship.transform;
            }
            catch { }
            try
            {
                GroundVehicle v = col.GetComponentInParent<GroundVehicle>();
                if (v != null && !v.disabled)
                    return v.transform;
            }
            catch { }
            return null;
        }
    }
}
