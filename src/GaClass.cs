using UnityEngine;

namespace GroundArsenal
{
    internal enum GaKitId
    {
        Assault = 0,
        Medic = 1,
        Engineer = 2,
        Support = 3,
        SpecOps = 4,
        AntiTank = 5,
        Sniper = 6,
        AntiAir = 7,
        Tsf = 8
    }

    internal static class GaClass
    {
        internal const int Count = 9;
        internal const string Pref = "GroundArsenal.Class";

        internal static GaKitId Picked = GaKitId.Assault;

        private static readonly string[] En = new string[]
        {
            "Assault", "Medic", "Engineer", "Support",
            "Spec Ops", "Anti-Tank", "Sniper", "Anti-Air", "TSF F-4"
        };

        internal static void Load()
        {
            try
            {
                int v = PlayerPrefs.GetInt(Pref, 0);
                if (v < 0 || v >= Count)
                    v = 0;
                Picked = (GaKitId)v;
            }
            catch
            {
                Picked = GaKitId.Assault;
            }
        }

        internal static void Set(GaKitId k)
        {
            Picked = k;
            try { PlayerPrefs.SetInt(Pref, (int)k); }
            catch { }
        }

        internal static string Name(GaKitId k)
        {
            int i = (int)k;
            if (i < 0 || i >= En.Length)
                return "Assault";
            return En[i];
        }

        internal static string Name()
        {
            return Name(Picked);
        }

        internal static GaKitId Random()
        {
            return (GaKitId)UnityEngine.Random.Range(0, Count);
        }

        internal static Color Tint(GaKitId k)
        {
            if (k == GaKitId.Medic)
                return new Color(0.95f, 0.92f, 0.92f);
            if (k == GaKitId.Engineer)
                return new Color(0.82f, 0.72f, 0.22f);
            if (k == GaKitId.Support)
                return new Color(0.22f, 0.45f, 0.28f);
            if (k == GaKitId.SpecOps)
                return new Color(0.12f, 0.14f, 0.12f);
            if (k == GaKitId.AntiTank)
                return new Color(0.72f, 0.38f, 0.12f);
            if (k == GaKitId.Sniper)
                return new Color(0.28f, 0.38f, 0.18f);
            if (k == GaKitId.AntiAir)
                return new Color(0.22f, 0.42f, 0.72f);
            if (k == GaKitId.Tsf)
                return new Color(0.38f, 0.46f, 0.58f);
            return new Color(0.32f, 0.55f, 0.28f);
        }

        internal static int[] Guns(GaKitId k)
        {
            if (k == GaKitId.Medic)
                return new int[] { 14, 15, 3, 16 };
            if (k == GaKitId.Engineer)
                return new int[] { 30, 31, 18, 1 };
            if (k == GaKitId.Support)
                return new int[] { 7, 5, 19, 2 };
            if (k == GaKitId.SpecOps)
                return new int[] { 42, 43, 20, 21 };
            if (k == GaKitId.AntiTank)
                return new int[] { 11, 4, 25, 24 };
            if (k == GaKitId.Sniper)
                return new int[] { 36, 38, 22, 39 };
            if (k == GaKitId.AntiAir)
                return new int[] { 9, 10, 23, 47 };
            if (k == GaKitId.Tsf)
                return new int[] { 49, 49, -1, 50 };
            return new int[] { 17, 0, 48, 12 };
        }

        internal static string SlotName(int slot)
        {
            if (slot == 1)
                return "SECONDARY";
            if (slot == 2)
                return "PISTOL";
            if (slot == 3)
                return "CLASS";
            return "PRIMARY";
        }

        internal static int[] SlotPool(GaKitId k, int slot)
        {
            if (slot == 3)
            {
                int[] kit = Guns(k);
                return new int[] { kit[3] };
            }
            if (slot == 2)
            {
                if (k == GaKitId.Medic)
                    return new int[] { 3, 18, 19 };
                if (k == GaKitId.Engineer)
                    return new int[] { 18, 23 };
                if (k == GaKitId.Support)
                    return new int[] { 19, 3 };
                if (k == GaKitId.SpecOps)
                    return new int[] { 20, 22 };
                if (k == GaKitId.AntiTank)
                    return new int[] { 25, 26, 27, 28, 29 };
                if (k == GaKitId.Sniper)
                    return new int[] { 22, 20 };
                if (k == GaKitId.AntiAir)
                    return new int[] { 23, 20, 22 };
                if (k == GaKitId.Tsf)
                    return new int[] { 50 };
                return new int[] { 48, 27, 28, 29 };
            }
            if (slot == 1)
            {
                if (k == GaKitId.Medic)
                    return new int[] { 15, 14 };
                if (k == GaKitId.Engineer)
                    return new int[] { 31, 32, 35 };
                if (k == GaKitId.Support)
                    return new int[] { 5, 6, 8, 33 };
                if (k == GaKitId.SpecOps)
                    return new int[] { 43, 44, 45, 46 };
                if (k == GaKitId.AntiTank)
                    return new int[] { 4, 13 };
                if (k == GaKitId.Sniper)
                    return new int[] { 38, 40, 41 };
                if (k == GaKitId.AntiAir)
                    return new int[] { 10, 13 };
                if (k == GaKitId.Tsf)
                    return new int[] { 49 };
                return new int[] { 0, 4, 9, 10 };
            }
            if (k == GaKitId.Medic)
                return new int[] { 14, 15 };
            if (k == GaKitId.Engineer)
                return new int[] { 30, 31, 32 };
            if (k == GaKitId.Support)
                return new int[] { 7, 6, 8 };
            if (k == GaKitId.SpecOps)
                return new int[] { 42, 43, 44, 45, 46 };
            if (k == GaKitId.AntiTank)
                return new int[] { 11, 4, 13 };
            if (k == GaKitId.Sniper)
                return new int[] { 36, 37, 38, 40, 41 };
            if (k == GaKitId.AntiAir)
                return new int[] { 9, 10, 13 };
            if (k == GaKitId.Tsf)
                return new int[] { 49, 50 };
            return new int[] { 17, 0, 4, 9, 10, 11 };
        }

