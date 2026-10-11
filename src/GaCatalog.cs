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
        internal float Vel;
        internal float Drop;
        internal bool HipAssist;
        internal Color Tint;
        internal Color Tracer;
        internal string Icon;
        internal string Kind;
        internal string Body;
        internal string Cal;
        internal int CalId;
        internal int Rank;
    }

    internal static class GaCatalog
    {
        internal const int Count = 51;
        internal const int FamilyCount = 16;
        internal const int HotbarSize = 4;
        internal const int CalCount = 12;

        // CalEn must be initialized before All = Build(); static fields run in order.
        internal static readonly string[] CalEn = new string[]
        {
            "9 mm sk ptr 6",
            "11 mm sk ptr 3",
            "5,56 mm sk ptr 6",
            "7,62 mm sk ptr 8",
            "7,62 mm sk ptr 9",
            "12,7 mm sk ptr 6",
            "12/70 hagel",
            "80 mm pskott",
            "80 mm grg",
            "Rb 8",
            "4,9 mm sk ptr 11",
            "8,6 mm sk ptr 6"
        };

        internal static readonly string[] FamilyEn = new string[]
        {
            "Revolver", "Pistol", "Kpist", "Kpist", "Hagelgev\u00e4r", "Hagelgev\u00e4r", "Ak", "Ak",
            "Ag", "Psg", "Psg", "Ksp", "Grg", "Pskott", "Rb", "Ak"
        };

        internal static readonly GaGun[] All = Build();

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
            a[0] = G(0, "Ak 9", "Ak 9", 7, 1, 30, 150, 600f, 0.013f, 26f, 5f, 400f, "weaponIcon_ak_body.png", "KE", "ak_body.obj");
            a[1] = G(1, "Pskott 6", "Pskott 6", 13, 4, 1, 3, 16f, 0.008f, 80f, 18f, 500f, "weaponIcon_at4.png", "HEAT", "at4.obj");
            a[2] = G(2, "Ak 11", "Ak 11", 15, 2, 50, 200, 620f, 0.006f, 22f, 6f, 520f, "weaponIcon_g11.png", "KE", "g11.obj");
            a[3] = G(3, "Pistol 6", "Pistol 6", 1, 0, 15, 75, 280f, 0.015f, 12f, 2f, 90f, "weaponIcon_pistol_body.png", "KE", "pistol_body.obj");
            a[4] = G(4, "Ak 9B", "Ak 9B", 7, 1, 30, 150, 600f, 0.013f, 26f, 5f, 400f, "weaponIcon_q_AK47.png", "KE", "q_AK47.obj");
            a[5] = G(5, "Ag 8", "Ag 8", 8, 0, 20, 80, 420f, 0.006f, 32f, 6f, 700f, "weaponIcon_q_AssaultRifle2_1.png", "KE", "q_AssaultRifle2_1.obj");
            a[6] = G(6, "Ak 8", "Ak 8", 8, 0, 20, 80, 380f, 0.005f, 36f, 7f, 780f, "weaponIcon_q_AssaultRifle2_2.png", "KE", "q_AssaultRifle2_2.obj");
            a[7] = G(7, "Ak 18", "Ak 18", 8, 1, 20, 100, 560f, 0.009f, 28f, 6f, 620f, "weaponIcon_q_AssaultRifle2_3.png", "KE", "q_AssaultRifle2_3.obj");
            a[8] = G(8, "Ag 8B", "Ag 8B", 8, 0, 10, 50, 340f, 0.0042f, 42f, 7f, 880f, "weaponIcon_q_AssaultRifle2_4.png", "KE", "q_AssaultRifle2_4.obj");
            a[9] = G(9, "Ak 6D", "Ak 6D", 7, 1, 30, 150, 700f, 0.010f, 20f, 4f, 420f, "weaponIcon_q_AssaultRifle_1.png", "KE", "q_AssaultRifle_1.obj");
            a[10] = G(10, "Ak 6C", "Ak 6C", 7, 1, 30, 180, 820f, 0.013f, 17f, 4f, 360f, "weaponIcon_q_AssaultRifle_2.png", "KE", "q_AssaultRifle_2.obj");
            a[11] = G(11, "Ak 14", "Ak 14", 7, 1, 30, 120, 580f, 0.007f, 24f, 5f, 520f, "weaponIcon_q_AssaultRifle_3.png", "KE", "q_AssaultRifle_3.obj");
            a[12] = G(12, "Ak 96", "Ak 96", 7, 2, 30, 150, 900f, 0.006f, 22f, 4f, 450f, "weaponIcon_q_AssaultRifle_4.png", "KE", "q_AssaultRifle_4.obj");
            a[13] = G(13, "Ak 23", "Ak 23", 7, 1, 20, 100, 640f, 0.008f, 23f, 5f, 480f, "weaponIcon_q_AssaultRifle_5.png", "KE", "q_AssaultRifle_5.obj");
            a[14] = G(14, "Ak 78", "Ak 78", 6, 1, 30, 150, 780f, 0.011f, 19f, 4f, 360f, "weaponIcon_q_Bullpup_1.png", "KE", "q_Bullpup_1.obj");
            a[15] = G(15, "Ak 81", "Ak 81", 6, 1, 30, 180, 880f, 0.014f, 16f, 3f, 300f, "weaponIcon_q_Bullpup_2.png", "KE", "q_Bullpup_2.obj");
            a[16] = G(16, "Ak 86", "Ak 86", 6, 1, 25, 125, 700f, 0.0085f, 22f, 5f, 420f, "weaponIcon_q_Bullpup_3.png", "KE", "q_Bullpup_3.obj");
            a[17] = G(17, "Ak 6", "Ak 6", 7, 1, 30, 150, 750f, 0.008f, 20f, 4f, 460f, "weaponIcon_q_M16.png", "KE", "q_M16.obj");
            a[18] = G(18, "Pistol 6B", "Pistol 6B", 1, 0, 15, 75, 300f, 0.016f, 12f, 2f, 90f, "weaponIcon_q_Pistol_1.png", "KE", "q_Pistol_1.obj");
            a[19] = G(19, "Pistol 6C", "Pistol 6C", 1, 0, 17, 85, 380f, 0.019f, 10f, 2f, 75f, "weaponIcon_q_Pistol_2.png", "KE", "q_Pistol_2.obj");
            a[20] = G(20, "Pistol 6D", "Pistol 6D", 1, 0, 12, 60, 220f, 0.011f, 18f, 3f, 120f, "weaponIcon_q_Pistol_3.png", "KE", "q_Pistol_3.obj");
            a[21] = G(21, "Kpist 6", "Kpist 6", 2, 1, 20, 100, 900f, 0.024f, 9f, 2f, 70f, "weaponIcon_q_Pistol_4.png", "KE", "q_Pistol_4.obj");
            a[22] = G(22, "Pistol 6E", "Pistol 6E", 1, 0, 8, 40, 180f, 0.009f, 22f, 3f, 140f, "weaponIcon_q_Pistol_5.png", "KE", "q_Pistol_5.obj");
            a[23] = G(23, "Pistol 6F", "Pistol 6F", 1, 0, 15, 75, 260f, 0.014f, 14f, 2f, 100f, "weaponIcon_q_Pistol_6.png", "KE", "q_Pistol_6.obj");
            a[24] = G(24, "Grg 8", "Grg 8", 13, 4, 1, 4, 20f, 0.012f, 70f, 16f, 1800f, "weaponIcon_q_RPG7.png", "HEAT", "q_RPG7.obj");
            a[25] = G(25, "Revolver 3", "Revolver 3", 0, 0, 6, 36, 90f, 0.014f, 32f, 3f, 85f, "weaponIcon_q_Revolver_1.png", "KE", "q_Revolver_1.obj");
            a[26] = G(26, "Revolver 3B", "Revolver 3B", 0, 0, 6, 30, 70f, 0.011f, 40f, 4f, 110f, "weaponIcon_q_Revolver_2.png", "KE", "q_Revolver_2.obj");
            a[27] = G(27, "Revolver 3C", "Revolver 3C", 0, 0, 6, 42, 120f, 0.017f, 26f, 3f, 70f, "weaponIcon_q_Revolver_3.png", "KE", "q_Revolver_3.obj");
            a[28] = G(28, "Revolver 3D", "Revolver 3D", 0, 0, 5, 25, 55f, 0.009f, 48f, 4f, 130f, "weaponIcon_q_Revolver_4.png", "KE", "q_Revolver_4.obj");
            a[29] = G(29, "Revolver 3E", "Revolver 3E", 0, 0, 6, 36, 80f, 0.013f, 34f, 3f, 95f, "weaponIcon_q_Revolver_5.png", "KE", "q_Revolver_5.obj");
            a[30] = G(30, "Hagelgev\u00e4r 6", "Hagelgev\u00e4r 6", 4, 3, 8, 40, 70f, 0.052f, 11f, 3f, 36f, "weaponIcon_q_Shotgun_1.png", "KE", "q_Shotgun_1.obj");
            a[31] = G(31, "Hagelgev\u00e4r 6B", "Hagelgev\u00e4r 6B", 4, 3, 8, 40, 85f, 0.046f, 10f, 3f, 42f, "weaponIcon_q_Shotgun_2.png", "KE", "q_Shotgun_2.obj");
            a[32] = G(32, "Hagelgev\u00e4r 6C", "Hagelgev\u00e4r 6C", 4, 3, 6, 30, 55f, 0.062f, 13f, 3f, 30f, "weaponIcon_q_Shotgun_3.png", "KE", "q_Shotgun_3.obj");
            a[33] = G(33, "Hagelgev\u00e4r 6D", "Hagelgev\u00e4r 6D", 5, 1, 10, 50, 220f, 0.058f, 8f, 3f, 32f, "weaponIcon_q_Shotgun_4.png", "KE", "q_Shotgun_4.obj");
            a[34] = G(34, "Hagelgev\u00e4r 6E", "Hagelgev\u00e4r 6E", 4, 3, 2, 24, 110f, 0.088f, 9f, 2f, 16f, "weaponIcon_q_Shotgun_SawedOff.png", "KE", "q_Shotgun_SawedOff.obj");
            a[35] = G(35, "Hagelgev\u00e4r 6F", "Hagelgev\u00e4r 6F", 4, 3, 5, 30, 90f, 0.072f, 10f, 2f, 22f, "weaponIcon_q_Shotgun_ShortStock.png", "KE", "q_Shotgun_ShortStock.obj");
            a[36] = G(36, "Psg 6", "Psg 6", 10, 0, 5, 25, 42f, 0.0020f, 36f, 6f, 1400f, "weaponIcon_q_SniperRifle_1.png", "KE", "q_SniperRifle_1.obj");
            a[37] = G(37, "Psg 6B", "Psg 6B", 10, 0, 5, 20, 36f, 0.0015f, 42f, 7f, 1600f, "weaponIcon_q_SniperRifle_2.png", "KE", "q_SniperRifle_2.obj");
            a[38] = G(38, "Psg 8", "Psg 8", 10, 0, 8, 32, 55f, 0.0026f, 30f, 6f, 1100f, "weaponIcon_q_SniperRifle_3.png", "KE", "q_SniperRifle_3.obj");
            a[39] = G(39, "Psg 11", "Psg 11", 10, 0, 4, 16, 26f, 0.0011f, 52f, 8f, 1900f, "weaponIcon_q_SniperRifle_4.png", "KE", "q_SniperRifle_4.obj");
            a[40] = G(40, "Psg 14", "Psg 14", 10, 0, 5, 25, 48f, 0.0022f, 34f, 6f, 1250f, "weaponIcon_q_SniperRifle_5.png", "KE", "q_SniperRifle_5.obj");
            a[41] = G(41, "Psg 16", "Psg 16", 10, 0, 5, 20, 32f, 0.0016f, 46f, 7f, 1750f, "weaponIcon_q_SniperRifle_6.png", "KE", "q_SniperRifle_6.obj");
            a[42] = G(42, "Kpist 6B", "Kpist 6B", 3, 1, 30, 150, 850f, 0.018f, 14f, 3f, 150f, "weaponIcon_q_SubmachineGun_1.png", "KE", "q_SubmachineGun_1.obj");
            a[43] = G(43, "Kpist 6C", "Kpist 6C", 3, 1, 32, 192, 980f, 0.022f, 12f, 3f, 120f, "weaponIcon_q_SubmachineGun_2.png", "KE", "q_SubmachineGun_2.obj");
            a[44] = G(44, "Kpist 6D", "Kpist 6D", 3, 1, 25, 125, 720f, 0.014f, 17f, 3f, 180f, "weaponIcon_q_SubmachineGun_3.png", "KE", "q_SubmachineGun_3.obj");
            a[45] = G(45, "Kpist 6E", "Kpist 6E", 3, 1, 40, 200, 1150f, 0.026f, 10f, 2f, 100f, "weaponIcon_q_SubmachineGun_4.png", "KE", "q_SubmachineGun_4.obj");
            a[46] = G(46, "Kpist 6F", "Kpist 6F", 3, 1, 30, 120, 680f, 0.012f, 18f, 4f, 200f, "weaponIcon_q_SubmachineGun_5.png", "KE", "q_SubmachineGun_5.obj");
            a[47] = G(47, "Rb 8", "Rb 8", 14, 4, 1, 4, 18f, 0.004f, 62f, 16f, 9000f, "weaponIcon_rbs70.png", "HE", "rbs70.obj");
            a[48] = G(48, "Revolver 3F", "Revolver 3F", 0, 0, 6, 36, 85f, 0.012f, 36f, 3f, 95f, "weaponIcon_rev_body.png", "KE", "rev_body.obj");
            a[49] = G(49, "WS-16C", "WS-16C突击炮", 11, 1, 36, 1200, 420f, 0.0035f, 62f, 18f, 12000f, "", "KE", "ws16.obj");
            a[50] = G(50, "Type-77", "77式近战刀", 12, 1, 0, 0, 220f, 0.002f, 110f, 22f, 16f, "", "HEAT", "type77.obj");
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
            ApplyBallistics(x);
            ApplyCaliber(x);
            ApplyRank(x);
            return x;
        }

        internal static string CalLabel(GaGun g)
        {
            if (g == null)
                return "?";
            if (!string.IsNullOrEmpty(g.Cal))
                return g.Cal;
            int id = g.CalId;
            if (id < 0 || id >= CalEn.Length)
                return "?";
            return CalEn[id];
        }

        internal static int MagSizeForCal(int cal)
        {
            for (int i = 0; i < Count; i++)
            {
                if (All[i].CalId == cal && All[i].Mag > 0)
                    return All[i].Mag;
            }
            return 30;
        }

        private static void ApplyCaliber(GaGun x)
        {
            if (x == null)
                return;
            int cal = 2;
            if (x.Family == 0)
                cal = 1;
            else if (x.Family == 1)
            {
                cal = 0;
                if (x.Id == 22)
                    cal = 5;
            }
            else if (x.Family == 2 || x.Family == 3)
                cal = 0;
            else if (x.Family == 4 || x.Family == 5)
                cal = 6;
            else if (x.Family == 6 || x.Family == 7)
            {
                cal = 2;
                if (x.Id == 0 || x.Id == 4)
                    cal = 4;
            }
            else if (x.Family == 8)
                cal = 3;
            else if (x.Family == 10)
            {
                cal = 3;
                if (x.Id == 37 || x.Id == 41)
                    cal = 11;
                else if (x.Id == 38)
                    cal = 4;
                else if (x.Id == 39)
                    cal = 5;
            }
            else if (x.Family == 13)
                cal = x.Body == "at4.obj" ? 7 : 8;
            else if (x.Family == 14)
                cal = 9;
            else if (x.Family == 15)
                cal = 10;
            if (x.Id == 49)
                cal = 5;
            else if (x.Id == 50)
                cal = 8;
            if (cal < 0 || cal >= CalCount)
                cal = 2;
            x.CalId = cal;
            if (CalEn != null && cal >= 0 && cal < CalEn.Length)
                x.Cal = CalEn[cal];
            else
                x.Cal = "";
        }

        private static void ApplyRank(GaGun x)
        {
            if (x == null)
                return;
            int r = 1;
            if (x.Family == 0 || x.Family == 1)
                r = 0;
            else if (x.Family == 4 && x.Fire != 1)
                r = 0;
            else if (x.Id == 17 || x.Id == 10 || x.Id == 9 || x.Id == 42 || x.Id == 48 || x.Id == 3)
                r = 0;
            else if (x.Family == 2 || x.Family == 3 || x.Family == 6 || x.Family == 7)
                r = 1;
            else if (x.Family == 5 || x.Family == 8 || x.Family == 15)
                r = 2;
            else if (x.Family == 10)
            {
                r = 2;
                if (x.Id == 39 || x.Id == 37 || x.Id == 41)
                    r = 3;
            }
            else if (x.Family == 13)
                r = 3;
            else if (x.Family == 14)
                r = 4;
            if (x.Id == 49 || x.Id == 50)
                r = 0;
            x.Rank = r;
        }

        private static void ApplyBallistics(GaGun x)
        {
            if (x == null)
                return;
            float vel = 720f;
            float drop = 0.70f;
            bool hip = x.Family == 6 || x.Family == 7 || x.Family == 8;
            if (x.Family == 0)
            {
                vel = 310f + x.Id * 6f;
                drop = 1.28f;
            }
            else if (x.Family == 1)
            {
                vel = 290f + (x.Id % 9) * 12f;
                drop = 1.38f;
            }
            else if (x.Family == 2)
            {
                vel = 330f;
                drop = 1.42f;
            }
            else if (x.Family == 3)
            {
                vel = 360f + (x.Id % 5) * 14f;
                drop = 1.18f;
            }
            else if (x.Family == 4)
            {
                vel = 390f + (x.Id % 4) * 18f;
                drop = 1.65f;
            }
            else if (x.Family == 5)
            {
                vel = 370f;
                drop = 1.55f;
            }
            else if (x.Family == 6)
            {
                vel = 800f + (x.Id % 3) * 28f;
                drop = 0.68f;
            }
            else if (x.Family == 7)
            {
                vel = 840f + (x.Id % 6) * 22f;
                drop = 0.62f;
            }
            else if (x.Family == 8)
            {
                vel = 790f + (x.Id % 4) * 30f;
                drop = 0.48f;
            }
            else if (x.Family == 9)
            {
                vel = 850f + (x.Id % 5) * 24f;
                drop = 0.36f;
            }
            else if (x.Family == 10)
            {
                vel = 880f + (x.Id % 6) * 48f;
                drop = 0.24f;
            }
            else if (x.Family == 13)
            {
                vel = x.Body == "at4.obj" ? 220f : 185f;
                drop = x.Body == "at4.obj" ? 0.22f : 0.22f;
            }
            else if (x.Family == 14)
            {
                vel = 680f;
                drop = 0.012f;
            }
            else if (x.Family == 15)
            {
                vel = 720f;
                drop = 0.48f;
            }
            if (x.Id == 22)
            {
                vel = 430f;
                drop = 1.05f;
            }
            else if (x.Id == 28)
            {
                vel = 460f;
                drop = 1.02f;
            }
            else if (x.Id == 39)
            {
                vel = 1180f;
                drop = 0.16f;
            }
            else if (x.Id == 37)
            {
                vel = 980f;
                drop = 0.20f;
            }
            else if (x.Id == 17)
            {
                vel = 960f;
                drop = 0.52f;
            }
            else if (x.Id == 4 || x.Id == 0)
            {
                vel = 715f;
                drop = 0.78f;
            }
            else if (x.Id == 45)
            {
                vel = 280f;
                drop = 1.48f;
            }
            else if (x.Id == 49)
            {
                vel = 1180f;
                drop = 0.06f;
            }
            else if (x.Id == 50)
            {
                vel = 70f;
                drop = 2.2f;
            }
            x.Vel = vel;
            x.Drop = drop;
            x.HipAssist = hip;
            if (x.Family == 10)
                x.Tracer = new Color(0.35f, 0.95f, 1f);
            else if (x.Body == "g11.obj")
                x.Tracer = new Color(1f, 0.12f, 0.06f);
            else if (x.Fire == 4)
                x.Tracer = new Color(1f, 0.55f, 0.12f);
            else
            {
                float h = (x.Id * 0.173f) % 1f;
                x.Tracer = Color.HSVToRGB(h, 0.62f, 1f);
            }
        }
    }
}
