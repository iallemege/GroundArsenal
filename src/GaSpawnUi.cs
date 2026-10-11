using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>BF2-style hangar deploy: kit list left, spawn map right.</summary>
    internal static class GaSpawnUi
    {
        private static readonly GaKitId[] Roster = new GaKitId[]
        {
            GaKitId.SpecOps, GaKitId.Sniper, GaKitId.Assault, GaKitId.Support,
            GaKitId.Engineer, GaKitId.Medic, GaKitId.AntiTank, GaKitId.AntiAir,
            GaKitId.Tsf
        };

        private static readonly string[] Glyph = new string[]
        {
            "SO", "SN", "AS", "SU", "EN", "MD", "AT", "AA", "F4"
        };

        private static GUIStyle _title;
        private static GUIStyle _tab;
        private static GUIStyle _tabOn;
        private static GUIStyle _name;
        private static GUIStyle _nameOn;
        private static GUIStyle _tiny;
        private static GUIStyle _ink;
        private static GUIStyle _center;
        private static GUIStyle _done;
        private static GUIStyle _ghost;
        private static bool _styles;
        private static int _tabIx;
        private static int _loadSlot = -1;
        private static Vector2 _gunScroll;
        private static Vector2 _liftScroll;
        private static readonly List<AircraftDefinition> LiftStock = new List<AircraftDefinition>(32);
        private static float _liftAt;
        private static Texture _mapTex;
        private static Rect _mapUv = new Rect(0f, 0f, 1f, 1f);
        private static Vector2 _worldSize;
        private static float _mapAt;
        private static Rect _fit;

        internal static void Draw(float a)
        {
            Ensure();
            GaIcons.Ensure();
            GaCombat.LoadPrefs();
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            bool was = GUI.enabled;
            GUI.enabled = GaHangar.MenuOpen || GaHangar.DeathPick || GaHangar.FieldOpen;
            DrawScreen();
            GUI.enabled = was;
            GUI.color = prev;
        }

        private static void DrawScreen()
        {
            float x0 = 20f;
            float y0 = 42f;
            float x1 = Screen.width - 20f;
            float y1 = Screen.height * 0.91f - 8f;
            if (x1 - x0 < 640f)
            {
                x0 = 8f;
                x1 = Screen.width - 8f;
            }
            Rect title = new Rect(220f, y0, x1 - 220f, 30f);
            Fill(title, new Color(0.16f, 0.20f, 0.14f, 0.94f));
            Frame(title, new Color(0.35f, 0.40f, 0.28f, 0.9f), 1f);
            GUI.Label(title, "SELECT SPAWN", _title);
            float funds = GaFunds.Read();
            GUI.Label(new Rect(title.xMax - 160f, title.y, 150f, 30f),
                funds.ToString("0.0") + "M", _center);

            float leftW = Mathf.Clamp((x1 - x0) * 0.38f, 340f, 480f);
            Rect left = new Rect(x0, y0 + 34f, leftW, y1 - y0 - 34f);
            Rect right = new Rect(left.xMax + 8f, left.y, x1 - left.xMax - 8f, left.height);
            DrawMap(right);
            DrawKitPanel(left);
        }

        private static void DrawKitPanel(Rect r)
        {
            Fill(r, new Color(0.14f, 0.17f, 0.12f, 0.94f));
            Frame(r, new Color(0.32f, 0.36f, 0.24f, 0.95f), 1.5f);
            bool air = GaForce.Kind == GaForceKind.Air;
            if (!air && _tabIx > 1)
                _tabIx = 0;
            int tabs = air ? 3 : 2;
            float tw = (r.width - 20f) / tabs;
            Rect tabA = new Rect(r.x + 8f, r.y + 6f, tw - 4f, 26f);
            Rect tabB = new Rect(tabA.xMax + 4f, tabA.y, tw - 4f, 26f);
            Rect tabC = new Rect(tabB.xMax + 4f, tabA.y, tw - 4f, 26f);
            Fill(tabA, _tabIx == 0 ? new Color(0.42f, 0.40f, 0.22f, 0.95f) : new Color(0.18f, 0.20f, 0.15f, 0.9f));
            Fill(tabB, _tabIx == 1 ? new Color(0.42f, 0.40f, 0.22f, 0.95f) : new Color(0.18f, 0.20f, 0.15f, 0.9f));
            GUI.Label(tabA, "KIT", _tabIx == 0 ? _tabOn : _tab);
            GUI.Label(tabB, "SQUAD", _tabIx == 1 ? _tabOn : _tab);
            if (Click(tabA))
                _tabIx = 0;
            if (Click(tabB))
                _tabIx = 1;
            if (air)
            {
                Fill(tabC, _tabIx == 2 ? new Color(0.42f, 0.40f, 0.22f, 0.95f) : new Color(0.18f, 0.20f, 0.15f, 0.9f));
                GUI.Label(tabC, "LIFT", _tabIx == 2 ? _tabOn : _tab);
                if (Click(tabC))
                    _tabIx = 2;
            }

            Rect body = new Rect(r.x + 6f, r.y + 36f, r.width - 12f, r.height - 44f);
            if (_tabIx == 1)
            {
                DrawSquad(body);
                return;
            }
            if (_tabIx == 2)
            {
                DrawLift(body);
                return;
            }

            float y = body.y;
            Rect pop = new Rect(0f, 0f, 0f, 0f);
            bool havePop = false;
            for (int i = 0; i < Roster.Length; i++)
            {
                GaKitId kit = Roster[i];
                bool sel = GaClass.Picked == kit;
                float h = 64f;
                Rect row = new Rect(body.x, y, body.width, h);
                DrawKitRow(row, kit, Glyph[i], sel);
                if (sel)
                {
                    pop = new Rect(row.xMax - 6f, row.y + 6f, 248f, 78f);
                    havePop = true;
                }
                y += h + 3f;
            }
            if (havePop)
                DrawLoadoutPop(pop);
        }

        private static void DrawKitRow(Rect row, GaKitId kit, string glyph, bool sel)
        {
            Color bg = sel
                ? new Color(0.22f, 0.24f, 0.16f, 0.95f)
                : new Color(0.17f, 0.20f, 0.14f, 0.88f);
            Fill(row, bg);
            if (sel)
                Frame(row, new Color(0.88f, 0.74f, 0.18f, 1f), 2f);
            else
                Frame(row, new Color(0.28f, 0.32f, 0.22f, 0.7f), 1f);

            Rect g = new Rect(row.x + 6f, row.y + 18f, 28f, 28f);
            Fill(g, Color.Lerp(GaClass.Tint(kit), new Color(0.08f, 0.1f, 0.08f), 0.35f));
            GUI.Label(g, glyph, _center);

            GUI.Label(new Rect(row.x + 40f, row.y + 2f, 180f, 20f),
                GaClass.Name(kit).ToUpperInvariant(), sel ? _nameOn : _name);

            int[] guns = GaClass.Guns(kit);
            GaGun primary = FirstGun(guns);
            Texture2D ic = primary != null ? GaIcons.Get(primary) : null;
            Rect img = new Rect(row.x + 40f, row.y + 22f, 168f, 38f);
            if (ic != null)
                GUI.DrawTexture(img, ic, ScaleMode.ScaleToFit);
            else if (primary != null)
                GUI.Label(img, primary.En, _tiny);

            float ix = row.xMax - 22f;
            for (int s = 0; s < 4; s++)
            {
                GaGun sg = SlotGun(guns, s);
                Rect d = new Rect(ix - 16f, row.y + 22f, 16f, 16f);
                Fill(d, new Color(0.12f, 0.14f, 0.1f, 0.9f));
                if (sg != null)
                    GUI.Label(d, (s + 1).ToString(), _tiny);
                ix -= 18f;
            }

            if (Click(row))
            {
                GaClass.Set(kit);
                GaCombat.ApplyClassKit();
                GaCombat.SavePrefs();
                _loadSlot = -1;
            }
        }

        private static void DrawLoadoutPop(Rect pop)
        {
            Fill(pop, new Color(0.82f, 0.82f, 0.76f, 0.96f));
            Frame(pop, new Color(0.12f, 0.12f, 0.1f, 1f), 1.5f);
            int[] guns = GaClass.Guns(GaClass.Picked);
            float slotW = (pop.width - 16f) / 4f;
            for (int s = 0; s < 4; s++)
            {
                Rect cell = new Rect(pop.x + 6f + s * slotW, pop.y + 8f, slotW - 6f, pop.height - 16f);
                bool on = _loadSlot == s;
                Fill(cell, on ? new Color(0.95f, 0.93f, 0.82f, 1f) : new Color(0.72f, 0.72f, 0.68f, 1f));
                Frame(cell, on ? new Color(0.55f, 0.42f, 0.08f, 1f) : new Color(0.35f, 0.35f, 0.32f, 1f), 1f);
                GaGun g = SlotGun(guns, s);
                if (g == null && s < GaCombat.Hotbar.Length)
                    g = GaCatalog.Get(GaCombat.Hotbar[s]);
                Texture2D ic = g != null ? GaIcons.Get(g) : null;
                if (ic != null)
                    GUI.DrawTexture(new Rect(cell.x + 4f, cell.y + 4f, cell.width - 8f, 36f), ic, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(cell.x, cell.y + 2f, cell.width, 14f),
                    (s + 1).ToString() + " " + GaClass.SlotName(s), _tiny);
                GUI.Label(new Rect(cell.x, cell.yMax - 16f, cell.width, 14f),
                    g != null ? g.En : "—", _ink);
                if (Click(cell))
                    _loadSlot = _loadSlot == s ? -1 : s;
            }
            if (_loadSlot >= 0)
                DrawGunPick(new Rect(pop.x, pop.yMax + 4f, pop.width, 168f));
        }

        private static void DrawGunPick(Rect r)
        {
            Fill(r, new Color(0.16f, 0.18f, 0.14f, 0.96f));
            Frame(r, new Color(0.88f, 0.74f, 0.18f, 0.85f), 1f);
            GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 12f, 16f),
                GaClass.SlotName(_loadSlot) + "  [" + (_loadSlot + 1).ToString() + "]", _tiny);
            Rect view = new Rect(r.x + 4f, r.y + 20f, r.width - 8f, r.height - 24f);
            int[] pool = GaClass.SlotPool(GaClass.Picked, _loadSlot);
            int rows = pool != null ? pool.Length : 0;
            float inner = rows * 36f;
            if (inner < view.height)
                inner = view.height;
            _gunScroll = GUI.BeginScrollView(view, _gunScroll, new Rect(0f, 0f, view.width - 18f, inner));
            for (int i = 0; i < rows; i++)
            {
                GaGun g = GaCatalog.Get(pool[i]);
                Rect row = new Rect(0f, i * 36f, view.width - 22f, 34f);
                bool on = _loadSlot < GaCombat.Hotbar.Length && GaCombat.Hotbar[_loadSlot] == g.Id;
                Fill(row, on ? new Color(0.32f, 0.28f, 0.12f, 1f) : new Color(0.12f, 0.14f, 0.1f, 1f));
                Texture2D ic = GaIcons.Get(g);
                if (ic != null)
                    GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, 48f, 30f), ic, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(row.x + 54f, row.y + 8f, row.width - 58f, 18f), g.En, _tiny);
                if (Click(row))
                {
                    if (_loadSlot == 3)
                    {
                        int[] only = GaClass.SlotPool(GaClass.Picked, 3);
                        if (only == null || only.Length == 0 || g.Id != only[0])
                            GaMode.Deny("Class weapon is fixed.", "职业专属武器不能换。");
                        else if (_loadSlot >= 0 && _loadSlot < GaCombat.Hotbar.Length)
                        {
                            GaCombat.Hotbar[_loadSlot] = g.Id;
                            GaCombat.GiveGunAmmo(g);
                            GaCombat.SavePrefs();
                        }
                    }
                    else if (_loadSlot >= 0 && _loadSlot < GaCombat.Hotbar.Length)
                    {
                        GaCombat.Hotbar[_loadSlot] = g.Id;
                        GaCombat.GiveGunAmmo(g);
                        GaCombat.SavePrefs();
                    }
                }
            }
            GUI.EndScrollView();
        }

        private static void DrawSquad(Rect r)
        {
            GUI.Label(new Rect(r.x + 8f, r.y + 8f, r.width - 16f, 48f),
                "Stick of " + (GaDropSquad.FriendCount + 1).ToString()
                    + ". You plus " + GaDropSquad.FriendCount.ToString()
                    + " AI. Kits roll random. AIR jumps the lift together.",
                _tiny);
            float y = r.y + 64f;
            for (int i = 0; i < GaDropSquad.FriendCount; i++)
            {
                Rect row = new Rect(r.x + 8f, y, r.width - 16f, 36f);
                Fill(row, new Color(0.18f, 0.22f, 0.16f, 0.9f));
                GUI.Label(row, "  SLOT " + (i + 2).ToString() + "  —  AI  random kit", _name);
                y += 40f;
            }
        }

        private static void DrawLift(Rect r)
        {
            RefreshLift();
            GUI.Label(new Rect(r.x + 6f, r.y, r.width - 12f, 36f),
                "Half airframe. AI flies it, T jump. Paradrop is free.", _tiny);
            float rowH = 30f;
            int n = LiftStock.Count + 1;
            float innerH = 40f + n * (rowH + 3f);
            Rect view = new Rect(r.x, r.y + 38f, r.width, r.height - 38f);
            Rect inner = new Rect(0f, 0f, view.width - 18f, innerH);
            _liftScroll = GUI.BeginScrollView(view, _liftScroll, inner);
            DrawLiftRow(new Rect(4f, 0f, inner.width - 8f, rowH), null, "PARADROP", 0f,
                GaAirLift.Picked == null);
            float y = rowH + 3f;
            for (int i = 0; i < LiftStock.Count; i++)
            {
                AircraftDefinition d = LiftStock[i];
                if (d == null)
                    continue;
                string lab = LiftLabel(d);
                float half = GaAirLift.HalfCost(d);
                bool sel = object.ReferenceEquals(GaAirLift.Picked, d);
                DrawLiftRow(new Rect(4f, y, inner.width - 8f, rowH), d, lab, half, sel);
                y += rowH + 3f;
            }
            GUI.EndScrollView();
        }

        private static void DrawLiftRow(Rect row, AircraftDefinition d, string lab, float half, bool sel)
        {
            Fill(row, sel ? new Color(0.22f, 0.24f, 0.16f, 0.95f) : new Color(0.17f, 0.20f, 0.14f, 0.88f));
            if (sel)
                Frame(row, new Color(0.88f, 0.74f, 0.18f, 1f), 2f);
            else
                Frame(row, new Color(0.28f, 0.32f, 0.22f, 0.7f), 1f);
            GUI.Label(new Rect(row.x + 8f, row.y, row.width - 88f, row.height), lab, sel ? _nameOn : _name);
            string price = half <= 0.001f ? "0M" : half.ToString("0.00") + "M";
            GUI.Label(new Rect(row.xMax - 84f, row.y, 78f, row.height), price, _center);
            if (Click(row))
                GaAirLift.Picked = d;
        }

        private static string LiftLabel(AircraftDefinition d)
        {
            if (d == null)
                return "PARADROP";
            try
            {
                if (!string.IsNullOrEmpty(d.code) && !string.IsNullOrEmpty(d.unitName))
                    return d.code + "  " + d.unitName;
                if (!string.IsNullOrEmpty(d.unitName))
                    return d.unitName;
                if (!string.IsNullOrEmpty(d.code))
                    return d.code;
            }
            catch { }
            return d.name;
        }

        private static void RefreshLift()
        {
            if (Time.unscaledTime < _liftAt)
                return;
            _liftAt = Time.unscaledTime + 0.45f;
            LiftStock.Clear();
            Airbase ab = GaHangar.PickedBase;
            if (ab == null || ab.disabled)
                ab = GaHangar.FieldAirbase();
            if (ab == null)
                return;
            List<AircraftDefinition> got = null;
            try { got = ab.GetAvailableAircraft(); }
            catch { got = null; }
            if (got != null)
            {
                for (int i = 0; i < got.Count; i++)
                {
                    AircraftDefinition d = got[i];
                    if (d == null)
                        continue;
                    try
                    {
                        if (!ab.CanSpawnAircraft(d))
                            continue;
                    }
                    catch { continue; }
                    LiftStock.Add(d);
                }
            }
            if (LiftStock.Count == 0)
            {
                Encyclopedia enc = null;
                try { enc = Encyclopedia.i; }
                catch { enc = null; }
                if (enc != null && enc.aircraft != null)
                {
                    foreach (AircraftDefinition d in enc.aircraft)
                    {
                        if (d == null)
                            continue;
                        try
                        {
                            if (!ab.CanSpawnAircraft(d))
                                continue;
                        }
                        catch { continue; }
                        LiftStock.Add(d);
                    }
                }
            }
            LiftStock.Sort(CompareLift);
            if (GaAirLift.Picked != null)
            {
                bool keep = false;
                for (int i = 0; i < LiftStock.Count; i++)
                {
                    if (object.ReferenceEquals(LiftStock[i], GaAirLift.Picked))
                    {
                        keep = true;
                        break;
                    }
                }
                if (!keep)
                    GaAirLift.Picked = null;
            }
        }

        private static int CompareLift(AircraftDefinition a, AircraftDefinition b)
        {
            float va = 0f;
            float vb = 0f;
            try { va = a != null ? a.value : 0f; }
            catch { va = 0f; }
            try { vb = b != null ? b.value : 0f; }
            catch { vb = 0f; }
            int c = va.CompareTo(vb);
            if (c != 0)
                return c;
            string na = LiftLabel(a);
            string nb = LiftLabel(b);
            return string.CompareOrdinal(na, nb);
        }

        private static void DrawMap(Rect r)
        {
            Fill(r, new Color(0.10f, 0.12f, 0.10f, 0.94f));
            Frame(r, new Color(0.22f, 0.20f, 0.14f, 1f), 2f);
            Fill(new Rect(r.x, r.y, r.width, 28f), new Color(0.18f, 0.20f, 0.14f, 0.92f));
            GUI.Label(new Rect(r.x + 8f, r.y, 280f, 28f), "SPAWN", _name);

            Rect hole = new Rect(r.x + 10f, r.y + 36f, r.width - 20f, r.height - 132f);
            if (hole.height < 180f)
                hole.height = r.height - 120f;
            Fill(hole, new Color(0.08f, 0.08f, 0.07f, 1f));
            PullMap();
            _fit = FitMap(hole);
            DrawMissionMap(_fit);
            PlotBases(_fit);

            float by = r.yMax - 88f;
            float bw = (r.width - 28f) / 3f;
            DrawForceChip(new Rect(r.x + 10f, by, bw - 4f, 36f), GaForceKind.Land, "LAND");
            DrawForceChip(new Rect(r.x + 10f + bw, by, bw - 4f, 36f), GaForceKind.Sea, "SEA");
            DrawForceChip(new Rect(r.x + 10f + bw * 2f, by, bw - 4f, 36f), GaForceKind.Air, "AIR");

            GUI.Label(new Rect(r.x + 12f, r.yMax - 50f, r.width - 180f, 40f),
                (Time.unscaledTime < GaMode.DenyUntil && !string.IsNullOrEmpty(GaMode.DenyMsg))
                    ? GaMode.DenyMsg : Blurb(), _tiny);

            float kitCost = GaCombat.ClassCostM();
            if (GaForce.Kind == GaForceKind.Air && GaAirLift.Picked != null)
                kitCost += GaAirLift.HalfCost(GaAirLift.Picked);
            float funds = GaFunds.Read();
            string dep = "DEPLOY  " + kitCost.ToString("0.00") + "M";
            if (funds + 0.001f < kitCost)
                dep = "NEED " + kitCost.ToString("0.00") + "M";
            Rect done = new Rect(r.xMax - 150f, r.yMax - 48f, 138f, 38f);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.28f, 0.32f, 0.18f, 1f);
            if (GUI.Button(done, dep, _done))
            {
                GaCombat.SavePrefs();
                if (GaMode.Active && !GaMode.Dead)
                {
                    GaMode.Deny("Cannot deploy — you are still alive.",
                        "无法部署，你还活着。");
                }
                else if (!GaConfig.IsEnabled())
                    GaMode.Deny("Turn infantry ON first.", "Turn infantry ON first.");
                else
                    GaMode.QueueBranchSpawn();
            }
            GUI.backgroundColor = prevBg;
        }

        private static void DrawForceChip(Rect r, GaForceKind k, string lab)
        {
            bool on = GaForce.Kind == k;
            Fill(r, on ? new Color(0.55f, 0.48f, 0.16f, 1f) : new Color(0.22f, 0.24f, 0.16f, 0.95f));
            Frame(r, on ? new Color(0.92f, 0.78f, 0.2f, 1f) : new Color(0.3f, 0.32f, 0.22f, 1f), 1.5f);
            GUI.Label(r, lab, _center);
            if (Click(r))
            {
                GaForce.Set(k);
                if (k == GaForceKind.Air)
                    _tabIx = 2;
            }
        }

        private static void PlotBases(Rect map)
        {
            Airbase[] abs = GaPerfFrameGate.Airbases();
            if (abs == null || abs.Length == 0)
            {
                GUI.Label(map, "No airfields on this mission.", _center);
                return;
            }
            if (_worldSize.x < 8f)
                PullMap();
            FactionHQ hq = GaPerfFrameGate.Hq();
            for (int i = 0; i < abs.Length; i++)
            {
                Airbase ab = abs[i];
                Vector3 p;
                if (!BasePos(ab, out p))
                    continue;
                Vector2 s = WorldToMap(map, p);
                if (s.x < map.x - 4f || s.x > map.xMax + 4f || s.y < map.y - 4f || s.y > map.yMax + 4f)
                    continue;
                bool carrier = GaHangar.MenuAirbaseIsCarrier(ab);
                bool friend = FriendBase(ab, hq);
                bool picked = object.ReferenceEquals(ab, GaHangar.PickedBase);
                Color c;
                if (!friend)
                    c = new Color(0.85f, 0.18f, 0.12f, 0.95f);
                else if (carrier)
                    c = new Color(0.2f, 0.45f, 0.85f, 0.95f);
                else
                    c = new Color(0.2f, 0.72f, 0.28f, 0.95f);
                Rect dot = new Rect(s.x - 8f, s.y - 8f, 16f, 16f);
                Fill(dot, c);
                if (picked)
                    Frame(new Rect(dot.x - 3f, dot.y - 3f, 22f, 22f), new Color(0.92f, 0.78f, 0.18f, 1f), 2f);
                string lab = BaseName(ab);
                if (friend && !string.IsNullOrEmpty(lab))
                {
                    Rect nr = new Rect(s.x + 10f, s.y - 9f, 140f, 18f);
                    Fill(nr, new Color(0.04f, 0.05f, 0.04f, 0.72f));
                    GUI.Label(nr, lab, _tiny);
                }
                if (friend && Click(new Rect(dot.x - 8f, dot.y - 8f, 32f, 32f)))
                {
                    GaHangar.PickBase(ab);
                    if (carrier)
                        GaForce.Set(GaForceKind.Sea);
                    else if (GaForce.Kind == GaForceKind.Sea)
                        GaForce.Set(GaForceKind.Land);
                }
            }
        }

        private static void DrawMissionMap(Rect map)
        {
            if (!PullMap() || _mapTex == null)
            {
                Fill(map, new Color(0.32f, 0.30f, 0.22f, 1f));
                GUI.Label(map, "Waiting for mission map…", _center);
                return;
            }
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, GUI.color.a);
            GUI.DrawTextureWithTexCoords(map, _mapTex, _mapUv);
            GUI.color = prev;
            Frame(map, new Color(0.08f, 0.08f, 0.06f, 0.9f), 1f);
        }

        private static Rect FitMap(Rect hole)
        {
            float aspect = 1f;
            if (_worldSize.x > 8f && _worldSize.y > 8f)
                aspect = _worldSize.x / _worldSize.y;
            else if (_mapTex != null && _mapTex.height > 0)
                aspect = (float)_mapTex.width / (float)_mapTex.height;
            float rw = hole.width;
            float rh = hole.height;
            if (rw / rh > aspect)
            {
                float w = rh * aspect;
                return new Rect(hole.x + (rw - w) * 0.5f, hole.y, w, rh);
            }
            float h = rw / aspect;
            return new Rect(hole.x, hole.y + (rh - h) * 0.5f, rw, h);
        }

        private static bool PullMap()
        {
            if (Time.unscaledTime < _mapAt && _mapTex != null && _worldSize.x > 8f)
                return true;
            _mapAt = Time.unscaledTime + 0.7f;
            MapSettings ms = null;
            try
            {
                LevelInfo li = NetworkSceneSingleton<LevelInfo>.i;
                if (li != null)
                    ms = li.LoadedMapSettings;
            }
            catch { }
            if (ms == null)
            {
                try { ms = UnityEngine.Object.FindObjectOfType<MapSettings>(); }
                catch { }
            }
            if (ms == null)
                return _mapTex != null && _worldSize.x > 8f;
            try
            {
                if (ms.MapSize.x > 8f)
                    _worldSize = ms.MapSize;
            }
            catch { }
            try
            {
                Sprite sp = ms.MapImage;
                if (sp != null && sp.texture != null)
                {
                    _mapTex = sp.texture;
                    Rect tr = sp.textureRect;
                    float tw = _mapTex.width;
                    float th = _mapTex.height;
                    if (tw > 1f && th > 1f)
                        _mapUv = new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
                    else
                        _mapUv = new Rect(0f, 0f, 1f, 1f);
                }
                else if (ms.TerrainColorMap != null)
                {
                    _mapTex = ms.TerrainColorMap;
                    _mapUv = new Rect(0f, 0f, 1f, 1f);
                }
            }
            catch { }
            return _mapTex != null && _worldSize.x > 8f;
        }

        private static Vector3 WorldOf(Airbase ab)
        {
            Transform t = null;
            try { t = ab.center != null ? ab.center : ab.transform; }
            catch { t = ab.transform; }
            if (t == null)
                return Vector3.zero;
            try
            {
                GlobalPosition g = t.GlobalPosition();
                Vector3 v = g.AsVector3();
                if (v.sqrMagnitude > 1f || t.position.sqrMagnitude < 1f)
                    return v;
            }
            catch { }
            return t.position;
        }

        private static bool BasePos(Airbase ab, out Vector3 p)
        {
            p = Vector3.zero;
            if (ab == null || ab.disabled)
                return false;
            try { p = WorldOf(ab); }
            catch { return false; }
            return true;
        }

        private static string BaseName(Airbase ab)
        {
            try
            {
                if (ab.SavedAirbase != null && !string.IsNullOrEmpty(ab.SavedAirbase.DisplayName))
                    return ab.SavedAirbase.DisplayName;
            }
            catch { }
            return ab != null ? ab.name : "";
        }

        private static Vector2 WorldToMap(Rect map, Vector3 g)
        {
            float sx = _worldSize.x;
            float sz = _worldSize.y;
            if (sx < 8f) sx = 1f;
            if (sz < 8f) sz = 1f;
            float u = (g.x + sx * 0.5f) / sx;
            float v = (g.z + sz * 0.5f) / sz;
            return new Vector2(map.x + u * map.width, map.yMax - v * map.height);
        }

        private static bool FriendBase(Airbase ab, FactionHQ hq)
        {
            if (hq == null || ab == null)
                return true;
            try
            {
                return ab.CurrentHQ == null || object.ReferenceEquals(ab.CurrentHQ, hq);
            }
            catch
            {
                return true;
            }
        }

        private static string Blurb()
        {
            string field = "";
            if (GaHangar.PickedBase != null)
                field = "  " + BaseName(GaHangar.PickedBase);
            if (GaForce.Kind == GaForceKind.Land)
                return "LAND" + field + "  airfield. Ride vehicles. No aircraft buy.";
            if (GaForce.Kind == GaForceKind.Sea)
                return "SEA" + field + "  carrier deck. Buy aircraft with B.";
            if (GaAirLift.Picked != null)
                return "AIR" + field + "  " + GaAirLift.PickName()
                    + "  half hull " + GaAirLift.HalfCost(GaAirLift.Picked).ToString("0.00")
                    + "M. T jump.";
            return "AIR" + field + "  paradrop. SPACE open chute. LIFT tab picks a plane.";
        }

        private static GaGun FirstGun(int[] guns)
        {
            return SlotGun(guns, 0);
        }

        private static GaGun SlotGun(int[] guns, int slot)
        {
            if (guns == null || slot < 0 || slot >= guns.Length)
                return null;
            int id = guns[slot];
            if (id < 0)
                return null;
            return GaCatalog.Get(id);
        }

        private static bool Click(Rect r)
        {
            return GUI.Button(r, GUIContent.none, _ghost);
        }

        private static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * GUI.color.a);
            GUI.DrawTexture(r, GaFxUi.Px);
            GUI.color = prev;
        }

        private static void Frame(Rect r, Color c, float th)
        {
            Fill(new Rect(r.x, r.y, r.width, th), c);
            Fill(new Rect(r.x, r.yMax - th, r.width, th), c);
            Fill(new Rect(r.x, r.y, th, r.height), c);
            Fill(new Rect(r.xMax - th, r.y, th, r.height), c);
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _title = new GUIStyle(GUI.skin.label);
            _title.fontSize = 16;
            _title.fontStyle = FontStyle.Bold;
            _title.alignment = TextAnchor.MiddleCenter;
            _title.normal.textColor = new Color(0.92f, 0.93f, 0.82f);
            _tab = new GUIStyle(GUI.skin.label);
            _tab.fontSize = 13;
            _tab.fontStyle = FontStyle.Bold;
            _tab.alignment = TextAnchor.MiddleCenter;
            _tab.normal.textColor = new Color(0.7f, 0.72f, 0.6f);
            _tabOn = new GUIStyle(_tab);
            _tabOn.normal.textColor = new Color(0.98f, 0.95f, 0.78f);
            _name = new GUIStyle(GUI.skin.label);
            _name.fontSize = 13;
            _name.fontStyle = FontStyle.Bold;
            _name.alignment = TextAnchor.MiddleLeft;
            _name.normal.textColor = new Color(0.88f, 0.9f, 0.8f);
            _nameOn = new GUIStyle(_name);
            _nameOn.normal.textColor = new Color(0.98f, 0.86f, 0.28f);
            _tiny = new GUIStyle(GUI.skin.label);
            _tiny.fontSize = 11;
            _tiny.wordWrap = true;
            _tiny.alignment = TextAnchor.MiddleCenter;
            _tiny.clipping = TextClipping.Clip;
            _tiny.normal.textColor = new Color(0.82f, 0.84f, 0.72f);
            _ink = new GUIStyle(_tiny);
            _ink.normal.textColor = new Color(0.12f, 0.12f, 0.1f);
            _center = new GUIStyle(GUI.skin.label);
            _center.fontSize = 13;
            _center.fontStyle = FontStyle.Bold;
            _center.alignment = TextAnchor.MiddleCenter;
            _center.normal.textColor = new Color(0.95f, 0.95f, 0.88f);
            _done = new GUIStyle(GUI.skin.button);
            _done.fontSize = 16;
            _done.fontStyle = FontStyle.Bold;
            _done.alignment = TextAnchor.MiddleCenter;
            _done.normal.textColor = new Color(0.95f, 0.95f, 0.85f);
            _ghost = new GUIStyle();
            _ghost.fixedHeight = 0f;
            _ghost.stretchHeight = true;
        }
    }
}
