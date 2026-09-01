using System;
using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>
    /// Live encyclopedia dump: every WeaponInfo pen grade and every unit facing armor.
    /// Open with ' while infantry, or from the hangar Weapons menu.
    /// </summary>
    internal static class ArmorCatalogService
    {
        private class WepRow
        {
            public string Name;
            public string Kind;
            public int Pen;
            public int CalMm;
            public float Pierce;
        }

        private class UnitRow
        {
            public string Name;
            public string Kind;
            public int Front, Side, Rear, Top, Bottom;
            public float Tier;
        }

        private static readonly List<WepRow> Weapons = new List<WepRow>(256);
        private static readonly List<UnitRow> Units = new List<UnitRow>(256);
        private static bool _scanned;
        private static Vector2 _scroll;
        private static int _tab;
        private static GUIStyle _head;
        private static GUIStyle _cell;
        private static bool _styles;

        internal static void EnsureScanned()
        {
            if (_scanned && Weapons.Count > 0)
                return;
            Scan();
        }

        internal static void Draw()
        {
            EnsureStyles();
            EnsureScanned();
            float w = Mathf.Min(920f, Screen.width - 40f);
            float h = Mathf.Min(560f, Screen.height - 80f);
            Rect box = new Rect((Screen.width - w) * 0.5f, 40f, w, h);
            GUI.Box(box, "");
            GUILayout.BeginArea(new Rect(box.x + 10f, box.y + 8f, box.width - 20f, box.height - 16f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("AP / ARMOR  0–20", GUILayout.Width(260f));
            if (GUILayout.Button("Weapons", GUILayout.Width(100f)))
                _tab = 0;
            if (GUILayout.Button("Vehicles", GUILayout.Width(100f)))
                _tab = 1;
            if (GUILayout.Button("Facings", GUILayout.Width(120f)))
                _tab = 2;
            if (GUILayout.Button("Close (')", GUILayout.Width(110f)))
                GaHud.CatalogOpen = false;
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_tab == 0)
                DrawWeapons();
            else if (_tab == 1)
                DrawUnits();
            else
                DrawFacings();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void DrawWeapons()
        {
            GUILayout.Label("Infantry guns first (fixed pen + KE), then every encyclopedia WeaponInfo. KE rifles are blast 0. Pen 0–20 (nukes = 11). Live hits roll ±1. Armor fatigues. Vehicle / ship / air facings use the same table.", _cell);
            Header(new string[] { "Name", "Kind", "Pen", "KE", "Pierce" });
            for (int i = 0; i < GaCatalog.Count; i++)
            {
                GaGun g = GaCatalog.All[i];
                Row(new string[]
                {
                    g.En,
                    g.Kind,
                    Mathf.RoundToInt(g.Pen).ToString(),
                    Mathf.RoundToInt(GaCombat.Kinetic(g)).ToString(),
                    GaCombat.PierceHp(g).ToString("0.0")
                }, (i % 2) == 0);
            }
            for (int i = 0; i < Weapons.Count; i++)
            {
                WepRow r = Weapons[i];
                Row(new string[]
                {
                    r.Name,
                    r.Kind,
                    r.Pen.ToString(),
                    "-",
                    r.Pierce.ToString("0")
                }, (i % 2) == 0);
            }
        }

        private static void DrawUnits()
        {
            GUILayout.Label("Facing grades after vanilla armorTier. Fatigue drops effective plate up to 3 grades. Infantry: outer 10 over body 2. Glance/bounce still deal a damage floor. Modules: turret +2, gun -2, engine -2, tracks -4, ammo -1, fuel -3, sensors -6, command -2.", _cell);
            Header(new string[] { "Unit", "Kind", "F", "S", "R", "T", "B", "Tier" });
            Row(new string[] { "Infantry plate", "outer", "10", "10", "10", "10", "10", "-" }, true);
            Row(new string[] { "Infantry body", "inner", "2", "2", "2", "2", "2", "-" }, false);
            for (int i = 0; i < Units.Count; i++)
            {
                UnitRow r = Units[i];
                Row(new string[]
                {
                    r.Name,
                    r.Kind,
                    r.Front.ToString(),
                    r.Side.ToString(),
                    r.Rear.ToString(),
                    r.Top.ToString(),
                    r.Bottom.ToString(),
                    r.Tier.ToString("0")
                }, (i % 2) == 0);
            }
        }

        private static void DrawFacings()
        {
            GUILayout.Label("Base facing by class. Every shot rolls pen ±1. KE guns overmatch at pen-armor >= 1, missiles >= 2, HEAT/bombs >= 4. Overpen chains KE into an interior module. Nukes catalog pen 11.", _cell);
            Header(new string[] { "Class", "Front", "Side", "Rear", "Top", "Bottom" });
            FaceRow("Infantry", GaArmorMath.KindInfantry);
            FaceRow("Building", GaArmorMath.KindBuilding);
            FaceRow("Fighter", GaArmorMath.KindFighter);
            FaceRow("Attack", GaArmorMath.KindAttack);
            FaceRow("Bomber", GaArmorMath.KindBomber);
            FaceRow("Helo", GaArmorMath.KindHelo);
            FaceRow("MBT", GaArmorMath.KindMbt);
            FaceRow("IFV", GaArmorMath.KindIfv);
            FaceRow("Soft / truck", GaArmorMath.KindSoft);
            FaceRow("Warship", GaArmorMath.KindWarship);
            FaceRow("Escort", GaArmorMath.KindEscort);
            FaceRow("Carrier", GaArmorMath.KindCarrier);
            FaceRow("Boat", GaArmorMath.KindBoat);
            FaceRow("Super-heavy air", GaArmorMath.KindSuperHeavy);
            GUILayout.Space(8f);
            Header(new string[] { "Cannon mm", "Pen" });
            Row(new string[] { "≤20", "6" }, true);
            Row(new string[] { "23", "7" }, false);
            Row(new string[] { "25", "8" }, true);
            Row(new string[] { "27", "9" }, false);
            Row(new string[] { "30–35", "10" }, true);
            Row(new string[] { "40", "12" }, false);
            Row(new string[] { "57", "16" }, true);
            Row(new string[] { "76", "17" }, false);
            Row(new string[] { "105", "18" }, true);
            Row(new string[] { "125+", "19–20" }, false);
            Header(new string[] { "MG mm", "Pen" });
            Row(new string[] { "≤8 / 7.62", "2" }, true);
            Row(new string[] { "12.7 / .50", "4" }, false);
            Row(new string[] { "14.5", "5" }, true);
        }

        private static void FaceRow(string name, int kind)
        {
            int f = GaArmorMath.BaseGrade(kind, GaArmorMath.FaceFront);
            int s = GaArmorMath.BaseGrade(kind, GaArmorMath.FaceSide);
            int r = GaArmorMath.BaseGrade(kind, GaArmorMath.FaceRear);
            int t = GaArmorMath.BaseGrade(kind, GaArmorMath.FaceTop);
            int b = GaArmorMath.BaseGrade(kind, GaArmorMath.FaceBottom);
            Row(new string[] { name, f.ToString(), s.ToString(), r.ToString(), t.ToString(), b.ToString() }, false);
        }

        internal static void Scan()
        {
            Weapons.Clear();
            Units.Clear();
            try
            {
                WeaponInfo[] infos = Resources.FindObjectsOfTypeAll<WeaponInfo>();
                if (infos != null)
                {
                    Dictionary<string, bool> seen = new Dictionary<string, bool>();
                    for (int i = 0; i < infos.Length; i++)
                    {
                        WeaponInfo w = infos[i];
                        if (w == null)
                            continue;
                        try
                        {
                            if (w.hideInDisplay)
                                continue;
                        }
                        catch { }
                        string name = w.weaponName;
                        if (string.IsNullOrEmpty(name))
                            name = w.shortName;
                        if (string.IsNullOrEmpty(name))
                            name = w.name;
                        if (string.IsNullOrEmpty(name) || seen.ContainsKey(name))
                            continue;
                        seen[name] = true;
                        WepRow row = new WepRow();
                        row.Name = name;
                        int cls = GaArmorMath.ClassifyKinetic(w);
                        row.Kind = GaArmorMath.IsNuclearWeapon(w)
                            ? "nuke"
                            : (GaArmorMath.IsKeNamed(w)
                                ? "KE replica"
                                : WepKindLabel(cls));
                        row.Pierce = w.pierceDamage;
                        row.CalMm = GaArmorMath.ParseCaliberMm(GaArmorMath.WeaponBlob(w));
                        row.Pen = GaArmorMath.PenGradeForWeapon(w, w.pierceDamage);
                        Weapons.Add(row);
                    }
                }
            }
            catch { }

            Encyclopedia enc = null;
            try { enc = Encyclopedia.i; }
            catch { enc = null; }
            if (enc != null)
            {
                AddDefs(enc.vehicles, "veh");
                AddDefs(enc.ships, "ship");
                AddDefs(enc.aircraft, "air");
            }
            _scanned = true;
        }

        private static void AddDefs(System.Collections.IList list, string hint)
        {
            if (list == null)
                return;
            for (int i = 0; i < list.Count; i++)
            {
                UnitDefinition def = list[i] as UnitDefinition;
                if (def == null)
                    continue;
                string name = def.unitName;
                if (string.IsNullOrEmpty(name))
                    name = def.code;
                if (string.IsNullOrEmpty(name))
                    name = def.jsonKey;
                if (string.IsNullOrEmpty(name))
                    continue;
                int kind = KindFromDef(def, hint);
                int tier = 0;
                try { tier = Mathf.RoundToInt(def.armorTier); }
                catch { }
                UnitRow row = new UnitRow();
                row.Name = name;
                row.Kind = KindLabel(kind);
                row.Front = GaArmorMath.ArmorGrade(kind, GaArmorMath.FaceFront, tier);
                row.Side = GaArmorMath.ArmorGrade(kind, GaArmorMath.FaceSide, tier);
                row.Rear = GaArmorMath.ArmorGrade(kind, GaArmorMath.FaceRear, tier);
                row.Top = GaArmorMath.ArmorGrade(kind, GaArmorMath.FaceTop, tier);
                row.Bottom = GaArmorMath.ArmorGrade(kind, GaArmorMath.FaceBottom, tier);
                row.Tier = def.armorTier;
                Units.Add(row);
            }
        }

        private static int KindFromDef(UnitDefinition def, string hint)
        {
            VehicleDefinition vd = def as VehicleDefinition;
            if (vd != null)
            {
                try
                {
                    VehicleType t = vd.vehicleType;
                    if (t == VehicleType.MBT)
                        return GaArmorMath.KindMbt;
                    if (t == VehicleType.AFV)
                        return GaArmorMath.KindIfv;
                    if (t == VehicleType.TRUCK || t == VehicleType.UGV || t == VehicleType.LCV
                        || t == VehicleType.RDR)
                        return GaArmorMath.KindSoft;
                    return GaArmorMath.KindIfv;
                }
                catch { }
            }
            string blob = (def.unitName != null ? def.unitName : "") + " "
                + (def.code != null ? def.code : "") + " "
                + (def.jsonKey != null ? def.jsonKey : "") + " " + hint;
            if (hint == "ship")
            {
                if (GaArmorMath.NameHas(blob, "carrier"))
                    return GaArmorMath.KindCarrier;
                if (GaArmorMath.NameHas(blob, "frigate") || GaArmorMath.NameHas(blob, "destroyer"))
                    return GaArmorMath.KindEscort;
                return GaArmorMath.KindWarship;
            }
            if (hint == "air")
            {
                if (GaArmorMath.NameHas(blob, "heli") || GaArmorMath.NameHas(blob, "uh-")
                    || GaArmorMath.NameHas(blob, "sah"))
                    return GaArmorMath.KindHelo;
                if (GaArmorMath.NameHas(blob, "bomber") || GaArmorMath.NameHas(blob, "sfb"))
                    return GaArmorMath.KindBomber;
                return GaArmorMath.KindFighter;
            }
            return GaArmorMath.KindSoft;
        }

        private static string WepKindLabel(int cls)
        {
            if (cls == GaArmorMath.WepMg) return "MG";
            if (cls == GaArmorMath.WepCannon) return "cannon";
            if (cls == GaArmorMath.WepRocket) return "rocket";
            if (cls == GaArmorMath.WepMissile) return "missile";
            if (cls == GaArmorMath.WepBomb) return "bomb";
            if (cls == GaArmorMath.WepEnergy) return "energy";
            return "other";
        }

        private static string KindLabel(int kind)
        {
            if (kind == GaArmorMath.KindMbt) return "MBT";
            if (kind == GaArmorMath.KindIfv) return "IFV";
            if (kind == GaArmorMath.KindSoft) return "soft";
            if (kind == GaArmorMath.KindWarship) return "warship";
            if (kind == GaArmorMath.KindEscort) return "escort";
            if (kind == GaArmorMath.KindCarrier) return "carrier";
            if (kind == GaArmorMath.KindBoat) return "boat";
            if (kind == GaArmorMath.KindFighter) return "fighter";
            if (kind == GaArmorMath.KindAttack) return "attack";
            if (kind == GaArmorMath.KindBomber) return "bomber";
            if (kind == GaArmorMath.KindHelo) return "helo";
            if (kind == GaArmorMath.KindSuperHeavy) return "super";
            if (kind == GaArmorMath.KindInfantry) return "infantry";
            if (kind == GaArmorMath.KindBuilding) return "building";
            return "?";
        }

        private static void Header(string[] cols)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < cols.Length; i++)
                GUILayout.Label(cols[i], _head, GUILayout.Width(i == 0 ? 220f : 70f));
            GUILayout.EndHorizontal();
        }

        private static void Row(string[] cols, bool dim)
        {
            GUILayout.BeginHorizontal();
            Color prev = GUI.color;
            if (dim)
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
            for (int i = 0; i < cols.Length; i++)
                GUILayout.Label(cols[i], _cell, GUILayout.Width(i == 0 ? 220f : 70f));
            GUI.color = prev;
            GUILayout.EndHorizontal();
        }

        private static void EnsureStyles()
        {
            if (_styles)
                return;
            _styles = true;
            _head = new GUIStyle(GUI.skin.label);
            _head.fontStyle = FontStyle.Bold;
            _head.fontSize = 12;
            _cell = new GUIStyle(GUI.skin.label);
            _cell.fontSize = 11;
            _cell.wordWrap = true;
        }
    }
}
