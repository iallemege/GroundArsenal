using UnityEngine;

namespace GroundArsenal
{
    internal sealed class GaGun
    {
        internal int Id;
        internal string En;
        internal string Zh;
        internal int Family;
        internal int Fire;
        internal int Mag;
        internal int Reserve;
        internal float Rpm;
        internal float Spread;
        internal float Damage;
        internal float Pen;
        internal float Range;
        internal Color Tint;
        internal Color Tracer;
        internal string Icon;
        internal string Kind;
        internal string Body;
    }

    internal static class GaCatalog
    {
        internal const int Count = 49;
        internal const int FamilyCount = 16;
        internal const int HotbarSize = 9;
        internal static readonly GaGun[] All = Build();

        internal static readonly string[] FamilyEn = new string[]
        {
            "Revolver", "Pistol", "Machine pistol", "SMG", "Shotgun", "Auto shotgun", "Carbine", "Assault rifle",
            "Battle rifle", "DMR", "Sniper", "LMG", "Grenade launcher", "Rocket", "ATGM / MANPADS", "Energy"
        };

        internal static GaGun Get(int id)
        {
            if (id < 0 || id >= Count)
                return All[0];
            return All[id];
        }

        internal static string Name(GaGun g, bool zh)
        {
            if (g == null)
                return "?";
            return zh ? g.Zh : g.En;
        }

        internal static int FindBody(string body)
        {
            if (string.IsNullOrEmpty(body))
                return 0;
            for (int i = 0; i < Count; i++)
            {
                if (All[i].Body == body)
                    return i;
            }
            return 0;
        }

        internal static bool FamilyHas(int fam)
        {
            for (int i = 0; i < Count; i++)
            {
                if (All[i].Family == fam)
                    return true;
            }
            return false;
        }

        internal static float CostM(GaGun g)
        {
            if (g == null)
                return 0.10f;
            if (g.Body == "at4.obj")
                return 1.45f;
            if (g.Body == "q_RPG7.obj")
                return 1.15f;
            if (g.Body == "rbs70.obj")
                return 2.80f;
            if (g.Body == "g11.obj")
                return 0.85f;
            if (g.Family == 0)
                return 0.16f;
            if (g.Family == 1)
                return 0.12f;
            if (g.Family == 2)
                return 0.20f;
            if (g.Family == 3)
                return 0.22f;
            if (g.Family == 4 || g.Family == 5)
                return 0.28f;
            if (g.Family == 6)
                return 0.38f;
            if (g.Family == 7)
                return 0.45f;
            if (g.Family == 8)
                return 0.55f;
            if (g.Family == 9)
                return 0.62f;
            if (g.Family == 10)
                return 0.75f;
            if (g.Family == 11)
                return 0.70f;
            if (g.Family == 12)
                return 0.90f;
            if (g.Family == 13)
                return 1.15f;
            if (g.Family == 14)
                return 2.80f;
            if (g.Family == 15)
                return 0.85f;
            if (g.Kind == "HEAT")
                return 1.20f;
            if (g.Kind == "HE")
                return 2.40f;
            return 0.35f;
        }

        internal static string FireName(int fire, bool zh)
        {
            if (fire == 1) return zh ? "\u5168\u81ea\u52a8" : "Auto";
            if (fire == 2) return zh ? "\u70b9\u5c04" : "Burst";
            if (fire == 3) return zh ? "\u9730\u5f39" : "Shotgun";
            if (fire == 4) return zh ? "\u706b\u7bad" : "Rocket";
            if (fire == 5) return zh ? "\u80fd\u91cf" : "Energy";
            return zh ? "\u5355\u53d1" : "Semi";
        }

