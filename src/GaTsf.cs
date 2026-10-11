using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    /// <summary>
    /// Muv-Luv F-4 Phantom TSF. Player visor is a true FPS cam + viewmodel.
    /// Bots keep their own hull rig so they cannot steal the player's hands.
    /// </summary>
    internal static class GaTsf
    {
        internal const float Height = 18.5f;
        internal const float Radius = 3.05f;
        internal const float HeadY = 16.2f;
        internal const float Cruise = 220f;
        internal const float Dash = 285f;
        internal const float Climb = 46f;

        internal static bool Jumping;
        private static bool _hover;
        private static Vector3 _lastWish;
        private static float _ctrlTapAt;

        private static GaTsfRig _rig;
        private static Material _bodyMat;
        private static Material _cannonMat;
        private static Material _bladeMat;
        private static Transform _pawn;
        private static float _spd;
        private static Vector3 _vel;
        private static float _roll;
        private static float _leanAmt;
        private static float _dropLie;
        private static float _launchLock;
        private static Mesh _bodyMesh;
        private static Mesh _cannonMesh;
        private static Mesh _bladeMesh;
        private static Mesh _armLMesh;
        private static Mesh _armRMesh;
        private static Vector3 _cannonMuzzleLocal = new Vector3(0f, 0f, 2.7f);
        private static bool _tried;
        private static GUIStyle _hud;
        private static GUIStyle _hudSm;
        private static Texture2D _ring;
        private static bool _hudReady;
        private static int _holdKind = -1;

        internal static bool Owns()
        {
            return GaMode.Active && !GaMode.Dead && !GaMode.Downed
                && !GaRide.Busy && GaClass.Picked == GaKitId.Tsf;
        }

        internal static bool OwnsKit(GaKitId k)
        {
            return k == GaKitId.Tsf;
        }

        internal static void Clear()
        {
            Jumping = false;
            _spd = 0f;
            _vel = Vector3.zero;
            _roll = 0f;
            _leanAmt = 0f;
            _dropLie = 0f;
            _launchLock = 0f;
            _hover = false;
            _lastWish = Vector3.zero;
            _ctrlTapAt = -10f;
            _holdKind = -1;
            _pawn = null;
            DropPlayerRig();
        }

        internal static void OnSpawn(Transform pawn, CharacterController cc, Transform eye)
        {
            Clear();
            _pawn = pawn;
            if (pawn == null || GaClass.Picked != GaKitId.Tsf)
                return;
            GaPhys.TuneTsf(cc);
            ResizeHit(pawn);
            if (eye != null)
                eye.localPosition = new Vector3(0f, HeadY, 0.35f);
            _rig = BuildRig(pawn, true);
            ApplyViewMode();
            HoldLayout(false);
            if (!IsGrounded(pawn.position))
                EnterJump(Vector3.zero, true);
        }

        internal static void ToggleView()
        {
        }

        internal static void AttachBot(Transform host)
        {
            if (host == null)
                return;
            BuildRig(host, false);
        }

        internal static void EnterJump(Vector3 inherit)
        {
            EnterJump(inherit, false);
        }

        internal static void EnterJump(Vector3 inherit, bool airdrop)
        {
            Jumping = true;
            Vector3 flat = new Vector3(inherit.x, 0f, inherit.z);
            float air = flat.magnitude;
            if (_spd > air)
                air = _spd;
            if (airdrop && air < 42f)
                air = 42f;
            _spd = air;
            Vector3 dir = flat.sqrMagnitude > 0.01f ? flat.normalized : LookDir();
            _vel = dir * _spd;
            if (airdrop)
            {
                _vel.y = inherit.y;
                _dropLie = 1f;
                _leanAmt = 1f;
                _launchLock = 2.4f;
            }
            else
            {
                if (_vel.y < 28f)
                    _vel.y = 28f;
                _launchLock = 0.7f;
            }
            _hover = false;
            Vector3 hold = new Vector3(_vel.x, 0f, _vel.z);
            _lastWish = hold.sqrMagnitude > 0.01f ? hold.normalized : LookYaw();
            GaFps.Stance = 0;
            GaChute.Clear();
        }

        internal static bool TryToggle()
        {
            if (!Owns())
                return false;
            if (GaHud.BlocksLook || GaRadio.Open)
                return false;
            if (!Input.GetKeyDown(KeyCode.Space) && !GaVr.JumpDown)
                return false;
            if (Jumping)
            {
                Jumping = false;
                _hover = false;
                _launchLock = 0f;
            }
            else
            {
                Vector3 inherit = LookDir() * GaMode.MoveSpeed;
                inherit.y = 0f;
                EnterJump(inherit, false);
            }
            return true;
        }

        internal static bool Fly(ref float vertVel, ref Vector3 world, Vector3 pos, bool grounded, float dt)
        {
            Pose(dt);
            if (!Owns() || !Jumping)
                return false;
            if (dt < 0.0001f)
                dt = 0.0001f;
            if (_launchLock > 0f)
                _launchLock -= dt;

            TickHoverTap();
            Vector3 yawFwd = LookYaw();
            Vector3 yawRight = Vector3.Cross(Vector3.up, yawFwd);

            Vector3 wish = Vector3.zero;
            if (Input.GetKey(KeyCode.W))
                wish += yawFwd;
            if (Input.GetKey(KeyCode.S))
                wish -= yawFwd;
            if (Input.GetKey(KeyCode.D))
                wish += yawRight;
            if (Input.GetKey(KeyCode.A))
                wish -= yawRight;
            if (Input.GetKey(KeyCode.Z))
                wish += Vector3.up;
            if (Input.GetKey(KeyCode.C))
                wish -= Vector3.up;

            if (wish.sqrMagnitude > 0.0001f)
            {
                _hover = false;
                _lastWish = wish.normalized;
            }

            if (_hover)
            {
                _vel = Vector3.MoveTowards(_vel, Vector3.zero, 120f * dt);
                _spd = new Vector3(_vel.x, 0f, _vel.z).magnitude;
            }
            else
            {
                bool dash = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                float spd = dash ? Dash : Cruise;
                Vector3 wantVel = Vector3.zero;
                if (_lastWish.sqrMagnitude > 0.0001f)
                {
                    Vector3 cmd = _lastWish;
                    wantVel = cmd * spd;
                    if (wantVel.y > Climb)
                        wantVel.y = Climb;
                    if (wantVel.y < -Climb)
                        wantVel.y = -Climb;
                    if (Mathf.Abs(cmd.y) > 0.55f && new Vector3(cmd.x, 0f, cmd.z).sqrMagnitude < 0.08f)
                    {
                        wantVel.x = _vel.x;
                        wantVel.z = _vel.z;
                        wantVel.y = cmd.y > 0f ? Climb : -Climb;
                    }
                }
                _vel = Vector3.MoveTowards(_vel, wantVel, 52f * dt);
                _spd = new Vector3(_vel.x, 0f, _vel.z).magnitude;
            }

            world = _vel;
            vertVel = _vel.y;
            float wantRoll = 0f;
            if (Input.GetKey(KeyCode.A))
                wantRoll = 18f;
            if (Input.GetKey(KeyCode.D))
                wantRoll = -18f;
            _roll = Mathf.MoveTowards(_roll, wantRoll, 80f * dt);
            return true;
        }

        internal static bool Glide(ref float vertVel, ref Vector3 world, bool grounded, float dt)
        {
            if (!Owns() || Jumping)
                return false;
            if (grounded)
            {
                _vel = Vector3.zero;
                _spd = 0f;
                return false;
            }
            if (_vel.sqrMagnitude < 1f)
                return false;
            if (dt < 0.0001f)
                dt = 0.0001f;
            _vel.y += Physics.gravity.y * dt;
            float drag = 5.5f * dt;
            _vel.x = Mathf.MoveTowards(_vel.x, 0f, drag);
            _vel.z = Mathf.MoveTowards(_vel.z, 0f, drag);
            _spd = new Vector3(_vel.x, 0f, _vel.z).magnitude;
            world = _vel;
            vertVel = _vel.y;
            return true;
        }

        internal static void ApplyCam(Camera cam)
        {
            if (cam == null || _pawn == null)
                return;
            Transform eye = GaMode.Eye;
            Quaternion look = _pawn.rotation;
            if (eye != null)
                look = eye.rotation;
            ApplyViewMode();
            Vector3 visor;
            if (_dropLie > 0.08f && _rig != null && _rig.Lean != null)
            {
                visor = _rig.Lean.TransformPoint(new Vector3(0f, HeadY * 0.90f, 1.2f));
                look = _rig.Lean.rotation * Quaternion.Euler(-8f, 0f, 0f);
            }
            else if (eye != null)
                visor = eye.position + look * Vector3.forward * 0.32f;
            else
                visor = _pawn.position + Vector3.up * HeadY + look * Vector3.forward * 0.32f;
            cam.transform.position = visor;
            cam.transform.rotation = look;
            if (cam.nearClipPlane > 0.06f)
                cam.nearClipPlane = 0.05f;
            PlaceViewmodel(cam);
            GaMode.HideAircraftHud();
        }

        internal static float Fov()
        {
            return 70f;
        }

        internal static string Hint()
        {
            return null;
        }

        internal static void Draw()
        {
            if (!Owns() || !GaPerfFrameGate.Paint())
                return;
            EnsureHud();
            GaHotensia.DrawVisor(0.38f, 0.22f,
                new Color(0.02f, 0.12f, 0.06f),
                new Color(0.28f, 0.95f, 0.52f));
            DrawTsfOs();
            GaSpot.Draw();
        }

        private static Vector3 LookDir()
        {
            Transform eye = GaMode.Eye;
            if (_dropLie > 0.12f && _rig != null && _rig.Lean != null)
                return _rig.Lean.forward;
            if (eye != null && eye.forward.sqrMagnitude > 0.0001f)
                return eye.forward;
            if (_pawn != null)
                return _pawn.forward;
            return Vector3.forward;
        }

        private static void DrawTsfOs()
        {
            Texture2D px = GaFxUi.Px;
            if (px == null)
                return;
            float w = Screen.width;
            float h = Screen.height;
            float cx = w * 0.5f;
            float cy = h * 0.48f;
            Color ink = new Color(0.45f, 0.95f, 0.72f, 0.95f);
            Color dim = new Color(0.02f, 0.06f, 0.04f, 0.55f);
            Color prev = GUI.color;

            int spd = Mathf.RoundToInt(Jumping ? _spd * 3.6f : GaMode.MoveSpeed * 3.6f);
            float agl = 0f;
            if (_pawn != null)
                agl = GaPhys.Agl(_pawn.position, 8000f);
            if (agl < 0f)
                agl = 0f;
            float yaw = _pawn != null ? _pawn.eulerAngles.y : 0f;
            if (yaw < 0f)
                yaw += 360f;
            int hdg = Mathf.RoundToInt(yaw) % 360;
            if (hdg < 0)
                hdg += 360;

            GUI.color = ink;
            GUI.Label(new Rect(22f, 10f, 420f, 20f), "TSF-OS  F-4 PHANTOM", _hud);
            string jump = _dropLie > 0.08f ? "JUMP  RECOVER"
                : (Jumping ? "JUMP UNIT  ACTIVE" : "JUMP UNIT  STBY");
            GUI.Label(new Rect(w - 240f, 10f, 220f, 20f), jump, _hud);
            GUI.color = dim;
            GUI.DrawTexture(new Rect(w - 236f, 32f, 200f, 8f), px);
            GUI.color = Jumping
                ? new Color(0.35f, 1f, 0.55f, 0.95f)
                : new Color(0.18f, 0.42f, 0.28f, 0.85f);
            GUI.DrawTexture(new Rect(w - 236f, 32f, 200f * (Jumping ? 1f : 0.18f), 8f), px);

            float hp = GaMode.Hp / GaMode.MaxHp;
            if (hp < 0f) hp = 0f;
            if (hp > 1f) hp = 1f;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(22f, 32f, 200f, 8f), px);
            GUI.color = hp < 0.28f
                ? new Color(1f, 0.35f, 0.2f, 0.95f)
                : ink;
            GUI.DrawTexture(new Rect(22f, 32f, 200f * hp, 8f), px);

            GaGun g = GaCombat.Current;
            bool blade = GaCombat.HeldId == 50;
            int mag = 0;
            int cap = 0;
            int rsv = 0;
            string wep = "WS-16";
            if (g != null)
            {
                wep = g.En;
                mag = GaCombat.Mag[g.Id];
                cap = g.Mag;
                rsv = GaCombat.PoolOf(g);
            }
            string[] bit = new string[6];
            bit[0] = "SUIT      " + Mathf.RoundToInt(GaMode.Hp).ToString();
            bit[1] = blade ? "WS-16     STOW"
                : (wep + "     " + mag.ToString() + "/" + rsv.ToString());
            bit[2] = blade ? "TYPE-77   HOLD" : "TYPE-77   STOW";
            bit[3] = _dropLie > 0.08f ? "JUMP      RECOVER"
                : (Jumping
                    ? (_hover ? "JUMP      HOVER  CTRL×2"
                        : "JUMP      FLIGHT  WASD ZC")
                    : "JUMP      WALK");
            bit[4] = "HDG       " + hdg.ToString("000");
            bit[5] = "SPD       " + spd.ToString("0000") + "  ALT " + Mathf.RoundToInt(agl).ToString("0000");
            float bitH = bit.Length * 13f + 8f;
            GUI.color = new Color(0.01f, 0.05f, 0.03f, 0.42f);
            GUI.DrawTexture(new Rect(16f, 52f, 280f, bitH), px);
            GUI.color = ink;
            for (int i = 0; i < bit.Length; i++)
                GUI.Label(new Rect(22f, 56f + i * 13f, 268f, 13f), bit[i], _hudSm);

            float pr = 42f;
            GUI.color = new Color(ink.r, ink.g, ink.b, 0.78f);
            if (_ring != null)
                GUI.DrawTexture(new Rect(cx - pr, cy - pr, pr * 2f, pr * 2f), _ring, ScaleMode.StretchToFill);
            GUI.color = ink;
            GUI.DrawTexture(new Rect(cx - 8f, cy - 1f, 16f, 2f), px);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 8f, 2f, 16f), px);

            GUI.color = prev;
        }

        private static void EnsureHud()
        {
            if (_hudReady)
                return;
            _hudReady = true;
            Color ink = new Color(0.45f, 0.95f, 0.72f, 1f);
            _hud = new GUIStyle(GUI.skin.label);
            _hud.fontSize = 11;
            _hud.fontStyle = FontStyle.Bold;
            _hud.alignment = TextAnchor.MiddleLeft;
            _hud.wordWrap = false;
            _hud.normal.textColor = ink;
            _hudSm = new GUIStyle(_hud);
            _hudSm.fontSize = 11;
            BuildRing();
        }

        private static void BuildRing()
        {
            int n = 96;
            _ring = new Texture2D(n, n, TextureFormat.ARGB32, false);
            _ring.wrapMode = TextureWrapMode.Clamp;
            _ring.filterMode = FilterMode.Bilinear;
            Color[] c = new Color[n * n];
            float mid = (n - 1) * 0.5f;
            float r0 = n * 0.42f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - mid;
                    float dy = y - mid;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.Abs(r - r0) / 1.6f;
                    if (a < 0f)
                        a = 0f;
                    c[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            _ring.SetPixels(c);
            _ring.Apply(false, true);
        }

        private static bool IsGrounded(Vector3 pos)
        {
            RaycastHit hit;
            return GaPhys.RayWalk(pos, 4f, out hit) && hit.distance < 3.6f;
        }

        private static void ResizeHit(Transform pawn)
        {
            Transform hb = pawn.Find("Hitbox");
            if (hb == null)
                return;
            CapsuleCollider cap = hb.GetComponent<CapsuleCollider>();
            if (cap == null)
                return;
            cap.height = Height;
            cap.radius = Radius;
            cap.center = new Vector3(0f, Height * 0.5f, 0f);
        }

        private static GaTsfRig BuildRig(Transform host, bool player)
        {
            EnsureMeshes();
            if (host == null || _bodyMesh == null)
                return null;
            GaTsfRig rig = host.GetComponent<GaTsfRig>();
            if (rig == null)
                rig = host.gameObject.AddComponent<GaTsfRig>();
            if (rig.Root != null)
                Object.Destroy(rig.Root);
            rig.Player = player;
            rig.Root = new GameObject(player ? "GaTsf" : "GaTsfBot");
            rig.Root.transform.SetParent(host, false);
            rig.Root.transform.localPosition = Vector3.zero;
            rig.Root.transform.localRotation = Quaternion.identity;
            GameObject leanGo = new GameObject("Lean");
            leanGo.transform.SetParent(rig.Root.transform, false);
            rig.Lean = leanGo.transform;
            rig.Body = MakeMesh("F4", _bodyMesh, rig.Lean, BodyMat());
            rig.Body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rig.Body.transform.localPosition = Vector3.zero;
            if (player)
            {
                GameObject hull = new GameObject("HullGun");
                hull.transform.SetParent(rig.Lean, false);
                hull.transform.localPosition = new Vector3(0.35f, HeadY, 1.55f);
                hull.transform.localRotation = Quaternion.identity;
                rig.Hull = hull;
                if (_cannonMesh != null)
                {
                    rig.HullCannon = MakeMesh("WS16H", _cannonMesh, hull.transform, CannonMat());
                    rig.HullCannon.transform.localRotation = Quaternion.identity;
                    rig.HullCannon.transform.localScale = Vector3.one * Fit(_cannonMesh, 5.4f);
                    rig.HullCannon.transform.localPosition = new Vector3(0f, 0f, 2.85f);
                    AttachMuzzle(rig.HullCannon.transform);
                }
                if (_bladeMesh != null)
                {
                    rig.HullBlade = MakeMesh("Type77H", _bladeMesh, hull.transform, BladeMat());
                    rig.HullBlade.transform.localRotation = BladeHold(_bladeMesh);
                    rig.HullBlade.transform.localScale = Vector3.one * Fit(_bladeMesh, 7.2f);
                    rig.HullBlade.transform.localPosition = new Vector3(-0.35f, 0f, 0.45f);
                    rig.HullBlade.SetActive(false);
                }
                GameObject view = new GameObject("View");
                view.transform.SetParent(rig.Root.transform, false);
                rig.View = view;
                GameObject turretGo = new GameObject("Turret");
                turretGo.transform.SetParent(view.transform, false);
                rig.Turret = turretGo.transform;
                rig.Turret.localPosition = Vector3.zero;
                rig.Turret.localRotation = Quaternion.identity;
            }
            else
            {
                GameObject turretGo = new GameObject("Turret");
                turretGo.transform.SetParent(rig.Lean, false);
                rig.Turret = turretGo.transform;
                rig.Turret.localPosition = new Vector3(0.35f, HeadY, 1.55f);
                rig.Turret.localRotation = Quaternion.identity;
            }
            Transform gunParent = rig.Turret;
            if (_cannonMesh != null)
            {
                rig.Cannon = MakeMesh("WS16", _cannonMesh, gunParent, CannonMat());
                float s = Fit(_cannonMesh, player ? 0.72f : 5.4f);
                rig.Cannon.transform.localScale = Vector3.one * s;
                rig.Cannon.transform.localRotation = Quaternion.identity;
                rig.Cannon.transform.localPosition = player
                    ? new Vector3(0f, 0f, 0.18f)
                    : new Vector3(0f, 0f, 2.85f);
                AttachMuzzle(rig.Cannon.transform);
            }
            if (_bladeMesh != null)
            {
                rig.Blade = MakeMesh("Type77", _bladeMesh, gunParent, BladeMat());
                float s = Fit(_bladeMesh, player ? 0.70f : 7.2f);
                rig.Blade.transform.localScale = Vector3.one * s;
                rig.Blade.transform.localRotation = BladeHold(_bladeMesh);
                rig.Blade.transform.localPosition = player
                    ? new Vector3(0.28f, -0.16f, 0.42f)
                    : new Vector3(-0.35f, 0f, 0.45f);
                rig.Blade.SetActive(false);
            }
            if (player)
            {
                rig.LHand = MakeArm("LHand", _armLMesh, rig.Turret);
                rig.RHand = MakeArm("RHand", _armRMesh, rig.Turret);
            }
            Transform cap = host.Find("Body");
            if (cap != null)
            {
                Renderer r = cap.GetComponent<Renderer>();
                if (r != null)
                    r.enabled = false;
                cap.gameObject.SetActive(false);
            }
            return rig;
        }

        private static GameObject MakeArm(string name, Mesh mesh, Transform parent)
        {
            if (mesh == null || parent == null)
                return null;
            GameObject go = MakeMesh(name, mesh, parent, BodyMat());
            float s = Fit(mesh, 0.74f);
            go.transform.localScale = Vector3.one * s;
            return go;
        }

        private static void Pose(float dt)
        {
            if (_rig == null || _rig.Lean == null)
                return;
            if (_dropLie > 0f)
            {
                _dropLie = Mathf.MoveTowards(_dropLie, 0f, dt * 0.42f);
                if (_dropLie < 0.001f)
                    _dropLie = 0f;
            }
            float want = Jumping ? 1f : 0f;
            _leanAmt = Mathf.MoveTowards(_leanAmt, want, dt * 2.4f);
            Quaternion fly = Quaternion.Euler(48f, 0f, _roll * _leanAmt);
            Quaternion stand = Quaternion.identity;
            Quaternion target = Jumping ? fly : stand;
            Quaternion lie = Quaternion.Euler(-92f, 0f, 0f);
            _rig.Lean.localRotation = Quaternion.Slerp(target, lie, _dropLie);
            bool blade = GaCombat.HeldId == 50;
            HoldLayout(blade);
            if (_rig.HullCannon != null)
                _rig.HullCannon.SetActive(!blade);
            if (_rig.HullBlade != null)
                _rig.HullBlade.SetActive(blade);
            ApplyViewMode();
        }

        private static Vector3 LookYaw()
        {
            Vector3 yawFwd = Vector3.ProjectOnPlane(LookDir(), Vector3.up);
            if (yawFwd.sqrMagnitude < 0.0001f && _pawn != null)
                yawFwd = Vector3.ProjectOnPlane(_pawn.forward, Vector3.up);
            if (yawFwd.sqrMagnitude < 0.0001f)
                yawFwd = Vector3.forward;
            return yawFwd.normalized;
        }

        private static void TickHoverTap()
        {
            if (GaHud.BlocksLook || GaRadio.Open)
                return;
            if (!Input.GetKeyDown(KeyCode.LeftControl) && !Input.GetKeyDown(KeyCode.RightControl))
                return;
            float now = Time.unscaledTime;
            if (now - _ctrlTapAt < 0.32f)
            {
                _hover = !_hover;
                _ctrlTapAt = -10f;
                if (_hover)
                    _lastWish = Vector3.zero;
            }
            else
                _ctrlTapAt = now;
        }

        private static void ApplyViewMode()
        {
            if (_rig == null || !_rig.Player)
                return;
            if (_rig.Body != null)
                _rig.Body.SetActive(false);
            if (_rig.Hull != null)
                _rig.Hull.SetActive(false);
            if (_rig.View != null)
                _rig.View.SetActive(true);
            if (_rig.Blade != null && GaCombat.HeldId != 50)
                _rig.Blade.SetActive(false);
        }

        private static void HoldLayout(bool blade)
        {
            if (_rig == null || !_rig.Player)
                return;
            if (_rig.Cannon != null)
                _rig.Cannon.SetActive(!blade);
            if (_rig.Blade != null)
                _rig.Blade.SetActive(blade);
            int kind = blade ? 1 : 0;
            if (kind == _holdKind)
                return;
            _holdKind = kind;
            Vector3 lElbow = new Vector3(-0.28f, -0.20f, 0.10f);
            Vector3 rElbow = new Vector3(0.30f, -0.18f, 0.08f);
            if (blade)
            {
                PlaceArm(_rig.LHand, new Vector3(0.18f, -0.16f, 0.42f), lElbow);
                PlaceArm(_rig.RHand, new Vector3(0.34f, -0.18f, 0.32f), rElbow);
            }
            else
            {
                PlaceArm(_rig.LHand, new Vector3(0.02f, -0.15f, 0.62f), lElbow);
                PlaceArm(_rig.RHand, new Vector3(0.24f, -0.17f, 0.38f), rElbow);
            }
        }

        private static void PlaceArm(GameObject hand, Vector3 grip, Vector3 elbow)
        {
            if (hand == null)
                return;
            hand.transform.localPosition = grip;
            Vector3 along = grip - elbow;
            if (along.sqrMagnitude < 0.0004f)
                along = Vector3.forward;
            hand.transform.localRotation = Quaternion.LookRotation(along.normalized, Vector3.up);
        }

        private static void PlaceViewmodel(Camera cam)
        {
            if (_rig == null || !_rig.Player || _rig.View == null || cam == null)
                return;
            Transform v = _rig.View.transform;
            if (v.parent != cam.transform)
            {
                v.SetParent(cam.transform, false);
                v.localPosition = Vector3.zero;
                v.localRotation = Quaternion.identity;
            }
            if (_rig.Turret == null)
                return;
            _rig.Turret.localPosition = new Vector3(0.22f, -0.22f, 0.40f);
            _rig.Turret.localRotation = Quaternion.identity;
        }

        internal static Vector3 MuzzleWorld()
        {
            Vector3 pos;
            Vector3 dir;
            TryMuzzle(out pos, out dir);
            return pos;
        }

        internal static Vector3 MuzzleDir()
        {
            Vector3 pos;
            Vector3 dir;
            TryMuzzle(out pos, out dir);
            return dir;
        }

        internal static bool TryMuzzle(out Vector3 pos, out Vector3 dir)
        {
            pos = Vector3.zero;
            dir = Vector3.forward;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam != null)
                PlaceViewmodel(cam);
            Transform gun = LiveGun();
            if (gun == null)
                return false;
            Transform m = gun.Find("Muzzle");
            if (m != null)
            {
                pos = m.position;
                dir = m.forward;
            }
            else
            {
                pos = gun.TransformPoint(_cannonMuzzleLocal);
                dir = gun.forward;
            }
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.forward;
            else
                dir.Normalize();
            return true;
        }

        private static Transform LiveGun()
        {
            if (_rig == null)
                return null;
            if (_rig.Cannon != null && _rig.Cannon.activeInHierarchy)
                return _rig.Cannon.transform;
            if (_rig.HullCannon != null && _rig.HullCannon.activeInHierarchy)
                return _rig.HullCannon.transform;
            if (_rig.Cannon != null)
                return _rig.Cannon.transform;
            return null;
        }

        private static void AttachMuzzle(Transform gun)
        {
            if (gun == null)
                return;
            Transform old = gun.Find("Muzzle");
            if (old != null)
                return;
            GameObject muz = new GameObject("Muzzle");
            muz.transform.SetParent(gun, false);
            muz.transform.localPosition = _cannonMuzzleLocal;
            muz.transform.localRotation = Quaternion.identity;
        }

        private static GameObject MakeMesh(string name, Mesh mesh, Transform parent, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            go.layer = 0;
            return go;
        }

        private static Quaternion AimAlongZ(Mesh mesh)
        {
            if (mesh == null)
                return Quaternion.identity;
            Vector3 sz = mesh.bounds.size;
            if (sz.y >= sz.x && sz.y >= sz.z)
                return Quaternion.Euler(-90f, 0f, 0f);
            if (sz.x >= sz.z)
                return Quaternion.Euler(0f, -90f, 0f);
            return Quaternion.identity;
        }

        private static Quaternion BladeHold(Mesh mesh)
        {
            return AimAlongZ(mesh) * Quaternion.Euler(-90f, 0f, 0f);
        }

        private static float Fit(Mesh mesh, float want)
        {
            if (mesh == null || want < 0.05f)
                return 1f;
            Vector3 sz = mesh.bounds.size;
            float longest = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            if (longest < 0.01f)
                return 1f;
            return want / longest;
        }

        private static void EnsureMeshes()
        {
            if (_tried)
                return;
            _tried = true;
            string dir = GaObj.ResolveDir();
            if (string.IsNullOrEmpty(dir))
                return;
            _bodyMesh = GaObj.Load(System.IO.Path.Combine(dir, "f4.obj"), false);
            _cannonMesh = GaObj.Load(System.IO.Path.Combine(dir, "ws16.obj"), true);
            _bladeMesh = GaObj.Load(System.IO.Path.Combine(dir, "type77.obj"), true);
            _armLMesh = GaObj.Load(System.IO.Path.Combine(dir, "f4_arm_l.obj"), false);
            _armRMesh = GaObj.Load(System.IO.Path.Combine(dir, "f4_arm_r.obj"), false);
            _cannonMuzzleLocal = MuzzleLocalOf(_cannonMesh);
        }

        private static Vector3 MuzzleLocalOf(Mesh mesh)
        {
            if (mesh == null)
                return new Vector3(0f, 0f, 2.7f);
            Vector3[] verts = mesh.vertices;
            if (verts == null || verts.Length == 0)
                return mesh.bounds.max.z >= 0f
                    ? new Vector3(0f, 0f, mesh.bounds.max.z)
                    : new Vector3(0f, 0f, 2.7f);
            float zHi = mesh.bounds.max.z;
            float zLo = mesh.bounds.min.z;
            float band = (zHi - zLo) * 0.045f;
            if (band < 0.04f)
                band = 0.04f;
            Vector3 sumHi = Vector3.zero;
            Vector3 sumLo = Vector3.zero;
            int nHi = 0;
            int nLo = 0;
            float rHi = 0f;
            float rLo = 0f;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                if (v.z > zHi - band)
                {
                    sumHi += v;
                    nHi++;
                    float r = v.x * v.x + v.y * v.y;
                    if (r > rHi)
                        rHi = r;
                }
                if (v.z < zLo + band)
                {
                    sumLo += v;
                    nLo++;
                    float r = v.x * v.x + v.y * v.y;
                    if (r > rLo)
                        rLo = r;
                }
            }
            Vector3 hi = nHi > 0 ? sumHi / nHi : new Vector3(0f, 0f, zHi);
            Vector3 lo = nLo > 0 ? sumLo / nLo : new Vector3(0f, 0f, zLo);
            return rHi <= rLo ? hi : lo;
        }

        private static Material BodyMat()
        {
            if (_bodyMat == null)
                _bodyMat = PaintTex("f4.png", "Ga_TsfBody");
            return _bodyMat;
        }

        private static Material CannonMat()
        {
            if (_cannonMat == null)
                _cannonMat = PaintTex("ws16.png", "Ga_TsfWs16");
            return _cannonMat;
        }

        private static Material BladeMat()
        {
            if (_bladeMat == null)
                _bladeMat = PaintTex("type77.png", "Ga_TsfType77");
            return _bladeMat;
        }

        private static Material PaintTex(string file, string matName)
        {
            Texture2D tex = null;
            string dir = GaObj.ResolveDir();
            if (!string.IsNullOrEmpty(dir))
                tex = GaObj.LoadPng(System.IO.Path.Combine(dir, file));
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.anisoLevel = 0;
            }
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null)
                sh = Shader.Find("Unlit/Texture");
            if (sh == null)
                sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (sh == null)
                sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null)
                sh = Shader.Find("Standard");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            Material mat = new Material(sh);
            mat.name = matName;
            Color tint = Color.white;
            if (tex == null)
                tint = new Color(0.44f, 0.51f, 0.53f, 1f);
            mat.color = tint;
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
            if (mat.HasProperty("_BaseMap") && tex != null)
                mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex") && tex != null)
                mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", tint);
                if (tex != null && mat.HasProperty("_EmissionMap"))
                    mat.SetTexture("_EmissionMap", tex);
                mat.EnableKeyword("_EMISSION");
            }
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.22f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.04f);
            return mat;
        }

        private static void DropPlayerRig()
        {
            if (_rig != null && _rig.Root != null)
                Object.Destroy(_rig.Root);
            if (_rig != null)
                Object.Destroy(_rig);
            _rig = null;
        }
    }

    internal sealed class GaTsfRig : MonoBehaviour
    {
        internal bool Player;
        internal GameObject Root;
        internal Transform Lean;
        internal Transform Turret;
        internal GameObject Body;
        internal GameObject View;
        internal GameObject Hull;
        internal GameObject HullCannon;
        internal GameObject HullBlade;
        internal GameObject Cannon;
        internal GameObject Blade;
        internal GameObject LHand;
        internal GameObject RHand;
    }
}