        internal static float MaxHp(GaKitId k)
        {
            if (k == GaKitId.AntiTank)
                return 108f;
            if (k == GaKitId.Support)
                return 98f;
            if (k == GaKitId.Engineer)
                return 90f;
            if (k == GaKitId.AntiAir)
                return 82f;
            if (k == GaKitId.Assault)
                return 80f;
            if (k == GaKitId.Medic)
                return 70f;
            if (k == GaKitId.Sniper)
                return 64f;
            if (k == GaKitId.Tsf)
                return 1600f;
            return 62f;
        }

        internal static int Armor(GaKitId k)
        {
            if (k == GaKitId.AntiTank)
                return 14;
            if (k == GaKitId.Support)
                return 12;
            if (k == GaKitId.Engineer)
                return 12;
            if (k == GaKitId.AntiAir)
                return 10;
            if (k == GaKitId.Assault)
                return 10;
            if (k == GaKitId.Medic)
                return 7;
            if (k == GaKitId.Sniper)
                return 6;
            if (k == GaKitId.Tsf)
                return 20;
            return 5;
        }

        internal static float Walk(GaKitId k)
        {
            if (k == GaKitId.SpecOps)
                return 5.5f;
            if (k == GaKitId.Medic)
                return 5.1f;
            if (k == GaKitId.Assault)
                return 4.8f;
            if (k == GaKitId.AntiAir)
                return 4.5f;
            if (k == GaKitId.Engineer)
                return 4.3f;
            if (k == GaKitId.Sniper)
                return 4.1f;
            if (k == GaKitId.Support)
                return 3.9f;
            if (k == GaKitId.Tsf)
                return 12.5f;
            return 3.6f;
        }

        internal static float Sprint(GaKitId k)
        {
            if (k == GaKitId.SpecOps)
                return 8.8f;
            if (k == GaKitId.Medic)
                return 8.1f;
            if (k == GaKitId.Assault)
                return 7.6f;
            if (k == GaKitId.AntiAir)
                return 7.2f;
            if (k == GaKitId.Engineer)
                return 6.8f;
            if (k == GaKitId.Sniper)
                return 6.4f;
            if (k == GaKitId.Support)
                return 6.1f;
            if (k == GaKitId.Tsf)
                return 22f;
            return 5.6f;
        }

        internal static float JumpMul(GaKitId k)
        {
            if (k == GaKitId.SpecOps)
                return 1.14f;
            if (k == GaKitId.Medic)
                return 1.06f;
            if (k == GaKitId.AntiTank)
                return 0.80f;
            if (k == GaKitId.Support)
                return 0.86f;
            if (k == GaKitId.Sniper)
                return 0.90f;
            if (k == GaKitId.Tsf)
                return 2.2f;
            return 1f;
        }

        internal static float SpreadMul(GaKitId k)
        {
            if (k == GaKitId.Sniper)
                return 0.72f;
            if (k == GaKitId.Assault)
                return 0.92f;
            if (k == GaKitId.Support)
                return 1.28f;
            if (k == GaKitId.AntiTank)
                return 1.22f;
            if (k == GaKitId.SpecOps)
                return 1.12f;
            if (k == GaKitId.Engineer)
                return 1.08f;
            if (k == GaKitId.Tsf)
                return 0.55f;
            return 1f;
        }

        internal static float ReloadMul(GaKitId k)
        {
            if (k == GaKitId.SpecOps)
                return 0.78f;
            if (k == GaKitId.Assault)
                return 0.90f;
            if (k == GaKitId.Medic)
                return 0.92f;
            if (k == GaKitId.Support)
                return 1.12f;
            if (k == GaKitId.AntiTank)
                return 1.18f;
            if (k == GaKitId.Engineer)
                return 1.08f;
            if (k == GaKitId.Tsf)
                return 0.72f;
            return 1f;
        }

