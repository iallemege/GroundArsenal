using System.Collections.Generic;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>Battlefield-style extras: stance, grenades, spot, hitmarker, killfeed, bleed-out.</summary>
    internal static class GaFps
    {
        internal static int Stance;
        internal static int Nades;
        internal static int NadeMax = 2;

        internal static string StanceLabel()
        {
            if (Stance == 2)
                return "PRONE";
            if (Stance == 1)
                return "CROUCH";
            return "STAND";
        }

        internal static float RescueAmt;
        private static float _hitUntil;
        private static bool _hitKill;
        private static float _reviveHold;
        private static float _medicHold;
        private static int _medicId;
        private static float _meleeUntil;
        private static int _fitStance = -99;
        private static int _nadeKey = -1;
        private static string _nadeLine = "";
        private static GaDropBot _lookDown;
        private static int _lookFrame = -1;
        private static readonly List<Nade> NadeList = new List<Nade>(4);
        private static readonly List<Feed> Feeds = new List<Feed>(8);
        private static readonly List<Mark> Marks = new List<Mark>(8);
        private static Texture2D _px;
        private static GUIStyle _feed;
        private static GUIStyle _hint;
        private static GUIStyle _bleed;
        private static bool _styles;

        private struct Nade
        {
            public Vector3 P;
            public Vector3 V;
            public float Fuse;
        }

        private struct Feed
        {
            public string Text;
            public float Dies;
        }

        private struct Mark
        {
            public Vector3 P;
            public float Dies;
            public bool Enemy;
            public string Label;
        }

        internal static void Clear()
        {
            Stance = 0;
            _fitStance = -99;
            NadeList.Clear();
            Marks.Clear();
            _reviveHold = 0f;
            _medicHold = 0f;
            RescueAmt = 0f;
        }

        internal static void OnSpawn()
        {
            Clear();
            Feeds.Clear();
            FillNades();
        }

        internal static void FillNades()
        {
            NadeMax = 2;
            if (GaClass.Picked == GaKitId.Support)
                NadeMax = 4;
            else if (GaClass.Picked == GaKitId.SpecOps)
                NadeMax = 3;
            else if (GaClass.Picked == GaKitId.Assault)
                NadeMax = 3;
            Nades = 0;
        }

        internal static string Hint()
        {
            if (GaMode.Downed)
                return null;
            GaDropBot down = CachedDowned();
            if (down != null && GaClass.Picked == GaKitId.Medic)
                return "HOLD F  REVIVE  " + down.Callsign;
            if (GaClass.Picked == GaKitId.SpecOps)
            {
                string jet = GaJet.Hint();
                if (!string.IsNullOrEmpty(jet))
                    return jet;
            }
            if (GaClass.Picked == GaKitId.Tsf)
            {
                string tsf = GaTsf.Hint();
                if (!string.IsNullOrEmpty(tsf))
                    return tsf;
            }
            string laser = GaAtLaser.Hint();
            if (!string.IsNullOrEmpty(laser))
                return laser;
            string ammo = GaClass.AmmoHint();
            if (!string.IsNullOrEmpty(ammo))
                return ammo;
            if (Stance == 2)
                return "PRONE  Z stand   C crouch";
            return null;
        }

        internal static void Tick()
        {
            if (!GaMode.Active)
                return;
            TickStance();
            TickNades();
            TickRevive();
            TickSpotKey();
            Prune();
        }

        internal static void TickDowned()
        {
            TickRevive();
            Prune();
        }

        internal static void ApplyMove(ref float speed, ref bool sprint, CharacterController cc, Transform eye)
        {
            if (Stance == 1)
            {
                speed *= 0.55f;
                sprint = false;
            }
            else if (Stance == 2)
            {
                speed *= 0.28f;
                sprint = false;
            }
            Fit(cc, eye);
        }

        internal static bool BlocksJump()
        {
            if (Stance == 0)
                return false;
            Stance = 0;
            return true;
        }

        internal static void HitMarker(bool killed)
        {
            _hitUntil = Time.unscaledTime + 0.18f;
            _hitKill = killed;
        }

        internal static void KillFeed(string killer, string victim)
        {
            if (string.IsNullOrEmpty(killer))
                killer = "?";
            if (string.IsNullOrEmpty(victim))
                victim = "?";
            Feed f;
            f.Text = killer + "  >  " + victim;
            f.Dies = Time.unscaledTime + 4.5f;
            Feeds.Add(f);
            while (Feeds.Count > 6)
                Feeds.RemoveAt(0);
        }

        internal static void NotePlayerShot(RaycastHit hit)
        {
            if (hit.collider == null)
                return;
            GaDropBot bot = GaPhys.Bot(hit.collider);
            if (bot == null)
                return;
            bot.HurtByPlayer = true;
            bool dead = bot.Hp <= 0f || bot.Downed;
            HitMarker(dead);
        }

        internal static void SpotFromEye()
        {
            if (GaAtLaser.BlocksQ)
            {
                GaMode.Deny("DESIGNATOR LOCKED", "指示器锁定中");
                return;
            }
            if (GaAtLaser.TryDesignate())
                return;
            Transform eye = GaMode.Eye;
            if (eye == null)
                return;
            RaycastHit hit;
            bool got = Physics.Raycast(eye.position, eye.forward, out hit, 8000f,
                GaPhys.WorldMask, QueryTriggerInteraction.Ignore);
            if (!got)
            {
                Vector3 far = eye.position + eye.forward * 900f;
                got = GaPhys.RayDown(far, 2500f, out hit);
            }
            if (!got)
            {
                GaRadio.SpotCall();
                return;
            }
            GaDropBot bot = GaPhys.Bot(hit.collider);
            string label = bot != null ? bot.Callsign : "Q MARK";
            bool enemy = bot == null || !bot.Friendly;
            Vector3 p = hit.point;
            if (bot != null)
                p = bot.transform.position + Vector3.up * 1.8f;
            PlaceMark(p, enemy, label);
            GaRadio.SpotCall();
        }

        internal static void ClearMark()
        {
            Marks.Clear();
        }

        internal static void PlaceMark(Vector3 p, bool enemy, string label)
        {
            Mark m;
            m.P = p;
            m.Dies = Time.unscaledTime + 240f;
            m.Enemy = enemy;
            m.Label = string.IsNullOrEmpty(label) ? "Q MARK" : label;
            Marks.Clear();
            Marks.Add(m);
        }

        internal static void RefreshMark(Vector3 p)
        {
            if (Marks.Count == 0)
                return;
            Mark m = Marks[0];
            m.P = p;
            m.Dies = Time.unscaledTime + 4f;
            Marks[0] = m;
        }

        internal static bool TryGetMark(out Vector3 p)
        {
            PruneMarks();
            if (Marks.Count == 0)
            {
                p = Vector3.zero;
                return false;
            }
            p = Marks[0].P;
            return true;
        }

        private static void PruneMarks()
        {
            float now = Time.unscaledTime;
            for (int i = Marks.Count - 1; i >= 0; i--)
            {
                if (now >= Marks[i].Dies)
                    Marks.RemoveAt(i);
            }
        }

        internal static void Melee()
        {
            if (!GaMode.Active || GaMode.Dead || GaMode.Downed)
                return;
            if (Time.time < _meleeUntil)
                return;
            _meleeUntil = Time.time + 0.55f;
            Transform eye = GaMode.Eye;
            if (eye == null)
                return;
            RaycastHit hit;
            float reach = GaCombat.HeldId == 50 ? 16f : 2.4f;
            if (!Physics.Raycast(eye.position, eye.forward, out hit, reach, GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
                return;
            GaDropBot bot = GaPhys.Bot(hit.collider);
            if (bot != null && !bot.Friendly)
            {
                bot.HurtByPlayer = true;
                float slash = GaCombat.HeldId == 50 ? 90f : 22f;
                bot.ApplyDamage(slash, 0f, 0f, slash * 0.35f);
                HitMarker(bot.Hp <= 0f || bot.Downed);
                return;
            }
            IDamageable dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                try { dmg.ApplyDamage(14f, 0f, 0f, 6f); }
                catch { }
                HitMarker(false);
            }
        }

        internal static void Draw()
        {
            if (!GaPerfFrameGate.Paint())
                return;
            Ensure();
            DrawHit();
            DrawFeed();
            DrawMarks();
            DrawBleed();
        }

        private static void TickStance()
        {
            if (GaHud.BlocksLook || GaRide.Busy || GaMode.Downed)
                return;
            if (GaJet.On || GaTsf.Jumping || GaClass.Picked == GaKitId.Tsf)
            {
                Stance = 0;
                return;
            }
            if (Input.GetKeyDown(KeyCode.Z))
                Stance = Stance == 2 ? 0 : 2;
            bool crouch = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Stance != 2)
                Stance = crouch ? 1 : 0;
        }

        private static void Fit(CharacterController cc, Transform eye)
        {
            if (cc == null)
                return;
            if (_fitStance == Stance)
                return;
            float h = 1.85f;
            float ey = 1.72f;
            if (Stance == 1)
            {
                h = 1.12f;
                ey = 1.02f;
            }
            else if (Stance == 2)
            {
                h = 0.52f;
                ey = 0.34f;
            }
            float old = cc.height;
            cc.height = h;
            cc.center = new Vector3(0f, h * 0.5f, 0f);
            if (h > old && cc.transform != null)
                cc.transform.position += Vector3.up * ((h - old) * 0.5f);
            if (eye != null)
                eye.localPosition = new Vector3(0f, ey, 0f);
            _fitStance = Stance;
        }

        private static void TickNades()
        {
            float dt = Time.deltaTime;
            for (int i = NadeList.Count - 1; i >= 0; i--)
            {
                Nade n = NadeList[i];
                Vector3 next = n.P + n.V * dt;
                n.V.y += Physics.gravity.y * dt;
                n.Fuse -= dt;
                RaycastHit hit;
                Vector3 delta = next - n.P;
                float len = delta.magnitude;
                if (len > 0.02f && Physics.Raycast(n.P, delta / len, out hit, len + 0.05f, GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    n.P = hit.point;
                    n.Fuse = 0f;
                }
                else
                    n.P = next;
                if (n.Fuse <= 0f)
                {
                    Boom(n.P);
                    NadeList.RemoveAt(i);
                }
                else
                    NadeList[i] = n;
            }
        }

        internal static void BotFrag(Vector3 at, Vector3 vel)
        {
            Nade nade;
            nade.P = at;
            nade.V = vel;
            nade.Fuse = 2.05f;
            NadeList.Add(nade);
        }

        private static void Boom(Vector3 p)
        {
            GaFx.Blast(p);
            GaDropSquad.Blast(p, 8.5f, 38f);
            Transform pawn = GaMode.PawnRoot;
            if (pawn != null && (pawn.position - p).sqrMagnitude < 72f)
            {
                float d = Vector3.Distance(pawn.position, p);
                float fall = 1f - d / 8.5f;
                if (fall < 0.15f)
                    fall = 0.15f;
                GaMode.Hurt(22f * fall);
            }
        }

        private static void TickRevive()
        {
            if (GaClass.Picked != GaKitId.Medic && !GaMode.Downed)
            {
                _reviveHold = 0f;
                return;
            }
            if (!Input.GetKey(KeyCode.F) || GaRadio.Open || GaHud.BlocksLook)
            {
                _reviveHold = 0f;
                return;
            }
            if (GaMode.Downed)
                return;
            GaDropBot down = CachedDowned();
            if (down == null)
            {
                _reviveHold = 0f;
                return;
            }
            _reviveHold += Time.deltaTime;
            if (_reviveHold < 1.6f)
                return;
            _reviveHold = 0f;
            down.Revive();
            GaRadio.Say(GaRadio.PlayerCall(), "You're up. Stay with me.", false, 6);
        }

        internal static bool TryRevivePlayer(GaDropBot medic)
        {
            if (!GaMode.Downed || medic == null)
                return false;
            Transform pawn = GaMode.PawnRoot;
            if (pawn == null)
                return false;
            int id = medic.GetInstanceID();
            if ((medic.transform.position - pawn.position).sqrMagnitude > 6.8f)
            {
                if (_medicId == id)
                {
                    _medicHold = 0f;
                    RescueAmt = 0f;
                }
                return false;
            }
            if (id != _medicId)
            {
                _medicId = id;
                _medicHold = 0f;
            }
            _medicHold += Time.deltaTime;
            RescueAmt = Mathf.Clamp01(_medicHold / 1.6f);
            if (_medicHold < 1.6f)
                return false;
            _medicHold = 0f;
            RescueAmt = 0f;
            GaMode.Revive(42f);
            GaRadio.Say(medic.Callsign, "Stay down a second — you're good.", false, 8);
            return true;
        }

        private static void TickSpotKey()
        {
            if (GaHud.BlocksLook || GaRadio.Open)
                return;
            if (Input.GetKeyDown(KeyCode.Q))
                SpotFromEye();
        }

        private static GaDropBot CachedDowned()
        {
            int f = Time.frameCount;
            if (f == _lookFrame)
                return _lookDown;
            _lookFrame = f;
            _lookDown = LookDowned();
            return _lookDown;
        }

        private static GaDropBot LookDowned()
        {
            Transform eye = GaMode.Eye;
            if (eye == null)
                return null;
            RaycastHit hit;
            if (Physics.Raycast(eye.position, eye.forward, out hit, 3.2f, GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
            {
                GaDropBot b = GaPhys.Bot(hit.collider);
                if (b != null && b.Friendly && b.Downed)
                    return b;
            }
            return GaDropSquad.NearestDowned(GaMode.EyePos, true, 2.8f);
        }

        private static void Prune()
        {
            float now = Time.unscaledTime;
            for (int i = Feeds.Count - 1; i >= 0; i--)
            {
                if (now >= Feeds[i].Dies)
                    Feeds.RemoveAt(i);
            }
            for (int i = Marks.Count - 1; i >= 0; i--)
            {
                if (now >= Marks[i].Dies)
                    Marks.RemoveAt(i);
            }
        }

        private static void DrawHit()
        {
            if (Time.unscaledTime >= _hitUntil)
                return;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color prev = GUI.color;
            GUI.color = _hitKill ? new Color(1f, 0.2f, 0.12f, 0.95f) : new Color(1f, 1f, 1f, 0.92f);
            GUI.DrawTexture(new Rect(cx - 14f, cy - 2f, 10f, 3f), _px);
            GUI.DrawTexture(new Rect(cx + 4f, cy - 2f, 10f, 3f), _px);
            GUI.DrawTexture(new Rect(cx - 2f, cy - 14f, 3f, 10f), _px);
            GUI.DrawTexture(new Rect(cx - 2f, cy + 4f, 3f, 10f), _px);
            GUI.color = prev;
        }

        private static void DrawFeed()
        {
            if (Feeds.Count == 0)
                return;
            float now = Time.unscaledTime;
            float x = 18f;
            float y = 42f;
            for (int i = 0; i < Feeds.Count; i++)
            {
                float left = Feeds[i].Dies - now;
                float a = left > 0.55f ? 1f : (left / 0.55f);
                if (a < 0.02f)
                    continue;
                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(new Rect(x, y + i * 18f, 320f, 18f), Feeds[i].Text, _feed);
                GUI.color = prev;
            }
        }

        private static void DrawMarks()
        {
            Camera cam = GaPerfFrameGate.Cam();
            if (cam == null)
                return;
            for (int i = 0; i < Marks.Count; i++)
            {
                Mark m = Marks[i];
                Vector3 sp = cam.WorldToScreenPoint(m.P);
                if (sp.z < 0.4f)
                    continue;
                float y = Screen.height - sp.y;
                Color prev = GUI.color;
                Color col = m.Enemy
                    ? new Color(1f, 0.16f, 0.1f, 0.95f)
                    : new Color(0.3f, 1f, 0.55f, 0.95f);
                GaFxUi.Corners(sp.x, y, 18f, col);
                GUI.color = col;
                GUI.Label(new Rect(sp.x - 70f, y - 24f, 140f, 16f), m.Label, _hint);
                GUI.color = prev;
            }
        }

        private static void DrawBleed()
        {
            if (!GaMode.Downed || GaMode.Dead)
                return;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float bw = 420f;
            float bh = 22f;
            float x = cx - bw * 0.5f;
            float y = cy - 6f;
            float rescue = RescueAmt;
            float bleed = GaMode.BleedLeft();
            float fill = rescue > 0.02f ? rescue : bleed;
            if (fill < 0f)
                fill = 0f;
            Color prev = GUI.color;
            GUI.color = new Color(0.45f, 0.02f, 0.02f, 0.32f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.82f);
            GUI.DrawTexture(new Rect(x - 3f, y - 3f, bw + 6f, bh + 6f), _px);
            GUI.color = new Color(0.18f, 0.05f, 0.05f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, bw, bh), _px);
            GUI.color = rescue > 0.02f
                ? new Color(0.22f, 0.78f, 0.38f, 0.95f)
                : new Color(0.82f, 0.16f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, bw * fill, bh), _px);
            GUI.color = new Color(1f, 0.92f, 0.88f, 1f);
            string title = rescue > 0.02f ? "RESCUE" : "BLEEDING OUT";
            GUI.Label(new Rect(cx - 240f, y - 36f, 480f, 32f), title, _bleed);
            GUI.color = prev;
        }

        private static void DrawNadeHud()
        {
            if (GaMode.Downed)
                return;
            int key = Nades + (NadeMax << 4) + Stance;
            if (key != _nadeKey)
            {
                _nadeKey = key;
                _nadeLine = "FRAG " + Nades + "/" + NadeMax + "   STANCE " + StanceName();
            }
            GUI.Label(new Rect(16f, 154f, 220f, 18f), _nadeLine, _hint);
        }

        private static string StanceName()
        {
            if (Stance == 2)
                return "PRONE";
            if (Stance == 1)
                return "CROUCH";
            return "STAND";
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
            _feed = new GUIStyle(GUI.skin.label);
            _feed.alignment = TextAnchor.MiddleRight;
            _feed.fontSize = 13;
            _feed.normal.textColor = new Color(0.95f, 0.95f, 0.9f, 0.95f);
            _hint = new GUIStyle(GUI.skin.label);
            _hint.alignment = TextAnchor.MiddleCenter;
            _hint.fontSize = 14;
            _hint.fontStyle = FontStyle.Bold;
            _hint.normal.textColor = Color.white;
            _bleed = new GUIStyle(GUI.skin.label);
            _bleed.alignment = TextAnchor.MiddleCenter;
            _bleed.fontSize = 22;
            _bleed.fontStyle = FontStyle.Bold;
            _bleed.normal.textColor = Color.white;
        }
    }
}
