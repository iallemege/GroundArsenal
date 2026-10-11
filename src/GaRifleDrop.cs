using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>
    /// Rifles drop with gravity. Grab with the right hand (grip) or E.
    /// </summary>
    internal static class GaRifleDrop
    {
        private class Drop
        {
            internal GameObject Go;
            internal int GunId;
            internal int Mag;
            internal float DieAt;
        }

        private static readonly List<Drop> Drops = new List<Drop>(8);
        internal static bool Grabbed;

        internal static void Tick(Transform eye)
        {
            Grabbed = false;
            float now = Time.time;
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                Drop d = Drops[i];
                if (d.Go == null || now >= d.DieAt)
                {
                    Kill(d);
                    Drops.RemoveAt(i);
                    continue;
                }
                Rigidbody rb = d.Go.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic && rb.velocity.sqrMagnitude < 0.04f && now > d.DieAt - 179.2f)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                }
            }
            if (eye == null || !GaMode.Active)
                return;
            if (!GaHud.BlocksLook && !GaMode.Downed && !GaRide.Busy && !GaRadio.Open)
            {
                bool dropWant = Input.GetKeyDown(KeyCode.G) || GaVr.DropDown;
                if (dropWant)
                    TryDrop(eye);
            }
            Vector3 hand = GaVr.HandWorld(eye);
            int near = Nearest(hand, 1.6f);
            if (near >= 0)
            {
                GaGun g = GaCatalog.Get(Drops[near].GunId);
                string name = GaCatalog.Name(g, GaMode.Zh);
                GaLoot.Hint = name + "  grip / E";
            }
            bool grab = Input.GetKeyDown(KeyCode.E) || GaVr.GrabDown;
            if (!grab)
                return;
            int hit = Nearest(hand, GaVr.Active ? 0.55f : 1.35f);
            if (hit < 0)
                return;
            Take(Drops[hit]);
            Drops.RemoveAt(hit);
            Grabbed = true;
            GaVr.ReloadDown = false;
            GaVr.GrabDown = false;
        }

        internal static void Clear()
        {
            for (int i = 0; i < Drops.Count; i++)
                Kill(Drops[i]);
            Drops.Clear();
        }

        private static void TryDrop(Transform eye)
        {
            GaGun g = GaCombat.Current;
            if (g == null)
            {
                GaMode.Deny("NO WEAPON", "没有武器");
                return;
            }
            if (Drops.Count >= 8)
            {
                Kill(Drops[0]);
                Drops.RemoveAt(0);
            }
            int mag = 0;
            if (g.Id >= 0 && g.Id < GaCombat.Mag.Length)
            {
                mag = GaCombat.Mag[g.Id];
                GaCombat.Mag[g.Id] = 0;
            }
            Vector3 pos = eye.position + eye.forward * 0.7f + Vector3.down * 0.12f;
            GameObject go = GaView.CreateWorldRifle(g);
            if (go == null)
            {
                if (g.Id >= 0 && g.Id < GaCombat.Mag.Length)
                    GaCombat.Mag[g.Id] = mag;
                GaMode.Deny("CAN'T DROP", "无法扔下");
                return;
            }
            go.transform.position = pos;
            Vector3 fwd = eye.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f)
                fwd = Vector3.forward;
            else
                fwd.Normalize();
            go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up)
                * Quaternion.Euler(80f, 0f, 0f);
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 3.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.velocity = eye.forward * 3.2f + Vector3.up * 1.1f;
            rb.angularVelocity = new Vector3(2.5f, 0.4f, 0.2f);
            Drop d = new Drop();
            d.Go = go;
            d.GunId = g.Id;
            d.Mag = mag;
            d.DieAt = Time.time + 180f;
            Drops.Add(d);
            GaCombat.Hotbar[GaCombat.Slot] = -1;
            PickNext();
            GaView.Refresh();
            GaMode.Deny("DROPPED", "已扔下武器");
        }

        private static void PickNext()
        {
            int n = GaCombat.Hotbar.Length;
            int slot = GaCombat.Slot;
            for (int i = 0; i < n; i++)
            {
                int s = (slot + i) % n;
                if (GaCombat.Hotbar[s] >= 0)
                {
                    GaCombat.Slot = s;
                    return;
                }
            }
        }

        private static void Take(Drop d)
        {
            if (d == null)
                return;
            int slot = GaCombat.Slot;
            if (slot < 0 || slot >= GaCombat.Hotbar.Length)
                slot = 0;
            GaCombat.Hotbar[slot] = d.GunId;
            if (d.GunId >= 0 && d.GunId < GaCombat.Mag.Length)
                GaCombat.Mag[d.GunId] = d.Mag;
            Kill(d);
            GaView.Refresh();
            GaGun g = GaCatalog.Get(d.GunId);
            GaCombat.Status = GaCatalog.Name(g, GaMode.Zh);
        }

        private static int Nearest(Vector3 hand, float maxDist)
        {
            int best = -1;
            float bestD = maxDist;
            for (int i = 0; i < Drops.Count; i++)
            {
                if (Drops[i].Go == null)
                    continue;
                float dist = Vector3.Distance(hand, Drops[i].Go.transform.position);
                if (dist < bestD)
                {
                    bestD = dist;
                    best = i;
                }
            }
            return best;
        }

        private static void Kill(Drop d)
        {
            if (d != null && d.Go != null)
                UnityEngine.Object.Destroy(d.Go);
        }
    }
}