        internal static float DmgMul(GaKitId k)
        {
            if (k == GaKitId.Sniper)
                return 1.18f;
            if (k == GaKitId.AntiTank)
                return 1.12f;
            if (k == GaKitId.Support)
                return 0.92f;
            if (k == GaKitId.SpecOps)
                return 0.90f;
            if (k == GaKitId.Medic)
                return 0.88f;
            if (k == GaKitId.Tsf)
                return 1.45f;
            return 1f;
        }

        internal static float ShotRange(GaKitId k)
        {
            if (k == GaKitId.Sniper)
                return 130f;
            if (k == GaKitId.AntiAir)
                return 100f;
            if (k == GaKitId.Support)
                return 78f;
            if (k == GaKitId.Assault)
                return 70f;
            if (k == GaKitId.AntiTank)
                return 55f;
            if (k == GaKitId.Engineer)
                return 42f;
            if (k == GaKitId.SpecOps)
                return 38f;
            if (k == GaKitId.Tsf)
                return 420f;
            return 58f;
        }

        internal static float ShotDmg(GaKitId k)
        {
            if (k == GaKitId.Sniper)
                return 16f;
            if (k == GaKitId.AntiTank)
                return 15f;
            if (k == GaKitId.Support)
                return 7f;
            if (k == GaKitId.AntiAir)
                return 8f;
            if (k == GaKitId.Assault)
                return 6f;
            if (k == GaKitId.Engineer)
                return 9f;
            if (k == GaKitId.SpecOps)
                return 5f;
            if (k == GaKitId.Tsf)
                return 48f;
            return 4.5f;
        }

        internal static float ShotCd(GaKitId k)
        {
            if (k == GaKitId.Support)
                return 0.18f;
            if (k == GaKitId.SpecOps)
                return 0.22f;
            if (k == GaKitId.Assault)
                return 0.32f;
            if (k == GaKitId.Medic)
                return 0.36f;
            if (k == GaKitId.Engineer)
                return 0.70f;
            if (k == GaKitId.AntiAir)
                return 0.45f;
            if (k == GaKitId.Sniper)
                return 1.25f;
            if (k == GaKitId.Tsf)
                return 0.08f;
            return 1.5f;
        }

        internal static float ReserveMul(GaKitId k)
        {
            if (k == GaKitId.Assault)
                return 2.4f;
            if (k == GaKitId.Support)
                return 1.25f;
            if (k == GaKitId.AntiTank || k == GaKitId.AntiAir)
                return 1.1f;
            return 1f;
        }

        internal static string AmmoHint()
        {
            if (GaClass.Picked == GaKitId.Assault)
                return "";
            GaDropBot src = NearAssault();
            if (src == null)
                return "";
            if (Time.unscaledTime < _ammoReady)
                return "AMMO  wait " + Mathf.CeilToInt(_ammoReady - Time.unscaledTime).ToString() + "s";
            return "E  AMMO  " + src.Callsign;
        }

        internal static void Tick()
        {
            if (!GaMode.Active || GaMode.Dead)
                return;
            TickAmmo();
            if (Picked == GaKitId.Medic)
                PulseMedic();
            else if (Picked == GaKitId.Support)
                PulseSupport();
        }

        private static float _ammoReady;

        private static void TickAmmo()
        {
            if (GaHud.BlocksLook || GaRide.Busy || GaRadio.Open)
                return;
            if (Picked == GaKitId.Assault)
                return;
            if (GaRifleDrop.Grabbed || GaAtLaser.TookE)
                return;
            GaDropBot src = NearAssault();
            if (src == null)
                return;
            bool grab = Input.GetKeyDown(KeyCode.E) || GaVr.GrabDown;
            if (!grab)
                return;
            if (Time.unscaledTime < _ammoReady)
            {
                GaMode.Deny("Ammo pack cooling down.", "补弹药冷却中。");
                return;
            }
            int got = GaCombat.ResupplyFromPack();
            if (got <= 0)
            {
                GaMode.Deny("Already full.", "弹药已满。");
                return;
            }
            _ammoReady = Time.unscaledTime + 6f;
            GaVr.GrabDown = false;
            GaMode.Deny("Ammo +" + got.ToString(), "补弹 +" + got.ToString());
        }

        private static GaDropBot NearAssault()
        {
            GaDropBot b = GaDropSquad.NearestKit(GaMode.EyePos, true, GaKitId.Assault);
            if (b == null || b.transform == null)
                return null;
            float max = 3.4f;
            if ((b.transform.position - GaMode.EyePos).sqrMagnitude > max * max)
                return null;
            return b;
        }

        private static float _nextMedic;
        private static float _nextSupp;

        private static void PulseMedic()
        {
            if (Time.time < _nextMedic)
                return;
            _nextMedic = Time.time + 1f;
            if (GaMode.Hp < GaMode.MaxHp)
            {
                GaMode.Hp += 3.5f;
                if (GaMode.Hp > GaMode.MaxHp)
                    GaMode.Hp = GaMode.MaxHp;
            }
            GaDropSquad.HealFriendlies(GaMode.EyePos, 14f, 6f);
        }

        private static void PulseSupport()
        {
            if (Time.time < _nextSupp)
                return;
            _nextSupp = Time.time + 3.5f;
            GaCombat.ResupplyPool(8);
        }
    }
}
