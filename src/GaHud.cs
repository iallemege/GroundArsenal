using System.Runtime.CompilerServices;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaHud
    {
        internal static bool ArsenalOpen;
        internal static bool CatalogOpen;
        internal static bool BlocksLook
        {
            get
            {
                return ArsenalOpen || CatalogOpen || GaShop.Open || GaGarage.Open || GaHangar.FieldOpen;
            }
        }

        private static GUIStyle _label;
        private static GUIStyle _small;
        private static GUIStyle _hud;
        private static GUIStyle _tiny;
        private static GUIStyle _hudTiny;
        private static GUIStyle _hudTick;
        private static GUIStyle _title;
        private static GUIStyle _offline;
        private static GUIStyle _chute;
        private static GUIStyle _btn;
        private static GUIStyle _btnOn;
        private static GUIStyle _ammo;
        private static GUIStyle _hpNum;
        private static GUIStyle _center;
        private static Texture2D _px;
        private static Texture2D _visor;
        private static Texture2D _scopeMask;
        private static Vector2 _scroll;
        private static Rect _win = new Rect(40f, 40f, 720f, 560f);
        private static int _filterFam = -1;
        private static bool _styles;
        private static string _flash;
        private static float _flashUntil;
        private static int _moveHudPhase;
        private static float _rng;
        private static float _rngAt;

        internal static void FlashHit(string msg)
        {
            _flash = msg;
            _flashUntil = Time.unscaledTime + 1.8f;
        }

        internal static void Tick()
        {
            if (Input.GetKeyDown(KeyCode.M) && !GaRadio.Open)
            {
                GaHangar.ToggleField();
                return;
            }
            if (GaHangar.FieldOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseMenus();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Quote))
            {
                CatalogOpen = !CatalogOpen;
                if (CatalogOpen)
                {
                    ArsenalOpen = false;
                    ArmorCatalogService.EnsureScanned();
                }
                Cursor.lockState = BlocksLook ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = BlocksLook;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && BlocksLook)
            {
                CloseMenus();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        internal static void CloseMenus()
        {
            ArsenalOpen = false;
            CatalogOpen = false;
            GaShop.Close();
            GaGarage.Close();
            GaHangar.CloseField();
        }

        internal static void OnSpawn()
        {
            GaFxUi.OnSpawn();
        }

        internal static void OnDeath()
        {
            GaFxUi.OnDeath();
        }

        internal static void NoteSlot()
        {
            GaFxUi.OnGun();
        }

        internal static void DrawKiaScreen()
        {
            Ensure();
            DrawKia();
        }

        [SkipLocalsInit]
        internal static void Draw()
        {
            Ensure();
            if (GaOritasy.NukeOverlayActive())
                return;
            if (GaMode.Dead)
            {
                DrawKia();
                return;
            }
            _moveHudPhase++;
            float hud = GaFxUi.Smooth(GaFxUi.Hud);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, hud);
            DrawHurtVignette();
            if (GaTsf.Owns())
            {
                DrawLockBox();
                DrawInteract();
                DrawLootHint();
                GaTsf.Draw();
            }
            else
            {
                DrawCompass();
                DrawCrosshair();
                DrawLockBox();
                DrawAds();
                DrawMuzzleBloom();
                DrawFpsHealth();
                DrawFpsAmmo();
                DrawGunPop();
                DrawInteract();
                DrawChutePrompt();
                DrawLootHint();
                GaJet.Draw();
            }
            DrawParkedMark();
            DrawPerf();
            if (Time.unscaledTime < _flashUntil && !string.IsNullOrEmpty(_flash))
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, 46f, 440f, 24f), _flash, _title);
            if (Time.unscaledTime < GaMode.DenyUntil && !string.IsNullOrEmpty(GaMode.DenyMsg))
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, 70f, 440f, 22f), GaMode.DenyMsg, _label);
            GUI.color = prev;
            DrawMenus();
        }

        private static void DrawKia()
        {
            float a = GaFxUi.Smooth(GaFxUi.Kia);
            if (a < 0.01f)
                a = 1f;
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = new Color(0.95f, 0.18f, 0.12f, a);
            GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.40f, 560f, 44f),
                "KIA", _offline);
            GUI.color = new Color(0.85f, 0.85f, 0.82f, a * 0.85f);
            string cause = GaMode.DeathCause;
            if (string.IsNullOrEmpty(cause))
                cause = "KIA";
            GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.48f, 560f, 24f),
                cause, _center);
            GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.53f, 560f, 22f),
                GaForce.Label() + "  /  " + GaClass.Name(), _center);
            GUI.color = prev;
        }

        private static void DrawMenus()
        {
            float a = GaFxUi.Smooth(GaFxUi.Arsenal);
            if (a > 0.02f)
            {
                GaFxUi.Dim(a);
                bool was = GUI.enabled;
                GUI.enabled = ArsenalOpen;
                Vector2 pivot = new Vector2(_win.x + _win.width * 0.5f, _win.y + 40f);
                GaFxUi.Push(a, pivot, 28f);
                _win = GUI.Window(0x47415253, _win, DrawArsenal,
                    "REARM CRATE  —  F close");
                GaFxUi.Pop();
                GUI.enabled = was;
            }
            if (CatalogOpen)
                ArmorCatalogService.Draw();
        }

        private static void DrawHurtVignette()
        {
            float hp = GaMode.Hp / GaMode.MaxHp;
            if (hp > 0.72f)
                return;
            float a = (0.72f - hp) / 0.72f;
            if (GaMode.Downed)
                a = 0.72f;
            float edge = Mathf.Lerp(48f, 140f, a);
            Color top = new Color(0.45f, 0.02f, 0.02f, 0.55f * a);
            Color fade = new Color(0.45f, 0.02f, 0.02f, 0f);
            GaFxUi.FillGrad(new Rect(0f, 0f, Screen.width, edge), top, fade);
            GaFxUi.FillGrad(new Rect(0f, Screen.height - edge, Screen.width, edge), fade, top);
            Color prev = GUI.color;
            GUI.color = new Color(0.45f, 0.02f, 0.02f, 0.28f * a);
            GUI.DrawTexture(new Rect(0f, 0f, edge * 0.45f, Screen.height), _px);
            GUI.DrawTexture(new Rect(Screen.width - edge * 0.45f, 0f, edge * 0.45f, Screen.height), _px);
            GUI.color = prev;
        }

        private static void DrawCrosshair()
        {
            if (GaRide.Busy)
                return;
            if (GaCombat.Ads >= 0.22f)
                return;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float spread = 6f + GaMode.MoveSpeed * 1.45f + GaCombat.BloomAmt * 14f;
            if (GaMode.SprintActive)
                spread += 5f;
            if (GaFps.Stance == 1)
                spread -= 1.5f;
            if (GaFps.Stance == 2)
                spread -= 2.5f;
            if (spread < 4f)
                spread = 4f;
            Color prev = GUI.color;
            GUI.color = new Color(0.95f, 0.97f, 0.92f, 0.88f);
            float arm = 9f;
            float th = 2f;
            GUI.DrawTexture(new Rect(cx - spread - arm, cy - th * 0.5f, arm, th), _px);
            GUI.DrawTexture(new Rect(cx + spread, cy - th * 0.5f, arm, th), _px);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy - spread - arm, th, arm), _px);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy + spread, th, arm), _px);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), _px);
            GUI.color = prev;
        }

        private static void DrawFpsHealth()
        {
            float x = 22f;
            float y = Screen.height - 108f;
            GaFxUi.FillGrad(new Rect(x, y, 248f, 88f),
                new Color(0.02f, 0.04f, 0.05f, 0.55f),
                new Color(0.02f, 0.04f, 0.05f, 0.18f));
            float hp = GaMode.Hp / GaMode.MaxHp;
            if (hp < 0f) hp = 0f;
            if (hp > 1f) hp = 1f;
            Color hc = hp < 0.35f ? new Color(1f, 0.28f, 0.18f) : new Color(0.35f, 0.92f, 0.42f);
            GUI.Label(new Rect(x + 10f, y + 4f, 80f, 36f), Mathf.RoundToInt(GaMode.Hp).ToString(), _hpNum);
            Color prev = GUI.color;
            GUI.color = new Color(0.08f, 0.1f, 0.1f, 0.8f);
            GUI.DrawTexture(new Rect(x + 88f, y + 18f, 148f, 10f), _px);
            GUI.color = hc;
            GUI.DrawTexture(new Rect(x + 88f, y + 18f, 148f * hp, 10f), _px);
            float fat = 1f - GaMode.ArmorFatigue;
            GUI.color = new Color(0.08f, 0.1f, 0.1f, 0.8f);
            GUI.DrawTexture(new Rect(x + 88f, y + 32f, 148f, 6f), _px);
            GUI.color = new Color(0.55f, 0.78f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(x + 88f, y + 32f, 148f * fat, 6f), _px);
            float sp = GaMode.SprintFill();
            GUI.color = new Color(0.08f, 0.1f, 0.1f, 0.8f);
            GUI.DrawTexture(new Rect(x + 10f, y + 48f, 226f, 5f), _px);
            GUI.color = GaMode.SprintActive
                ? new Color(0.35f, 0.95f, 1f, 0.95f)
                : new Color(0.85f, 0.82f, 0.35f, 0.9f);
            GUI.DrawTexture(new Rect(x + 10f, y + 48f, 226f * sp, 5f), _px);
            GUI.color = new Color(0.82f, 0.88f, 0.84f, 0.9f);
            GUI.Label(new Rect(x + 10f, y + 58f, 226f, 24f),
                GaClass.Name() + "  " + GaFps.StanceLabel()
                    + "  " + GaDropSquad.FriendsAlive.ToString() + " squad",
                _hudTiny);
            GUI.color = prev;
        }

        private static void DrawFpsAmmo()
        {
            if (GaRide.Busy)
                return;
            GaGun g = GaCombat.Current;
            if (g == null)
                return;
            GaIcons.Ensure();
            DrawMagRow(Screen.width - 220f, Screen.height - 48f, g);
        }

        private static void DrawGunPop()
        {
            float a = GaFxUi.Smooth(GaFxUi.GunPop);
            if (a < 0.02f)
                return;
            GaGun g = GaCombat.Current;
            if (g == null)
                return;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.48f, 360f, 28f),
                g.En, _center);
            GUI.color = prev;
        }

        private static void DrawInteract()
        {
            string radio = GaRadio.Tip();
            if (string.IsNullOrEmpty(radio))
            {
                Aircraft claimed = GaMode.ParkedAircraft;
                if (claimed != null)
                {
                    try
                    {
                        if (!claimed.disabled)
                            radio = "T  board marked jet";
                    }
                    catch { }
                }
            }
            if (string.IsNullOrEmpty(radio))
                return;
            Color prev = GUI.color;
            GUI.color = new Color(0.95f, 0.95f, 0.9f, 0.9f);
            GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height - 38f, 440f, 22f),
                radio, _center);
            GUI.color = prev;
        }

        internal static void DrawVisorFrame()
        {
            Ensure();
            DrawVisor();
        }

        private static void DrawVisor()
        {
            EnsureVisor();
            Color prev = GUI.color;
            GUI.color = Color.white;
            if (_visor != null)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _visor, ScaleMode.StretchToFill);
            float brow = 22f;
            float chin = Screen.height * 0.09f;
            float leftCut = 236f;
            float rightCut = 200f;
            GUI.color = new Color(0.01f, 0.035f, 0.04f, 0.58f);
            GUI.DrawTexture(new Rect(leftCut, 0f, Screen.width - leftCut - rightCut, brow), _px);
            GUI.DrawTexture(new Rect(0f, Screen.height - chin, Screen.width, chin), _px);
            GUI.DrawTexture(new Rect(0f, brow, 12f, Screen.height - chin - brow), _px);
            GUI.DrawTexture(new Rect(Screen.width - 12f, brow, 12f, Screen.height - chin - brow), _px);
            GUI.color = new Color(0.2f, 0.95f, 0.82f, 0.42f);
            GUI.DrawTexture(new Rect(leftCut, brow - 1f, Screen.width - leftCut - rightCut, 1.5f), _px);
            GUI.DrawTexture(new Rect(0f, Screen.height - chin, Screen.width, 1.5f), _px);
            GUI.color = new Color(0.2f, 0.95f, 0.82f, 0.55f);
            DrawBracket(18f, Screen.height - 18f, 1f, -1f);
            DrawBracket(Screen.width - 18f, Screen.height - 18f, -1f, -1f);
            GUI.color = prev;
        }

        private static void DrawBracket(float x, float y, float sx, float sy)
        {
            float arm = 28f;
            float th = 2f;
            GUI.DrawTexture(new Rect(sx > 0f ? x : x - arm, y - th * 0.5f, arm, th), _px);
            GUI.DrawTexture(new Rect(x - th * 0.5f, sy > 0f ? y : y - arm, th, arm), _px);
        }

        private static void DrawCompass()
        {
            float yaw = GaMode.LookYaw;
            float w = 420f;
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = 10f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.04f, 0.05f, 0.42f);
            GUI.DrawTexture(new Rect(x, y, w, 24f), _px);
            GUI.color = new Color(0.92f, 0.95f, 0.9f, 0.92f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1f, y, 2f, 28f), _px);
            float pxPerDeg = w / 90f;
            for (int d = -50; d <= 50; d += 5)
            {
                float hdg = Mathf.Repeat(yaw + d + 360f, 360f);
                float tx = Screen.width * 0.5f + d * pxPerDeg;
                if (tx < x + 6f || tx > x + w - 6f)
                    continue;
                bool major = (Mathf.RoundToInt(hdg) % 15) == 0;
                float tickH = major ? 10f : 5f;
                GUI.color = new Color(0.9f, 0.93f, 0.88f, major ? 0.9f : 0.35f);
                GUI.DrawTexture(new Rect(tx - 0.5f, y + 28f - tickH, 1.5f, tickH), _px);
                if (!major)
                    continue;
                int ih = Mathf.RoundToInt(hdg);
                if (ih == 360)
                    ih = 0;
                string lab = ih.ToString("000");
                if (ih == 0) lab = "N";
                else if (ih == 90) lab = "E";
                else if (ih == 180) lab = "S";
                else if (ih == 270) lab = "W";
                GUI.Label(new Rect(tx - 14f, y + 2f, 28f, 16f), lab, _hudTick);
            }
            GUI.color = new Color(0.9f, 0.95f, 0.88f, 0.85f);
            GUI.Label(new Rect(x + w + 8f, y + 4f, 72f, 20f),
                Mathf.RoundToInt(Mathf.Repeat(yaw, 360f)).ToString("000") + "°", _hud);
            GUI.color = prev;
        }

        private static void DrawSuitPanel()
        {
            float x = 16f;
            float y = 36f;
            float w = 220f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.07f, 0.08f, 0.72f);
            GUI.DrawTexture(new Rect(x, y, w, 132f), _px);
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, w, 2f), _px);
            GUI.DrawTexture(new Rect(x, y, 2f, 132f), _px);
            GUI.color = new Color(0.35f, 1f, 0.88f, 0.95f);
            GUI.Label(new Rect(x + 8f, y + 4f, w - 12f, 20f), "EXO-10  POWERED SUIT", _hud);
            float hp = GaMode.Hp / GaMode.MaxHp;
            if (hp < 0f) hp = 0f;
            if (hp > 1f) hp = 1f;
            DrawBar(x + 8f, y + 24f, w - 16f, hp,
                "INTEGRITY  " + Mathf.RoundToInt(GaMode.Hp) + "/" + Mathf.RoundToInt(GaMode.MaxHp),
                hp < 0.35f ? new Color(1f, 0.28f, 0.18f) : new Color(0.3f, 1f, 0.55f));
            float fat = GaMode.ArmorFatigue;
            DrawBar(x + 8f, y + 48f, w - 16f, 1f - fat,
                "PLATE " + GaMode.OuterEffective() + "  FAT " + Mathf.RoundToInt(fat * 100f) + "%",
                fat > 0.55f ? new Color(1f, 0.7f, 0.2f) : new Color(0.45f, 0.85f, 1f));
            float heat = GaMode.ServoHeat();
            DrawBar(x + 8f, y + 72f, w - 16f, heat,
                "SERVO  " + Mathf.RoundToInt(heat * 100f) + "%",
                heat >= 0.72f ? new Color(1f, 0.4f, 0.1f) : new Color(0.95f, 0.8f, 0.25f));
            GUI.color = new Color(0.65f, 0.9f, 0.85f, 0.85f);
            GUI.Label(new Rect(x + 8f, y + 100f, w - 16f, 22f),
                "SPD " + GaMode.MoveSpeed.ToString("0.0") + " m/s   PWR "
                    + Mathf.RoundToInt(GaMode.SprintFill() * 100f) + "%",
                _hudTiny);
            GUI.color = prev;
        }

        private static void DrawSysRail()
        {
            float w = 176f;
            float x = Screen.width - w - 18f;
            float y = 36f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.07f, 0.08f, 0.72f);
            GUI.DrawTexture(new Rect(x, y, w, GaChute.Falling ? 168f : 148f), _px);
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, w, 2f), _px);
            GUI.color = new Color(0.35f, 1f, 0.88f, 0.95f);
            GUI.Label(new Rect(x + 8f, y + 4f, w - 12f, 20f), "SUIT BUS", _hud);
            GUI.color = new Color(0.6f, 0.95f, 0.85f, 0.9f);
            GUI.Label(new Rect(x + 8f, y + 26f, w - 12f, 18f), "LINK  ACTIVE", _hudTiny);
            GUI.Label(new Rect(x + 8f, y + 46f, w - 12f, 18f), "OPTICS  STAB", _hudTiny);
            GUI.Label(new Rect(x + 8f, y + 66f, w - 12f, 18f),
                "SERVO  " + (GaMode.ServoHeat() >= 0.72f ? "LIMIT" : "READY"), _hudTiny);
            GUI.Label(new Rect(x + 8f, y + 86f, w - 12f, 18f),
                "PLATE  " + GaMode.OuterEffective() + " / " + GaMode.InnerArmor, _hudTiny);
            GUI.Label(new Rect(x + 8f, y + 106f, w - 12f, 18f),
                "SQUAD  " + GaDropSquad.FriendsAlive + "   FOE  " + GaDropSquad.HostilesAlive, _hudTiny);
            GUI.Label(new Rect(x + 8f, y + 126f, w - 12f, 18f),
                GaForce.Label() + "  " + GaClass.Name(), _hudTiny);
            if (GaChute.Falling)
            {
                GUI.color = new Color(0.95f, 0.82f, 0.25f, 0.95f);
                GUI.Label(new Rect(x + 8f, y + 146f, w - 12f, 18f), GaChute.Hint(), _hudTiny);
            }
            GUI.color = prev;
        }

        private static void DrawHelmetReticle()
        {
            if (GaCombat.Ads >= 0.22f)
                return;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color prev = GUI.color;
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.72f);
            GUI.DrawTexture(new Rect(cx - 14f, cy - 1f, 9f, 2f), _px);
            GUI.DrawTexture(new Rect(cx + 5f, cy - 1f, 9f, 2f), _px);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 14f, 2f, 9f), _px);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 5f, 2f, 9f), _px);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), _px);
            GUI.DrawTexture(new Rect(cx - 22f, cy - 22f, 8f, 1.5f), _px);
            GUI.DrawTexture(new Rect(cx - 22f, cy - 22f, 1.5f, 8f), _px);
            GUI.DrawTexture(new Rect(cx + 14f, cy - 22f, 8f, 1.5f), _px);
            GUI.DrawTexture(new Rect(cx + 20.5f, cy - 22f, 1.5f, 8f), _px);
            GUI.DrawTexture(new Rect(cx - 22f, cy + 20.5f, 8f, 1.5f), _px);
            GUI.DrawTexture(new Rect(cx - 22f, cy + 14f, 1.5f, 8f), _px);
            GUI.DrawTexture(new Rect(cx + 14f, cy + 20.5f, 8f, 1.5f), _px);
            GUI.DrawTexture(new Rect(cx + 20.5f, cy + 14f, 1.5f, 8f), _px);
            float rng = CachedRange();
            GUI.color = new Color(0.45f, 1f, 0.88f, 0.75f);
            GUI.Label(new Rect(cx - 40f, cy + 26f, 80f, 14f),
                rng > 0.4f ? ("RNG " + Mathf.RoundToInt(rng).ToString() + "m") : "RNG ----",
                _hudTiny);
            GUI.color = prev;
        }

        private static float CachedRange()
        {
            if (Time.unscaledTime < _rngAt)
                return _rng;
            if (GaPerfFrameGate.SkipPolish() && _rngAt > 0f)
                return _rng;
            _rngAt = Time.unscaledTime + (GaMode.MoveSpeed > 0.8f ? 0.32f : 0.2f);
            _rng = 0f;
            RaycastHit hit;
            if (Physics.Raycast(GaMode.EyePos, GaMode.EyeFwd, out hit, 400f,
                GaPhys.WorldMask, QueryTriggerInteraction.Ignore))
                _rng = hit.distance;
            return _rng;
        }

        private static void DrawHipAssist()
        {
            if (!GaCombat.AssistOn || GaCombat.Ads >= 0.2f)
                return;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam == null)
                return;
            Vector3 sp = cam.WorldToScreenPoint(GaCombat.AssistWorld);
            if (sp.z < 0.35f)
                return;
            float x = sp.x;
            float y = Screen.height - sp.y;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.72f, 0.18f, 0.92f);
            GUI.DrawTexture(new Rect(x - 7f, y - 1.5f, 5f, 3f), _px);
            GUI.DrawTexture(new Rect(x + 2f, y - 1.5f, 5f, 3f), _px);
            GUI.DrawTexture(new Rect(x - 1.5f, y - 7f, 3f, 5f), _px);
            GUI.DrawTexture(new Rect(x - 1.5f, y + 2f, 3f, 5f), _px);
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, 4f, 4f), _px);
            GUI.color = new Color(1f, 0.82f, 0.28f, 0.75f);
            GUI.Label(new Rect(x - 36f, y + 10f, 72f, 14f), "LEAD", _hudTiny);
            GUI.color = prev;
        }

        private static void DrawLockBox()
        {
            GaGun g = GaCombat.Current;
            if (g == null || (!GaCombat.IsRpg(g) && !GaCombat.IsRbs(g)))
                return;
            if (GaCombat.LockTgt == null || GaCombat.LockTgt.disabled)
                return;
            if (GaCombat.LockAmt < 0.12f)
                return;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam == null)
                return;
            Vector3 world = GaCombat.LockTgt.transform.position;
            try { world = GaCombat.LockTgt.GetCenterOfMass(); }
            catch { }
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0.4f)
                return;
            float x = sp.x;
            float y = Screen.height - sp.y;
            float dist = Vector3.Distance(cam.transform.position, world);
            if (dist < 8f)
                dist = 8f;
            float s = 2200f / dist;
            if (s < 22f) s = 22f;
            if (s > 96f) s = 96f;
            bool ready = GaCombat.LockReady;
            float a = ready ? 1f : (0.35f + GaCombat.LockAmt * 0.55f);
            Color prev = GUI.color;
            GaFxUi.Corners(x, y, s, new Color(1f, 0.16f, 0.1f, a));
            if (ready)
            {
                bool blink = ((int)(Time.unscaledTime * 6f) % 2) == 0;
                GUI.color = new Color(1f, blink ? 0.18f : 0.55f, 0.08f, 1f);
                GUI.Label(new Rect(x - 40f, y + s * 0.5f + 6f, 80f, 20f), "FIRE", _title);
            }
            else
            {
                GUI.color = new Color(1f, 0.35f, 0.15f, a);
                GUI.Label(new Rect(x - 40f, y + s * 0.5f + 6f, 80f, 16f),
                    Mathf.RoundToInt(GaCombat.LockAmt * 100f).ToString() + "%", _tiny);
            }
            GUI.color = prev;
        }

        private static void DrawSeg(Vector2 a, Vector2 b, float th)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f)
                return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.DrawTexture(new Rect(a.x, a.y - th * 0.5f, len, th), _px);
            GUI.matrix = m;
        }

        private static void DrawMuzzleBloom()
        {
            if (Time.time >= GaCombat.FlashUntil)
                return;
            float u = (GaCombat.FlashUntil - Time.time) / 0.048f;
            if (u < 0f) u = 0f;
            if (u > 1f) u = 1f;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            if (GaCombat.Ads < 0.28f)
            {
                cx += Screen.width * 0.042f;
                cy += Screen.height * 0.038f;
            }
            float a = u * (GaCombat.Ads >= 0.28f ? 0.26f : 0.14f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.78f, 0.28f, a);
            float s = (GaCombat.Ads >= 0.28f ? 36f : 20f) * (0.7f + u * 0.3f);
            DrawSeg(new Vector2(cx - s, cy), new Vector2(cx + s, cy), 2.1f);
            DrawSeg(new Vector2(cx, cy - s * 0.52f), new Vector2(cx, cy + s * 0.52f), 1.7f);
            GUI.color = new Color(1f, 0.95f, 0.7f, a * 0.85f);
            float d = s * 0.6f;
            DrawSeg(new Vector2(cx - d, cy - d * 0.32f), new Vector2(cx + d, cy + d * 0.32f), 1.3f);
            DrawSeg(new Vector2(cx - d, cy + d * 0.32f), new Vector2(cx + d, cy - d * 0.32f), 1.3f);
            GUI.color = prev;
        }

        private static void DrawWeaponStrip()
        {
            GaIcons.Ensure();
            GaGun g = GaCombat.Current;
            string n = g != null ? g.En : "?";
            float x = 18f;
            float barY = Screen.height - 96f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.08f, 0.09f, 0.78f);
            GUI.DrawTexture(new Rect(x, barY, 460f, 80f), _px);
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.7f);
            GUI.DrawTexture(new Rect(x, barY, 460f, 2f), _px);
            GUI.color = Color.white;
            Texture2D cur = g != null ? GaIcons.Get(g) : null;
            float tx = x + 10f;
            if (cur != null)
            {
                GUI.DrawTexture(new Rect(x + 8f, barY + 6f, 140f, 68f), cur, ScaleMode.ScaleToFit);
                tx = x + 156f;
            }
            GUI.Label(new Rect(tx, barY + 6f, 290f, 22f),
                "[" + (GaCombat.Slot + 1).ToString() + " " + GaClass.SlotName(GaCombat.Slot) + "]  " + n, _hud);
            GUI.Label(new Rect(tx, barY + 28f, 290f, 18f), KitHint(), _hudTiny);
            GUI.Label(new Rect(tx, barY + 48f, 280f, 16f), BoardHint(), _tiny);
            GUI.color = prev;
        }

        private static string KitHint()
        {
            string radio = GaRadio.Tip();
            if (!string.IsNullOrEmpty(radio))
                return radio;
            return GaForce.Label() + "  " + GaClass.Name()
                + (GaClass.Picked == GaKitId.SpecOps ? "  JET" : "")
                + (GaClass.Picked == GaKitId.Tsf ? "  TSF" : "")
                + "  PLATE " + GaMode.OuterArmor.ToString()
                + "  " + GaFps.StanceLabel();
        }

        private static string BoardHint()
        {
            Aircraft claimed = GaMode.ParkedAircraft;
            if (claimed != null)
            {
                try
                {
                    if (!claimed.disabled)
                        return "T  board marked jet (12m)";
                }
                catch { }
            }
            if (GaForce.Kind == GaForceKind.Land)
                return "T  board vehicle   H  buy vehicle   V  radio";
            return "T  board   H  vehicle   B  aircraft   L  3rd";
        }

        private static void DrawMagRow(float x, float y, GaGun g)
        {
            int state = GaCombat.MagState(g);
            Texture2D cur = GaIcons.MagIcon(g, state);
            if (cur != null)
                GUI.DrawTexture(new Rect(x, y - 4f, 22f, 34f), cur, ScaleMode.ScaleToFit);
            int inMag = GaCombat.Mag[g.Id];
            GUI.Label(new Rect(x + 22f, y + 8f, 36f, 16f), inMag.ToString(), _hudTiny);
            int pool = GaCombat.PoolOf(g);
            int size = g.Mag > 0 ? g.Mag : 1;
            int full = pool / size;
            int rem = pool - full * size;
            float px = x + 62f;
            int drawn = 0;
            int maxSpare = 8;
            for (int i = 0; i < full && drawn < maxSpare; i++)
            {
                Texture2D ic = GaIcons.MagIcon(g, 2);
                if (ic != null)
                    GUI.DrawTexture(new Rect(px, y, 16f, 26f), ic, ScaleMode.ScaleToFit);
                px += 18f;
                drawn++;
            }
            if (rem > 0 && drawn < maxSpare)
            {
                int st = rem * 2 <= size ? 1 : 2;
                if (rem * 4 < size)
                    st = 0;
                Texture2D ic = GaIcons.MagIcon(g, st);
                if (ic != null)
                    GUI.DrawTexture(new Rect(px, y, 16f, 26f), ic, ScaleMode.ScaleToFit);
                px += 18f;
                drawn++;
            }
            if (drawn == 0)
            {
                Texture2D ic = GaIcons.MagIcon(g, 0);
                if (ic != null)
                    GUI.DrawTexture(new Rect(px, y, 16f, 26f), ic, ScaleMode.ScaleToFit);
            }
        }

        private static void DrawLootHint()
        {
            if (GaMode.Downed || GaChute.Falling)
                return;
            string msg = GaSupport.Hint();
            if (string.IsNullOrEmpty(msg))
                msg = GaSupply.Hint();
            if (string.IsNullOrEmpty(msg) && !GaTsf.Owns())
                msg = GaFps.Hint();
            if (string.IsNullOrEmpty(msg))
                msg = GaRide.Hint();
            if (string.IsNullOrEmpty(msg))
                msg = GaLoot.Hint;
            if (string.IsNullOrEmpty(msg))
                return;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.7f, 0.2f, 0.92f);
            GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.52f, 440f, 22f),
                msg, _title);
            GUI.color = prev;
        }

        private static void DrawChutePrompt()
        {
            if (!GaChute.Falling)
                return;
            string action = GaChute.PromptAction();
            string agl = GaChute.PromptAgl();
            if (string.IsNullOrEmpty(action))
                return;
            float w = 420f;
            float h = 58f;
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = Screen.height * 0.18f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.04f, 0.78f);
            GUI.DrawTexture(new Rect(x, y, w, h), _px);
            Color ink = GaChute.Open
                ? new Color(0.45f, 0.95f, 0.55f, 1f)
                : new Color(1f, 0.78f, 0.22f, 1f);
            GUI.color = ink;
            GUI.Label(new Rect(x, y + 4f, w, 26f), action, _chute);
            GUI.color = new Color(0.92f, 0.94f, 0.9f, 0.95f);
            GUI.Label(new Rect(x, y + 28f, w, 24f), agl, _chute);
            GUI.color = prev;
        }

        private static void DrawParkedMark()
        {
            Aircraft ac = GaMode.ParkedAircraft;
            if (ac == null)
                return;
            try
            {
                if (ac.disabled)
                    return;
            }
            catch { }
            Camera cam = GaPerfFrameGate.Cam();
            if (cam == null)
                return;
            Vector3 pos = ac.transform.position + Vector3.up * 3.2f;
            Vector3 sp = cam.WorldToScreenPoint(pos);
            if (sp.z < 0.4f)
                return;
            float y = Screen.height - sp.y;
            float dist = Vector3.Distance(GaMode.EyePos, ac.transform.position);
            string n = "?";
            try
            {
                if (ac.definition != null && !string.IsNullOrEmpty(ac.definition.unitName))
                    n = ac.definition.unitName;
                else
                    n = ac.name;
            }
            catch { n = ac.name; }
            string line = n + "  " + Mathf.RoundToInt(dist).ToString() + "m";
            if (dist <= (GaMode.AirHop ? 48f : 12f))
                line = line + "  T BOARD";
            Color prev = GUI.color;
            GaFxUi.Corners(sp.x, y, 22f, new Color(0.25f, 0.9f, 1f, 0.95f));
            GUI.color = new Color(0.75f, 0.95f, 1f, 0.95f);
            GUI.Label(new Rect(sp.x - 110f, y - 28f, 220f, 20f), line, _hudTiny);
            GUI.color = prev;
        }

        private static string HintLine(GaGun g)
        {
            if (g != null && GaCombat.IsRbs(g))
                return "RMB lock air   LMB fire   G drop   U supply";
            if (g != null && GaCombat.IsRpg(g))
                return "RMB lock ground   LMB fire   G drop   U supply";
            return "G drop   U supply drop   T board   B aircraft   V radio   Q spot   C crouch";
        }

        private static void DrawSkillRings()
        {
            float cx = Screen.width * 0.5f;
            float y = Screen.height - 148f;
            DrawRing(new Vector2(cx - 54f, y), 28f,
                GaMode.SprintFill(),
                GaMode.SprintActive,
                "SPRINT",
                "SHIFT");
            DrawRing(new Vector2(cx + 54f, y), 28f,
                GaMode.JumpFill(),
                false,
                "JUMP",
                "SPACE");
        }

        private static void DrawRing(Vector2 c, float r, float fill, bool active, string name, string key)
        {
            Color prev = GUI.color;
            int segs = 36;
            float f = fill;
            if (f < 0f) f = 0f;
            if (f > 1f) f = 1f;
            Color on = active ? new Color(0.35f, 0.95f, 1f, 0.95f) : new Color(0.4f, 1f, 0.45f, 0.95f);
            Color off = new Color(0.15f, 0.18f, 0.16f, 0.7f);
            for (int i = 0; i < segs; i++)
            {
                float t = (i + 0.5f) / segs;
                GUI.color = t <= f ? on : off;
                float a = (i / (float)segs) * 360f * Mathf.Deg2Rad;
                float x = c.x + Mathf.Sin(a) * r;
                float y = c.y - Mathf.Cos(a) * r;
                GUI.DrawTexture(new Rect(x - 2.5f, y - 2.5f, 5f, 5f), _px);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(c.x - 36f, c.y - 10f, 72f, 16f), name, _tiny);
            GUI.Label(new Rect(c.x - 36f, c.y + 6f, 72f, 14f), key, _tiny);
            GUI.color = prev;
        }

        private static void DrawAds()
        {
            if (GaCombat.Ads < 0.28f)
                return;
            if (GaCombat.IsG11())
            {
                DrawG11Optic();
                return;
            }
            EnsureScopeMask();
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float a = GaCombat.Ads;
            bool sniper = GaCombat.IsSniper();
            float fill = sniper
                ? Mathf.Lerp(0.78f, 0.96f, GaCombat.Zoom)
                : Mathf.Lerp(0.48f, 0.70f, GaCombat.Zoom);
            float side = Mathf.Min(Screen.width, Screen.height) * fill;
            Rect hole = new Rect(cx - side * 0.5f, cy - side * 0.5f, side, side);
            Color prev = GUI.color;
            GUI.color = new Color(0.01f, 0.015f, 0.02f, 0.93f * a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, hole.y), _px);
            GUI.DrawTexture(new Rect(0f, hole.yMax, Screen.width, Screen.height - hole.yMax), _px);
            GUI.DrawTexture(new Rect(0f, hole.y, hole.x, hole.height), _px);
            GUI.DrawTexture(new Rect(hole.xMax, hole.y, Screen.width - hole.xMax, hole.height), _px);
            GUI.color = new Color(1f, 1f, 1f, a);
            if (_scopeMask != null)
                GUI.DrawTexture(hole, _scopeMask, ScaleMode.StretchToFill);
            DrawScopeReticle(cx, cy, side * 0.5f, a, sniper);
            GUI.color = new Color(0.75f, 0.88f, 0.8f, 0.8f * a);
            GUI.Label(new Rect(cx - 50f, cy + side * 0.36f, 100f, 16f),
                GaCombat.ZoomPower().ToString("0.0") + "x", _tiny);
            GUI.color = prev;
        }

        private static void DrawG11Optic()
        {
            float a = GaCombat.Ads;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float bw = Mathf.Min(Screen.width, Screen.height) * Mathf.Lerp(0.42f, 0.58f, GaCombat.Zoom);
            float bh = bw * 0.62f;
            Rect hole = new Rect(cx - bw * 0.5f, cy - bh * 0.5f, bw, bh);
            Color prev = GUI.color;
            GUI.color = new Color(0.01f, 0.03f, 0.04f, 0.94f * a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, hole.y), _px);
            GUI.DrawTexture(new Rect(0f, hole.yMax, Screen.width, Screen.height - hole.yMax), _px);
            GUI.DrawTexture(new Rect(0f, hole.y, hole.x, hole.height), _px);
            GUI.DrawTexture(new Rect(hole.xMax, hole.y, Screen.width - hole.xMax, hole.height), _px);
            GUI.color = new Color(0.02f, 0.08f, 0.09f, 0.35f * a);
            GUI.DrawTexture(hole, _px);
            float th = 3f;
            GUI.color = new Color(0.25f, 0.95f, 0.82f, 0.88f * a);
            GUI.DrawTexture(new Rect(hole.x, hole.y, hole.width, th), _px);
            GUI.DrawTexture(new Rect(hole.x, hole.yMax - th, hole.width, th), _px);
            GUI.DrawTexture(new Rect(hole.x, hole.y, th, hole.height), _px);
            GUI.DrawTexture(new Rect(hole.xMax - th, hole.y, th, hole.height), _px);
            float inset = 10f;
            GUI.color = new Color(0.2f, 0.9f, 0.78f, 0.45f * a);
            GUI.DrawTexture(new Rect(hole.x + inset, hole.y + inset, hole.width - inset * 2f, 1.5f), _px);
            GUI.DrawTexture(new Rect(hole.x + inset, hole.yMax - inset, hole.width - inset * 2f, 1.5f), _px);
            GUI.DrawTexture(new Rect(hole.x + inset, hole.y + inset, 1.5f, hole.height - inset * 2f), _px);
            GUI.DrawTexture(new Rect(hole.xMax - inset, hole.y + inset, 1.5f, hole.height - inset * 2f), _px);
            GUI.color = new Color(0.3f, 1f, 0.85f, 0.9f * a);
            GUI.DrawTexture(new Rect(cx - 18f, cy - 1.5f, 12f, 3f), _px);
            GUI.DrawTexture(new Rect(cx + 6f, cy - 1.5f, 12f, 3f), _px);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy + 6f, 3f, 14f), _px);
            GUI.DrawTexture(new Rect(cx - 3f, cy - 3f, 6f, 6f), _px);
            GUI.color = new Color(0.55f, 1f, 0.88f, 0.8f * a);
            GUI.Label(new Rect(hole.x + 12f, hole.y + 8f, 180f, 16f), "AK 11  SMART", _hud);
            GUI.Label(new Rect(hole.xMax - 90f, hole.y + 8f, 80f, 16f),
                GaCombat.ZoomPower().ToString("0.0") + "x", _hudTiny);
            GUI.Label(new Rect(cx - 70f, hole.yMax - 22f, 140f, 16f), "SEEK ON FIRE", _hudTiny);
            GUI.color = prev;
        }

        private static void DrawScopeReticle(float cx, float cy, float r, float a, bool sniper)
        {
            Color line = sniper
                ? new Color(0.12f, 0.95f, 0.55f, 0.88f * a)
                : new Color(0.85f, 0.95f, 0.88f, 0.8f * a);
            GUI.color = line;
            float arm = sniper ? r * 0.72f : r * 0.38f;
            float gap = sniper ? 10f : 7f;
            float th = 1.6f;
            GUI.DrawTexture(new Rect(cx - arm, cy - th * 0.5f, arm - gap, th), _px);
            GUI.DrawTexture(new Rect(cx + gap, cy - th * 0.5f, arm - gap, th), _px);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy - arm, th, arm - gap), _px);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy + gap, th, arm - gap), _px);
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), _px);
            if (!sniper)
                return;
            float[] ticks = new float[] { 0.18f, 0.34f, 0.5f };
            for (int i = 0; i < ticks.Length; i++)
            {
                float d = r * ticks[i];
                float tick = i == ticks.Length - 1 ? 9f : 6f;
                GUI.DrawTexture(new Rect(cx - d - 1f, cy - tick * 0.5f, 2f, tick), _px);
                GUI.DrawTexture(new Rect(cx + d - 1f, cy - tick * 0.5f, 2f, tick), _px);
                GUI.DrawTexture(new Rect(cx - tick * 0.5f, cy - d - 1f, tick, 2f), _px);
                GUI.DrawTexture(new Rect(cx - tick * 0.5f, cy + d - 1f, tick, 2f), _px);
            }
        }

        private static void EnsureVisor()
        {
            if (_visor != null)
                return;
            int n = 128;
            _visor = new Texture2D(n, n, TextureFormat.ARGB32, false);
            _visor.wrapMode = TextureWrapMode.Clamp;
            _visor.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float nx = Mathf.Abs(x - c) / c;
                    float ny = Mathf.Abs(y - c) / c;
                    float e = Mathf.Max(nx, ny);
                    float a = 0f;
                    if (e > 0.62f)
                        a = Mathf.Pow(Mathf.InverseLerp(0.62f, 1f, e), 1.35f) * 0.62f;
                    px[y * n + x] = new Color(0.01f, 0.04f, 0.05f, a);
                }
            }
            _visor.SetPixels(px);
            _visor.Apply(false, true);
        }

        private static void EnsureScopeMask()
        {
            if (_scopeMask != null)
                return;
            int n = 256;
            _scopeMask = new Texture2D(n, n, TextureFormat.ARGB32, false);
            _scopeMask.wrapMode = TextureWrapMode.Clamp;
            _scopeMask.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[n * n];
            float c = (n - 1) * 0.5f;
            float r0 = c * 0.84f;
            float r1 = c * 0.99f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float rr = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha;
                    if (rr < r0)
                        alpha = 0f;
                    else if (rr > r1)
                        alpha = 0.96f;
                    else
                        alpha = Mathf.InverseLerp(r0, r1, rr) * 0.96f;
                    px[y * n + x] = new Color(0.02f, 0.03f, 0.03f, alpha);
                }
            }
            _scopeMask.SetPixels(px);
            _scopeMask.Apply(false, true);
        }

        private static void DrawPerf()
        {
            if (!GaConfig.WantPerf())
                return;
            Ensure();
            float fps = Time.unscaledDeltaTime > 0.0001f ? (1f / Time.unscaledDeltaTime) : 0f;
            string line = "GA " + GaInfo.Version
                + "  " + Mathf.RoundToInt(fps).ToString() + " fps"
                + "  sparks " + GaFx.Live
                + "  standalone"
                + (GaOritasy.HasModularArmor ? "  Oritasy-AP" : "  GA-AP");
            GUI.color = new Color(0.55f, 1f, 0.75f, 0.85f);
            GUI.Label(new Rect(Screen.width * 0.5f + 230f, 12f, 280f, 18f), line, _hudTiny);
            GUI.color = Color.white;
        }

        private static void DrawBar(float x, float y, float w, float fill, string label, Color col)
        {
            if (fill < 0f) fill = 0f;
            if (fill > 1f) fill = 1f;
            Color prev = GUI.color;
            GUI.color = new Color(0.08f, 0.1f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(x, y + 14f, w, 6f), _px);
            GUI.color = col;
            GUI.DrawTexture(new Rect(x, y + 14f, w * fill, 6f), _px);
            GUI.color = new Color(0.8f, 0.95f, 0.9f, 0.9f);
            GUI.Label(new Rect(x, y, w, 18f), label, _hudTiny);
            GUI.color = prev;
        }

        private static void DrawArsenal(int id)
        {
            GaIcons.Ensure();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All", _filterFam < 0 ? _btnOn : _btn, GUILayout.Width(64f)))
                _filterFam = -1;
            for (int f = 0; f < GaCatalog.FamilyCount; f++)
            {
                if (!GaCatalog.FamilyHas(f))
                    continue;
                if (GUILayout.Button(GaCatalog.FamilyEn[f],
                    _filterFam == f ? _btnOn : _btn, GUILayout.Height(22f)))
                    _filterFam = f;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("1 PRIMARY  2 SECONDARY  3 PISTOL  4 CLASS  — slot "
                + (GaCombat.Slot + 1).ToString() + " " + GaClass.SlotName(GaCombat.Slot), _small);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(420f));
            for (int i = 0; i < GaCatalog.Count; i++)
            {
                GaGun g = GaCatalog.All[i];
                if (_filterFam >= 0 && g.Family != _filterFam)
                    continue;
                bool on = false;
                for (int h = 0; h < GaCombat.Hotbar.Length; h++)
                {
                    if (GaCombat.Hotbar[h] == g.Id)
                        on = true;
                }
                Rect row = GUILayoutUtility.GetRect(1f, 52f, GUILayout.ExpandWidth(true));
                Texture2D ic = GaIcons.Get(g);
                if (ic != null)
                    GUI.DrawTexture(new Rect(row.x, row.y + 2f, 96f, 48f), ic, ScaleMode.ScaleToFit);
                string label = g.En + "  " + GaCatalog.CalLabel(g) + "  PEN " + Mathf.RoundToInt(g.Pen).ToString()
                    + "  " + GaCatalog.CostM(g).ToString("0.00") + "M"
                    + "  " + GaCatalog.FireName(g.Fire, false)
                    + "  MAG " + g.Mag.ToString()
                    + (GaCombat.Unlocked(g) ? "" : ("  LOCK RANK " + g.Rank.ToString()));
                    if (GUI.Button(new Rect(row.x + 100f, row.y, row.width - 100f, 50f),
                    label, on ? _btnOn : _btn))
                {
                    if (!InClassPool(g.Id, GaCombat.Slot))
                        FlashHit(GaCombat.Slot == 3 ? "CLASS WEAPON LOCKED" : "NOT THIS SLOT");
                    else
                    {
                        GaCombat.Hotbar[GaCombat.Slot] = g.Id;
                        GaCombat.GiveGunAmmo(g);
                        GaCombat.SavePrefs();
                        GaCombat.RefreshView();
                    }
                }
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static bool InClassPool(int id, int slot)
        {
            int[] pool = GaClass.SlotPool(GaClass.Picked, slot);
            if (pool == null)
                return false;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == id)
                    return true;
            }
            return false;
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = 16;
            _label.normal.textColor = Color.white;
            _label.alignment = TextAnchor.MiddleCenter;
            _small = new GUIStyle(GUI.skin.label);
            _small.fontSize = 13;
            _small.normal.textColor = new Color(0.85f, 0.9f, 0.95f);
            _hud = new GUIStyle(GUI.skin.label);
            _hud.fontSize = 13;
            _hud.fontStyle = FontStyle.Bold;
            _hud.alignment = TextAnchor.UpperLeft;
            _hud.clipping = TextClipping.Overflow;
            _hud.wordWrap = false;
            _hud.normal.textColor = new Color(0.35f, 1f, 0.88f);
            _tiny = new GUIStyle(GUI.skin.label);
            _tiny.fontSize = 11;
            _tiny.alignment = TextAnchor.MiddleCenter;
            _tiny.normal.textColor = new Color(0.75f, 0.8f, 0.78f);
            _hudTiny = new GUIStyle(GUI.skin.label);
            _hudTiny.fontSize = 12;
            _hudTiny.alignment = TextAnchor.UpperLeft;
            _hudTiny.clipping = TextClipping.Overflow;
            _hudTiny.wordWrap = false;
            _hudTiny.normal.textColor = new Color(0.55f, 0.95f, 0.85f);
            _hudTick = new GUIStyle(GUI.skin.label);
            _hudTick.fontSize = 11;
            _hudTick.alignment = TextAnchor.MiddleCenter;
            _hudTick.clipping = TextClipping.Overflow;
            _hudTick.wordWrap = false;
            _hudTick.normal.textColor = new Color(0.55f, 0.95f, 0.85f);
            _title = new GUIStyle(GUI.skin.label);
            _title.fontSize = 16;
            _title.fontStyle = FontStyle.Bold;
            _title.alignment = TextAnchor.MiddleCenter;
            _title.normal.textColor = new Color(0.92f, 0.95f, 0.88f);
            _offline = new GUIStyle(GUI.skin.label);
            _offline.fontSize = 42;
            _offline.fontStyle = FontStyle.Bold;
            _offline.alignment = TextAnchor.MiddleCenter;
            _offline.wordWrap = false;
            _offline.clipping = TextClipping.Overflow;
            _offline.normal.textColor = new Color(0.95f, 0.22f, 0.16f);
            _chute = new GUIStyle(GUI.skin.label);
            _chute.fontSize = 20;
            _chute.fontStyle = FontStyle.Bold;
            _chute.alignment = TextAnchor.MiddleCenter;
            _chute.wordWrap = false;
            _chute.clipping = TextClipping.Overflow;
            _chute.normal.textColor = new Color(0.95f, 0.95f, 0.9f);
            _ammo = new GUIStyle(GUI.skin.label);
            _ammo.fontSize = 34;
            _ammo.fontStyle = FontStyle.Bold;
            _ammo.alignment = TextAnchor.UpperRight;
            _ammo.clipping = TextClipping.Overflow;
            _ammo.wordWrap = false;
            _ammo.normal.textColor = new Color(0.95f, 0.97f, 0.92f);
            _hpNum = new GUIStyle(GUI.skin.label);
            _hpNum.fontSize = 28;
            _hpNum.fontStyle = FontStyle.Bold;
            _hpNum.alignment = TextAnchor.MiddleLeft;
            _hpNum.clipping = TextClipping.Overflow;
            _hpNum.wordWrap = false;
            _hpNum.normal.textColor = new Color(0.95f, 0.97f, 0.92f);
            _center = new GUIStyle(GUI.skin.label);
            _center.fontSize = 16;
            _center.fontStyle = FontStyle.Bold;
            _center.alignment = TextAnchor.MiddleCenter;
            _center.wordWrap = false;
            _center.normal.textColor = new Color(0.92f, 0.95f, 0.9f);
            _btn = new GUIStyle(GUI.skin.button);
            _btn.fontSize = 12;
            _btn.alignment = TextAnchor.MiddleLeft;
            _btnOn = new GUIStyle(_btn);
            _btnOn.normal.textColor = new Color(1f, 0.9f, 0.3f);
            _btnOn.fontStyle = FontStyle.Bold;
            _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }
    }
}
