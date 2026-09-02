using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    internal static class GaLoot
    {
        internal static string Hint = "";

        private class Pile
        {
            internal GameObject Go;
            internal Vector3 Pos;
            internal int CalA;
            internal int AmtA;
            internal int CalB;
            internal int AmtB;
            internal float DieAt;
        }

        private static readonly List<Pile> Piles = new List<Pile>(24);
        private static readonly List<int> Seen = new List<int>(128);
        private static float _scanAt;
        private static Material _mat;

        internal static void Tick(Vector3 player)
        {
            ScanWrecks();
            Hint = "";
            float best = 12f;
            int near = -1;
            for (int i = Piles.Count - 1; i >= 0; i--)
            {
                Pile p = Piles[i];
                if (p.Go == null || Time.time >= p.DieAt)
                {
                    Kill(p);
                    Piles.RemoveAt(i);
                    continue;
                }
                float d = Vector3.Distance(player, p.Pos);
                if (d < 3.6f)
                {
                    Take(p);
                    Piles.RemoveAt(i);
                    continue;
                }
                if (d < best)
                {
                    best = d;
                    near = i;
                }
            }
            if (near >= 0)
            {
                Pile p = Piles[near];
                Hint = "WRECK  " + Label(p) + "  approach";
            }
        }

        internal static void Clear()
        {
            for (int i = 0; i < Piles.Count; i++)
                Kill(Piles[i]);
            Piles.Clear();
            Seen.Clear();
            Hint = "";
        }

        private static void ScanWrecks()
        {
            if (Time.time < _scanAt)
                return;
            _scanAt = Time.time + 0.22f;
            List<Unit> all = null;
            try { all = UnitRegistry.allUnits; }
            catch { }
            if (all == null)
                return;
            FactionHQ hq = null;
            try { GameManager.GetLocalHQ(out hq); }
            catch { }
            Unit linked = GaMode.LinkedUnit();
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (u == null)
                    continue;
                int id = u.GetInstanceID();
                if (Contains(Seen, id))
                    continue;
                if (!u.disabled)
                    continue;
                Seen.Add(id);
                if (linked != null && object.ReferenceEquals(u, linked))
                    continue;
                if (IsFriendly(u, hq))
                    continue;
                if (!(u is Aircraft) && !(u is GroundVehicle) && !(u is Ship))
                    continue;
                Vector3 pos = u.transform.position + Vector3.up * 0.45f;
                SpawnFor(u, pos);
            }
            if (Seen.Count > 400)
                Seen.RemoveRange(0, 200);
        }

        private static void SpawnFor(Unit u, Vector3 pos)
        {
            int calA = 3;
            int amtA = 60;
            int calB = 2;
            int amtB = 90;
            if (u is Aircraft)
            {
                calA = 5;
                amtA = 20;
                calB = 2;
                amtB = 90;
                if ((u.GetInstanceID() & 3) == 0)
                {
                    calB = 9;
                    amtB = 1;
                }
            }
            else if (u is Ship)
            {
                calA = 5;
                amtA = 24;
                calB = 3;
                amtB = 80;
            }
            else
            {
                int roll = Mathf.Abs(u.GetInstanceID()) % 5;
                if (roll == 0)
                {
                    calA = 7;
                    amtA = 1;
                    calB = 3;
                    amtB = 40;
                }
                else if (roll == 1)
                {
                    calA = 5;
                    amtA = 12;
                    calB = 2;
                    amtB = 90;
                }
                else if (roll == 2)
                {
                    calA = 4;
                    amtA = 90;
                    calB = 0;
                    amtB = 45;
                }
                else
                {
                    calA = 3;
                    amtA = 60;
                    calB = 2;
                    amtB = 90;
                }
            }
            Spawn(pos, calA, amtA, calB, amtB);
        }

        private static void Spawn(Vector3 pos, int calA, int amtA, int calB, int amtB)
        {
            if (Piles.Count >= 24)
            {
                Kill(Piles[0]);
                Piles.RemoveAt(0);
            }
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "GaLoot";
            go.layer = 0;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.38f, 0.22f, 0.28f);
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            Material m = Mat();
            if (r != null && m != null)
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            Pile p = new Pile();
            p.Go = go;
            p.Pos = pos;
            p.CalA = calA;
            p.AmtA = amtA;
            p.CalB = calB;
            p.AmtB = amtB;
            p.DieAt = Time.time + 180f;
            Piles.Add(p);
        }

        private static void Take(Pile p)
        {
            int got = 0;
            string msg = "LOOT";
            if (p.AmtA > 0)
            {
                int n = GaCombat.AddPool(p.CalA, p.AmtA);
                if (n > 0)
                {
                    got += n;
                    msg = msg + "  " + CalName(p.CalA) + " +" + n.ToString();
                }
            }
            if (p.AmtB > 0)
            {
                int n = GaCombat.AddPool(p.CalB, p.AmtB);
                if (n > 0)
                {
                    got += n;
                    msg = msg + "  " + CalName(p.CalB) + " +" + n.ToString();
                }
            }
            Kill(p);
            if (got > 0)
                GaHud.FlashHit(msg);
            else
                GaHud.FlashHit("LOOT  no matching space");
        }

        private static string Label(Pile p)
        {
            string s = CalName(p.CalA);
            if (p.AmtB > 0)
                s = s + " + " + CalName(p.CalB);
            return s;
        }

        private static string CalName(int cal)
        {
            if (cal < 0 || cal >= GaCatalog.CalEn.Length)
                return "?";
            return GaCatalog.CalEn[cal];
        }

        private static void Kill(Pile p)
        {
            if (p != null && p.Go != null)
                UnityEngine.Object.Destroy(p.Go);
        }

        private static bool Contains(List<int> list, int id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                    return true;
            }
            return false;
        }

        private static bool IsFriendly(Unit u, FactionHQ local)
        {
            if (u == null || local == null)
                return false;
            try
            {
                FactionHQ other = u.NetworkHQ;
                if (other == null)
                    return false;
                return object.ReferenceEquals(other, local);
            }
            catch
            {
                return false;
            }
        }

        private static Material Mat()
        {
            if (_mat != null)
                return _mat;
            Shader sh = Shader.Find("Unlit/Color");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            if (sh == null)
                return null;
            _mat = new Material(sh);
            _mat.hideFlags = HideFlags.HideAndDontSave;
            _mat.color = new Color(1f, 0.55f, 0.12f, 1f);
            return _mat;
        }
    }
}
