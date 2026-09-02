using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    /// <summary>
    /// First-person kit: downloaded body + mag + slide/bolt. Reload moves those parts.
    /// </summary>
    internal static class GaView
    {
        private static readonly Vector3[] FamPos = new Vector3[]
        {
            new Vector3(0.16f, -0.13f, 0.28f),
            new Vector3(0.16f, -0.13f, 0.30f),
            new Vector3(0.16f, -0.14f, 0.30f),
            new Vector3(0.18f, -0.15f, 0.32f),
            new Vector3(0.20f, -0.17f, 0.36f),
            new Vector3(0.20f, -0.17f, 0.36f),
            new Vector3(0.20f, -0.16f, 0.34f),
            new Vector3(0.20f, -0.16f, 0.34f),
            new Vector3(0.20f, -0.16f, 0.36f),
            new Vector3(0.20f, -0.17f, 0.38f),
            new Vector3(0.20f, -0.17f, 0.40f),
            new Vector3(0.18f, -0.18f, 0.36f),
            new Vector3(0.16f, -0.18f, 0.36f),
            new Vector3(0.16f, -0.22f, 0.40f),
            new Vector3(0.12f, -0.22f, 0.36f),
            new Vector3(0.18f, -0.15f, 0.32f)
        };
        private static readonly float[] FamLen = new float[]
        {
            0.28f, 0.32f, 0.34f, 0.42f, 0.50f, 0.50f, 0.48f, 0.52f,
            0.54f, 0.56f, 0.58f, 0.55f, 0.46f, 0.62f, 0.55f, 0.50f
        };

        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Texture2D> Tex = new Dictionary<string, Texture2D>();
        private static bool _tried;
        private static Material _unlitTex;
        private static Material _unlitCol;
        private static GameObject _root;
        private static GameObject _hold;
        private static GameObject _body;
        private static GameObject _mag;
        private static GameObject _slide;
        private static GameObject _acc;
        private static int _shown = -1;
        private static int _rig;
        private static int _style;
        private static int _animSeed;
        private static float _kickAmt = 0.012f;
        private static float _kickDur = 0.09f;
        private static Vector3 _holdBias;
        private static Vector3 _holdEuler;
        private static Vector3 _magRest;
        private static Vector3 _slideRest;
        private static float _reloadUntil;
        private static float _reloadDur = 1.6f;
        private static float _kickUntil;
        private static float _bodyScale = 1f;
        private static Quaternion _aimRot = Quaternion.Euler(0f, 180f, 0f);

        internal static void Attach(Transform eye)
        {
            Destroy();
            if (eye == null)
                return;
            EnsureMats();
            _root = new GameObject("GaView");
            _root.transform.SetParent(eye, false);
            _root.transform.localPosition = Vector3.zero;
            _root.transform.localRotation = Quaternion.identity;
            _hold = new GameObject("GaHold");
            _hold.transform.SetParent(_root.transform, false);
            Camera cam = Camera.main;
            if (cam != null)
                BindToCamera(cam);
            Show(true);
        }

        internal static void BindToCamera(Camera cam)
        {
            if (_root == null || cam == null)
                return;
            if (_root.transform.parent != cam.transform)
            {
                _root.transform.SetParent(cam.transform, false);
                _root.transform.localRotation = Quaternion.identity;
            }
            ApplyLayer(_root, 0);
        }

        private static void ApplyLayer(GameObject go, int layer)
        {
            if (go == null)
                return;
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                ApplyLayer(t.GetChild(i).gameObject, layer);
        }

        internal static void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _hold = null;
            _body = null;
            _mag = null;
            _slide = null;
            _acc = null;
            _shown = -1;
        }

        internal static void ShowCurrent()
        {
            Show(false);
        }

        internal static void Refresh()
        {
            Show(true);
        }

        internal static void PlayReload(GaGun g)
        {
            int st = AnimStyle(g);
            float d = 1.7f;
            if (st == 4) d = 2.55f;
            else if (st == 5) d = 2.15f;
            else if (st == 2) d = 2.05f;
            else if (st == 3) d = 1.95f;
            else if (st == 0) d = 1.35f;
            else if (st == 7) d = 1.2f;
            else if (st == 6) d = 1.5f;
            else if (st == 8) d = 1.45f;
            else if (st == 9) d = 2.05f;
            if (g != null)
                d += (g.Id % 5) * 0.04f;
            _reloadDur = d;
            _reloadUntil = Time.time + _reloadDur;
        }

        internal static void PlayFire()
        {
            if (Time.time < _reloadUntil)
                return;
            GaGun g = GaCombat.Current;
            if (g != null)
            {
                _kickAmt = 0.007f + g.Damage * 0.0005f + g.Spread * 0.35f;
                float rpm = g.Rpm;
                if (rpm < 40f)
                    rpm = 40f;
                _kickDur = 0.05f + (60f / rpm) * 0.42f;
                if (_kickDur > 0.22f)
                    _kickDur = 0.22f;
            }
            _kickUntil = Time.time + _kickDur;
        }

        internal static void Tick()
        {
            if (_hold == null)
                return;
            Camera cam = Camera.main;
            if (cam != null)
                BindToCamera(cam);
            Show(false);
            bool hide = GaCombat.Ads > 0.42f;
            _hold.SetActive(!hide);
            if (_root != null)
            {
                Vector3 adsOff = Vector3.Lerp(Vector3.zero, new Vector3(-0.18f, 0.06f, -0.08f), GaCombat.Ads);
                _root.transform.localPosition = adsOff;
                _root.transform.localRotation = Quaternion.identity;
            }
            float kick = 0f;
            if (Time.time < _kickUntil && _kickDur > 0.01f)
                kick = (_kickUntil - Time.time) / _kickDur;
            float reload = 0f;
            Vector3 magOff = Vector3.zero;
            Vector3 slideOff = Vector3.zero;
            bool magOn = true;
            if (Time.time < _reloadUntil && _reloadDur > 0.05f)
            {
                reload = 1f - ((_reloadUntil - Time.time) / _reloadDur);
                AnimReload(reload, out magOff, out slideOff, out magOn);
            }
            if (_mag != null)
            {
                _mag.SetActive(magOn);
                _mag.transform.localPosition = _magRest + magOff;
            }
            if (_slide != null)
            {
                Vector3 kickSlide = Vector3.zero;
                if (kick > 0.01f && _style != 4)
                    kickSlide = new Vector3(0f, 0f, (_style == 5 ? 0.04f : 0.018f) * kick);
                _slide.transform.localPosition = _slideRest + slideOff + kickSlide;
            }
            int fam = 0;
            GaGun g = GaCombat.Current;
            if (g != null)
                fam = g.Family;
            if (fam < 0 || fam >= FamPos.Length)
                fam = 0;
            Vector3 pos = FamPos[fam] + _holdBias;
            if (_style == 4)
                pos += new Vector3(0f, 0.04f * reload, -0.08f * reload);
            else if (_style == 5)
                pos += new Vector3(0f, -0.03f * reload, -0.07f * reload);
            else
                pos += new Vector3(0f, -0.02f * reload, -0.04f * reload);
            pos += new Vector3(0f, 0f, -_kickAmt * kick);
            _hold.transform.localPosition = pos;
            float yawAdd = 12f;
            float pitchAdd = 8f;
            float rollAdd = -6f;
            if (_style == 2)
            {
                yawAdd = 22f;
                rollAdd = 14f;
            }
            else if (_style == 4)
            {
                pitchAdd = -28f;
                yawAdd = 4f;
            }
            else if (_style == 5)
            {
                pitchAdd = 16f;
                yawAdd = 6f;
            }
            else if (_style == 3)
            {
                pitchAdd = 4f;
                yawAdd = 8f;
            }
            else if (_style == 8)
            {
                pitchAdd = 10f;
                yawAdd = -8f;
                rollAdd = 10f;
            }
            else if (_style == 9)
            {
                pitchAdd = 14f;
                yawAdd = 16f;
                rollAdd = -10f;
            }
            else if (_style == 0)
            {
                pitchAdd = 6f;
                yawAdd = 18f;
                rollAdd = 8f;
            }
            else if (_style == 7)
            {
                pitchAdd = 5f;
                yawAdd = -10f;
                rollAdd = 4f;
            }
            float kickPitch = (_style == 5 || _style == 9) ? 7f : 4f;
            _hold.transform.localRotation = Quaternion.Euler(
                _holdEuler.x + pitchAdd * reload + kickPitch * kick,
                _holdEuler.y + yawAdd * reload,
                _holdEuler.z + rollAdd * reload);
        }

        private static void AnimReload(float u, out Vector3 magOff, out Vector3 slideOff, out bool magOn)
        {
            magOff = Vector3.zero;
            slideOff = Vector3.zero;
            magOn = true;
            if (_style == 4)
                return;
            if (_style == 2)
            {
                float swing = 0f;
                if (u < 0.32f)
                    swing = u / 0.32f;
                else if (u < 0.68f)
                    swing = 1f;
                else
                    swing = 1f - (u - 0.68f) / 0.32f;
                magOff = new Vector3(0.045f * swing, -0.012f * swing, 0.01f * swing);
                return;
            }
            if (_style == 3)
            {
                float pump = (u > 0.28f && u < 0.78f) ? Mathf.Sin((u - 0.28f) / 0.5f * 3.14159f) : 0f;
                slideOff = new Vector3(0f, 0f, 0.055f * pump);
                magOff = new Vector3(0f, -0.02f * pump, 0f);
                return;
            }
            if (_style == 5)
            {
                if (u < 0.18f)
                    slideOff = new Vector3(0f, 0f, 0.07f * (u / 0.18f));
                else if (u < 0.55f)
                    slideOff = new Vector3(0f, 0f, 0.07f);
                else if (u < 0.85f)
                    slideOff = new Vector3(0f, 0f, 0.07f * (1f - (u - 0.55f) / 0.3f));
                if (u > 0.2f && u < 0.62f)
                {
                    magOff = new Vector3(0f, -0.16f, 0.02f);
                    magOn = u < 0.28f || u > 0.55f;
                }
                return;
            }
            if (_style == 8)
            {
                float t = u < 0.35f ? (u / 0.35f) : (u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f);
                magOff = new Vector3(-0.03f * t, -0.07f * t, 0.01f);
                if (u > 0.75f && u < 0.95f)
                    slideOff = new Vector3(0f, 0f, 0.02f * Mathf.Sin((u - 0.75f) / 0.2f * 3.14159f));
                return;
            }
            if (_style == 9)
            {
                if (u < 0.22f)
                    slideOff = new Vector3(0f, 0f, 0.05f * (u / 0.22f));
                else if (u < 0.78f)
                {
                    slideOff = new Vector3(0f, 0f, 0.05f);
                    magOff = new Vector3(0.02f, -0.14f, 0.03f);
                    magOn = u < 0.3f || u > 0.7f;
                }
                else
                    slideOff = new Vector3(0f, 0f, 0.05f * (1f - (u - 0.78f) / 0.22f));
                return;
            }
            float drop = _style == 7 ? 0.09f : 0.12f;
            float side = ((_animSeed % 3) - 1) * 0.018f;
            if (_style == 0)
                side = 0.03f;
            else if (_style == 7)
                side = -0.025f;
            float outEnd = _style == 7 ? 0.18f : (_style == 0 ? 0.2f : 0.22f);
            float goneEnd = _style == 7 ? 0.4f : 0.48f;
            float inEnd = _style == 7 ? 0.62f : 0.72f;
            if (u < outEnd)
                magOff = new Vector3(side * (u / outEnd), -drop * (u / outEnd), 0.01f);
            else if (u < goneEnd)
            {
                magOff = new Vector3(side, -drop - 0.02f, 0.02f);
                magOn = false;
            }
            else if (u < inEnd)
            {
                float t = (u - goneEnd) / (inEnd - goneEnd);
                magOff = new Vector3(side * (1f - t), -(drop + 0.02f) * (1f - t), 0.02f * (1f - t));
            }
            float boltS = _style == 6 ? 0.018f : 0.028f;
            if (u > inEnd && u < 0.94f)
            {
                float t = (u - inEnd) / (0.94f - inEnd);
                slideOff = new Vector3(0f, 0f, boltS * Mathf.Sin(t * 3.14159f));
            }
        }

        private static int AnimStyle(GaGun g)
        {
            if (g == null)
                return 1;
            if (g.Fire == 4 || g.Body == "at4.obj" || g.Body == "rbs70.obj")
                return 4;
            if (g.Family == 0)
                return 2;
            if (g.Family == 1)
                return 0;
            if (g.Family == 4)
                return 3;
            if (g.Family == 10)
                return 5;
            if (g.Body == "g11.obj")
                return 6;
            if (g.Family == 3 || g.Family == 2)
                return 7;
            if (g.Family == 6)
                return 8;
            if (g.Family == 8)
                return 9;
            return 1;
        }

        private static void Show(bool force)
        {
            GaGun g = GaCombat.Current;
            int id = g != null ? g.Id : 0;
            if (!force && id == _shown && _body != null)
                return;
            if (_hold == null)
                return;
            EnsureMats();
            ClearParts();
            GaKit kit = GaKits.Get(id);
            int fam = g != null ? g.Family : 0;
            if (fam < 0 || fam >= FamPos.Length)
                fam = 0;
            Mesh bodyMesh = LoadMesh(kit != null ? kit.Body : null);
            if (bodyMesh == null)
                bodyMesh = LoadMesh("fam_" + fam.ToString("00") + ".obj");
            _aimRot = AimFromMesh(bodyMesh);
            Material mat = MatFor(kit);
            _body = MakePart("Body", bodyMesh, mat);
            bool cubeBody = false;
            if (_body == null)
            {
                _body = MakeCube("Body", FamLen[fam], mat);
                cubeBody = true;
            }
            _bodyScale = Fit(bodyMesh, FamLen[fam]);
            if (_body != null && !cubeBody)
                _body.transform.localScale = Vector3.one * _bodyScale;
            Mesh magMesh = LoadMesh(kit != null ? kit.Mag : null);
            _mag = MakePart("Mag", magMesh, mat);
            if (_mag != null)
            {
                bool native = kit != null && kit.Body != null
                    && (kit.Body.StartsWith("pistol_") || kit.Body.StartsWith("ak_") || kit.Body.StartsWith("rev_"));
                if (native)
                    _mag.transform.localScale = Vector3.one * _bodyScale;
                else
                {
                    _mag.transform.localScale = Vector3.one * Fit(magMesh, FamLen[fam] * 0.20f);
                    _mag.transform.localPosition = new Vector3(0f, -0.035f, -0.02f);
                }
            }
            Mesh slideMesh = LoadMesh(kit != null ? kit.Slide : null);
            _slide = MakePart("Slide", slideMesh, mat);
            if (_slide != null)
            {
                if (kit != null && kit.Rig >= 1 && kit.Rig <= 2
                    && kit.Body != null
                    && (kit.Body.StartsWith("pistol_") || kit.Body.StartsWith("ak_")))
                    _slide.transform.localScale = Vector3.one * _bodyScale;
                else
                    _slide.transform.localScale = Vector3.one * Fit(slideMesh, FamLen[fam] * 0.18f);
            }
            Mesh accMesh = LoadMesh(kit != null ? kit.Acc : null);
            _acc = MakePart("Acc", accMesh, mat);
            if (_acc != null)
                _acc.transform.localScale = Vector3.one * Fit(accMesh, FamLen[fam] * 0.28f);
            _rig = kit != null ? kit.Rig : 0;
            _style = AnimStyle(g);
            _animSeed = g != null ? g.Id : 0;
            _kickDur = 0.08f;
            _kickAmt = 0.012f;
            _holdBias = Vector3.zero;
            _holdEuler = Vector3.zero;
            if (g != null)
            {
                if (_style == 5)
                {
                    _kickDur = 0.16f;
                    _kickAmt = 0.028f;
                    _holdBias = new Vector3(0.02f, -0.02f, 0.04f);
                }
                else if (_style == 4)
                {
                    _kickDur = 0.14f;
                    _kickAmt = 0.03f;
                    _holdBias = new Vector3(0.04f, -0.04f, 0.02f);
                }
                else if (_style == 3)
                {
                    _kickDur = 0.12f;
                    _kickAmt = 0.022f;
                }
                else if (_style == 0)
                {
                    _kickDur = 0.07f;
                    _kickAmt = 0.01f;
                    _holdBias = new Vector3(-0.02f, 0.01f, -0.02f);
                }
                else if (_style == 2)
                {
                    _kickDur = 0.11f;
                    _kickAmt = 0.018f;
                }
                else if (_style == 7)
                {
                    _kickDur = 0.06f;
                    _kickAmt = 0.008f;
                }
                else if (_style == 8)
                {
                    _kickDur = 0.07f;
                    _kickAmt = 0.011f;
                    _holdBias = new Vector3(-0.015f, 0.01f, -0.03f);
                }
                else if (_style == 9)
                {
                    _kickDur = 0.13f;
                    _kickAmt = 0.02f;
                    _holdBias = new Vector3(0.015f, -0.015f, 0.02f);
                }
                _holdBias += new Vector3((g.Id % 3 - 1) * 0.012f, (g.Id % 4 - 1) * 0.006f, (g.Id % 2) * 0.01f);
                _holdEuler = new Vector3((g.Id % 5 - 2) * 2.4f, (g.Id % 4 - 1) * 1.8f, (g.Id % 3 - 1) * 2.2f);
            }
            _magRest = _mag != null ? _mag.transform.localPosition : Vector3.zero;
            _slideRest = _slide != null ? _slide.transform.localPosition : Vector3.zero;
            _shown = id;
        }

        private static void ClearParts()
        {
            Kill(ref _body);
            Kill(ref _mag);
            Kill(ref _slide);
            Kill(ref _acc);
        }

        private static void Kill(ref GameObject go)
        {
            if (go != null)
                UnityEngine.Object.Destroy(go);
            go = null;
        }

        private static GameObject MakePart(string name, Mesh mesh, Material mat)
        {
            if (mesh == null || _hold == null)
                return null;
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;
            go.layer = 0;
            go.transform.SetParent(_hold.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = _aimRot;
            return go;
        }

        /// <summary>
        /// Quaternius / loafbrr barrels sit on +X. Original infantry meshes sit on -Z.
        /// Aim so the muzzle points down the camera +Z.
        /// </summary>
        private static Quaternion AimFromMesh(Mesh mesh)
        {
            if (mesh == null)
                return Quaternion.Euler(0f, 180f, 0f);
            Vector3 sz = mesh.bounds.size;
            if (sz.x >= sz.z * 1.12f && sz.x >= sz.y)
                return Quaternion.Euler(0f, -90f, 0f);
            if (sz.y > sz.x && sz.y > sz.z)
                return Quaternion.Euler(-90f, 0f, 0f);
            return Quaternion.Euler(0f, 180f, 0f);
        }

        private static GameObject MakeCube(string name, float wantLen, Material mat)
        {
            if (_hold == null)
                return null;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat != null ? mat : _unlitCol;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.allowOcclusionWhenDynamic = false;
            }
            go.layer = 0;
            go.transform.SetParent(_hold.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            float len = wantLen > 0.1f ? wantLen : 0.42f;
            go.transform.localScale = new Vector3(0.05f, 0.05f, len);
            return go;
        }

        private static float Fit(Mesh mesh, float wantLen)
        {
            if (mesh == null || wantLen < 0.02f)
                return 1f;
            Vector3 sz = mesh.bounds.size;
            float longest = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            if (longest < 0.01f)
                return 1f;
            return wantLen / longest;
        }

        private static Mesh LoadMesh(string file)
        {
            if (string.IsNullOrEmpty(file))
                return null;
            Mesh cached;
            if (Meshes.TryGetValue(file, out cached))
                return cached;
            string dir = GaObj.ResolveDir();
            if (string.IsNullOrEmpty(dir))
                return null;
            Mesh m = GaObj.Load(Path.Combine(dir, file));
            Meshes[file] = m;
            return m;
        }

        private static Material MatFor(GaKit kit)
        {
            EnsureMats();
            if (_unlitCol == null)
                return null;
            GaGun g = GaCombat.Current;
            Color paint = Paint(g);
            Texture2D t = null;
            if (g != null)
                t = LoadTex("livery_" + g.Id.ToString("00") + ".png");
            if (t == null)
            {
                string texName = kit != null ? kit.Tex : null;
                if (string.IsNullOrEmpty(texName) && kit != null && !string.IsNullOrEmpty(kit.Body))
                    texName = Path.GetFileNameWithoutExtension(kit.Body) + ".png";
                t = LoadTex(texName);
            }
            if (t == null)
            {
                Material col = new Material(_unlitCol);
                col.color = paint;
                return col;
            }
            Material mat = new Material(_unlitTex);
            mat.mainTexture = t;
            mat.color = Color.Lerp(Color.white, paint, 0.22f);
            return mat;
        }

        private static Color Paint(GaGun g)
        {
            if (g == null)
                return new Color(0.35f, 0.35f, 0.36f);
            float h = (g.Id * 0.6180339f) % 1f;
            float s = 0.48f + (g.Id % 5) * 0.09f;
            float v = 0.58f + ((g.Id * 3) % 7) * 0.05f;
            return Color.HSVToRGB(h, s, v);
        }

        private static Texture2D LoadTex(string file)
        {
            if (string.IsNullOrEmpty(file))
                return null;
            Texture2D cached;
            if (Tex.TryGetValue(file, out cached))
                return cached;
            string dir = GaObj.ResolveDir();
            if (string.IsNullOrEmpty(dir))
                return null;
            Texture2D t = GaObj.LoadPng(Path.Combine(dir, file));
            Tex[file] = t;
            return t;
        }

        private static Shader FindSh()
        {
            Shader sh = Shader.Find("Unlit/Texture");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            if (sh == null)
                sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            if (sh == null)
                sh = Shader.Find("Standard");
            if (sh == null)
                sh = Shader.Find("Diffuse");
            if (sh == null)
                sh = Shader.Find("Legacy Shaders/Diffuse");
            return sh;
        }

        private static void EnsureMats()
        {
            if (_tried && _unlitCol != null && _unlitTex != null)
                return;
            _tried = true;
            Shader sh = FindSh();
            if (sh == null)
            {
                if (GaPlugin.Log != null)
                    GaPlugin.Log.LogWarning("GroundArsenal: no unlit shader for viewmodels");
                return;
            }
            _unlitTex = new Material(sh);
            Shader col = Shader.Find("Unlit/Color");
            if (col == null)
                col = sh;
            _unlitCol = new Material(col);
            _unlitCol.color = new Color(0.55f, 0.56f, 0.58f);
            GaKits.Ensure();
            string dir = GaObj.ResolveDir();
            if (GaPlugin.Log != null)
                GaPlugin.Log.LogInfo("GroundArsenal view kits from " + (dir != null ? dir : "?"));
        }
    }
}