        private static GaGun[] Build()
        {
            GaGun[] a = new GaGun[Count];
            a[0] = G(0, "AK", "AK", 7, 1, 30, 150, 600f, 0.011f, 22f, 5f, 80f, "weaponIcon_ak_body.png", "KE", "ak_body.obj");
            a[1] = G(1, "AT-4", "AT-4", 13, 4, 1, 3, 16f, 0.008f, 80f, 18f, 80f, "weaponIcon_at4.png", "HEAT", "at4.obj");
            a[2] = G(2, "G11", "G11", 15, 2, 50, 200, 600f, 0.008f, 22f, 6f, 80f, "weaponIcon_g11.png", "KE", "g11.obj");
            a[3] = G(3, "Pistol", "Pistol", 1, 0, 15, 75, 280f, 0.014f, 12f, 2f, 50f, "weaponIcon_pistol_body.png", "KE", "pistol_body.obj");
            a[4] = G(4, "AK-47", "AK-47", 7, 1, 30, 150, 600f, 0.011f, 22f, 5f, 80f, "weaponIcon_q_AK47.png", "KE", "q_AK47.obj");
            a[5] = G(5, "Battle Rifle 1", "Battle Rifle 1", 8, 0, 20, 100, 500f, 0.008f, 28f, 6f, 110f, "weaponIcon_q_AssaultRifle2_1.png", "KE", "q_AssaultRifle2_1.obj");
            a[6] = G(6, "Battle Rifle 2", "Battle Rifle 2", 8, 0, 20, 100, 500f, 0.008f, 28f, 6f, 110f, "weaponIcon_q_AssaultRifle2_2.png", "KE", "q_AssaultRifle2_2.obj");
            a[7] = G(7, "Battle Rifle 3", "Battle Rifle 3", 8, 0, 20, 100, 500f, 0.008f, 28f, 6f, 110f, "weaponIcon_q_AssaultRifle2_3.png", "KE", "q_AssaultRifle2_3.obj");
            a[8] = G(8, "Battle Rifle 4", "Battle Rifle 4", 8, 0, 20, 100, 500f, 0.008f, 28f, 6f, 110f, "weaponIcon_q_AssaultRifle2_4.png", "KE", "q_AssaultRifle2_4.obj");
            a[9] = G(9, "Assault Rifle 1", "Assault Rifle 1", 7, 1, 30, 150, 700f, 0.009f, 20f, 4f, 85f, "weaponIcon_q_AssaultRifle_1.png", "KE", "q_AssaultRifle_1.obj");
            a[10] = G(10, "Assault Rifle 2", "Assault Rifle 2", 7, 1, 30, 150, 700f, 0.009f, 20f, 4f, 85f, "weaponIcon_q_AssaultRifle_2.png", "KE", "q_AssaultRifle_2.obj");
            a[11] = G(11, "Assault Rifle 3", "Assault Rifle 3", 7, 1, 30, 150, 700f, 0.009f, 20f, 4f, 85f, "weaponIcon_q_AssaultRifle_3.png", "KE", "q_AssaultRifle_3.obj");
            a[12] = G(12, "Assault Rifle 4", "Assault Rifle 4", 7, 1, 30, 150, 700f, 0.009f, 20f, 4f, 85f, "weaponIcon_q_AssaultRifle_4.png", "KE", "q_AssaultRifle_4.obj");
            a[13] = G(13, "Assault Rifle 5", "Assault Rifle 5", 7, 1, 30, 150, 700f, 0.009f, 20f, 4f, 85f, "weaponIcon_q_AssaultRifle_5.png", "KE", "q_AssaultRifle_5.obj");
            a[14] = G(14, "Bullpup 1", "Bullpup 1", 6, 1, 30, 150, 750f, 0.01f, 20f, 4f, 80f, "weaponIcon_q_Bullpup_1.png", "KE", "q_Bullpup_1.obj");
            a[15] = G(15, "Bullpup 2", "Bullpup 2", 6, 1, 30, 150, 750f, 0.01f, 20f, 4f, 80f, "weaponIcon_q_Bullpup_2.png", "KE", "q_Bullpup_2.obj");
            a[16] = G(16, "Bullpup 3", "Bullpup 3", 6, 1, 30, 150, 750f, 0.01f, 20f, 4f, 80f, "weaponIcon_q_Bullpup_3.png", "KE", "q_Bullpup_3.obj");
            a[17] = G(17, "M16", "M16", 7, 1, 30, 150, 700f, 0.009f, 20f, 4f, 85f, "weaponIcon_q_M16.png", "KE", "q_M16.obj");
            a[18] = G(18, "Pistol 1", "Pistol 1", 1, 0, 15, 75, 280f, 0.014f, 12f, 2f, 50f, "weaponIcon_q_Pistol_1.png", "KE", "q_Pistol_1.obj");
            a[19] = G(19, "Pistol 2", "Pistol 2", 1, 0, 15, 75, 280f, 0.014f, 12f, 2f, 50f, "weaponIcon_q_Pistol_2.png", "KE", "q_Pistol_2.obj");
            a[20] = G(20, "Pistol 3", "Pistol 3", 1, 0, 15, 75, 280f, 0.014f, 12f, 2f, 50f, "weaponIcon_q_Pistol_3.png", "KE", "q_Pistol_3.obj");
            a[21] = G(21, "Pistol 4", "Pistol 4", 1, 0, 15, 75, 280f, 0.014f, 12f, 2f, 50f, "weaponIcon_q_Pistol_4.png", "KE", "q_Pistol_4.obj");
            a[22] = G(22, "Pistol 5", "Pistol 5", 1, 0, 15, 75, 280f, 0.014f, 12f, 2f, 50f, "weaponIcon_q_Pistol_5.png", "KE", "q_Pistol_5.obj");
            a[23] = G(23, "Pistol 6", "Pistol 6", 1, 0, 15, 75, 280f, 0.014f, 12f, 2f, 50f, "weaponIcon_q_Pistol_6.png", "KE", "q_Pistol_6.obj");
            a[24] = G(24, "RPG-7", "RPG-7", 13, 4, 1, 4, 20f, 0.01f, 70f, 16f, 90f, "weaponIcon_q_RPG7.png", "HEAT", "q_RPG7.obj");
            a[25] = G(25, "Revolver 1", "Revolver 1", 0, 0, 6, 48, 90f, 0.012f, 32f, 3f, 70f, "weaponIcon_q_Revolver_1.png", "KE", "q_Revolver_1.obj");
            a[26] = G(26, "Revolver 2", "Revolver 2", 0, 0, 6, 48, 90f, 0.012f, 32f, 3f, 70f, "weaponIcon_q_Revolver_2.png", "KE", "q_Revolver_2.obj");
            a[27] = G(27, "Revolver 3", "Revolver 3", 0, 0, 6, 48, 90f, 0.012f, 32f, 3f, 70f, "weaponIcon_q_Revolver_3.png", "KE", "q_Revolver_3.obj");
            a[28] = G(28, "Revolver 4", "Revolver 4", 0, 0, 6, 48, 90f, 0.012f, 32f, 3f, 70f, "weaponIcon_q_Revolver_4.png", "KE", "q_Revolver_4.obj");
            a[29] = G(29, "Revolver 5", "Revolver 5", 0, 0, 6, 48, 90f, 0.012f, 32f, 3f, 70f, "weaponIcon_q_Revolver_5.png", "KE", "q_Revolver_5.obj");
            a[30] = G(30, "Shotgun 1", "Shotgun 1", 4, 3, 8, 40, 70f, 0.055f, 10f, 3f, 28f, "weaponIcon_q_Shotgun_1.png", "KE", "q_Shotgun_1.obj");
            a[31] = G(31, "Shotgun 2", "Shotgun 2", 4, 3, 8, 40, 70f, 0.055f, 10f, 3f, 28f, "weaponIcon_q_Shotgun_2.png", "KE", "q_Shotgun_2.obj");
            a[32] = G(32, "Shotgun 3", "Shotgun 3", 4, 3, 8, 40, 70f, 0.055f, 10f, 3f, 28f, "weaponIcon_q_Shotgun_3.png", "KE", "q_Shotgun_3.obj");
            a[33] = G(33, "Shotgun 4", "Shotgun 4", 4, 3, 8, 40, 70f, 0.055f, 10f, 3f, 28f, "weaponIcon_q_Shotgun_4.png", "KE", "q_Shotgun_4.obj");
            a[34] = G(34, "Shotgun Sawed-Off", "Shotgun Sawed-Off", 4, 3, 8, 40, 70f, 0.055f, 10f, 3f, 28f, "weaponIcon_q_Shotgun_SawedOff.png", "KE", "q_Shotgun_SawedOff.obj");
            a[35] = G(35, "Shotgun Short", "Shotgun Short", 4, 3, 8, 40, 70f, 0.055f, 10f, 3f, 28f, "weaponIcon_q_Shotgun_ShortStock.png", "KE", "q_Shotgun_ShortStock.obj");
            a[36] = G(36, "Sniper 1", "Sniper 1", 10, 0, 5, 25, 40f, 0.002f, 36f, 6f, 200f, "weaponIcon_q_SniperRifle_1.png", "KE", "q_SniperRifle_1.obj");
            a[37] = G(37, "Sniper 2", "Sniper 2", 10, 0, 5, 25, 40f, 0.002f, 36f, 6f, 200f, "weaponIcon_q_SniperRifle_2.png", "KE", "q_SniperRifle_2.obj");
            a[38] = G(38, "Sniper 3", "Sniper 3", 10, 0, 5, 25, 40f, 0.002f, 36f, 6f, 200f, "weaponIcon_q_SniperRifle_3.png", "KE", "q_SniperRifle_3.obj");
            a[39] = G(39, "Sniper 4", "Sniper 4", 10, 0, 5, 25, 40f, 0.002f, 36f, 6f, 200f, "weaponIcon_q_SniperRifle_4.png", "KE", "q_SniperRifle_4.obj");
            a[40] = G(40, "Sniper 5", "Sniper 5", 10, 0, 5, 25, 40f, 0.002f, 36f, 6f, 200f, "weaponIcon_q_SniperRifle_5.png", "KE", "q_SniperRifle_5.obj");
            a[41] = G(41, "Sniper 6", "Sniper 6", 10, 0, 5, 25, 40f, 0.002f, 36f, 6f, 200f, "weaponIcon_q_SniperRifle_6.png", "KE", "q_SniperRifle_6.obj");
            a[42] = G(42, "SMG 1", "SMG 1", 3, 1, 30, 150, 800f, 0.016f, 14f, 3f, 45f, "weaponIcon_q_SubmachineGun_1.png", "KE", "q_SubmachineGun_1.obj");
            a[43] = G(43, "SMG 2", "SMG 2", 3, 1, 30, 150, 800f, 0.016f, 14f, 3f, 45f, "weaponIcon_q_SubmachineGun_2.png", "KE", "q_SubmachineGun_2.obj");
            a[44] = G(44, "SMG 3", "SMG 3", 3, 1, 30, 150, 800f, 0.016f, 14f, 3f, 45f, "weaponIcon_q_SubmachineGun_3.png", "KE", "q_SubmachineGun_3.obj");
            a[45] = G(45, "SMG 4", "SMG 4", 3, 1, 30, 150, 800f, 0.016f, 14f, 3f, 45f, "weaponIcon_q_SubmachineGun_4.png", "KE", "q_SubmachineGun_4.obj");
            a[46] = G(46, "SMG 5", "SMG 5", 3, 1, 30, 150, 800f, 0.016f, 14f, 3f, 45f, "weaponIcon_q_SubmachineGun_5.png", "KE", "q_SubmachineGun_5.obj");
            a[47] = G(47, "RBS 70", "RBS 70", 14, 4, 1, 4, 14f, 0.006f, 55f, 15f, 140f, "weaponIcon_rbs70.png", "HE", "rbs70.obj");
            a[48] = G(48, "Revolver", "Revolver", 0, 0, 6, 48, 90f, 0.012f, 32f, 3f, 70f, "weaponIcon_rev_body.png", "KE", "rev_body.obj");
            return a;
        }

        private static GaGun G(int id, string en, string zh, int fam, int fire,
            int mag, int reserve, float rpm, float spread, float dmg, float pen,
            float range, string icon, string kind, string body)
        {
            GaGun x = new GaGun();
            x.Id = id;
            x.En = en;
            x.Zh = zh;
            x.Family = fam;
            x.Fire = fire;
            x.Mag = mag;
            x.Reserve = reserve;
            x.Rpm = rpm;
            x.Spread = spread;
            x.Damage = dmg;
            x.Pen = pen;
            x.Range = range;
            x.Tint = Color.white;
            x.Tracer = new Color(1f, 0.82f, 0.35f);
            x.Icon = icon;
            x.Kind = kind;
            x.Body = body;
            return x;
        }
    }
}
