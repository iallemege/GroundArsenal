using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    /// <summary>Muzzle flash, impact sparks, tracers (LineRenderer — not GL.LINES).</summary>
    internal static class GaFx
    {
        internal static int Live;
        private static float _nextBurstAt;
        private static Material _unlit;
        private static Material _add;
        private static bool _tried;
        private static ParticleSystem _flameJet;

        internal static void Tracer(Vector3 a, Vector3 b, Color c)
        {
            GameObject go = new GameObject("GaTracer");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = 0.055f;
            lr.endWidth = 0.012f;
            lr.allowOcclusionWhenDynamic = false;
            lr.numCapVertices = 1;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Material add = AddMat();
            if (add != null)
                lr.material = add;
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g * 0.45f, c.b * 0.12f, 0f);
            UnityEngine.Object.Destroy(go, 0.16f);
        }

        internal static GameObject MakePath(Vector3 start, Color color, bool bright)
        {
            GameObject go = new GameObject("GaPath");
            go.layer = 0;
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 1;
            lr.SetPosition(0, start);
            lr.startWidth = bright ? 0.055f : 0.028f;
            lr.endWidth = bright ? 0.018f : 0.008f;
            lr.numCapVertices = 1;
            lr.numCornerVertices = 1;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false;
            Material add = AddMat();
            if (add != null)
                lr.material = add;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g * 0.4f, color.b * 0.15f, 0.15f);
            UnityEngine.Object.Destroy(go, 2.4f);
            return go;
        }

        internal static void PushPath(GameObject go, Vector3 p)
        {
            if (go == null)
                return;
            LineRenderer lr = go.GetComponent<LineRenderer>();
            if (lr == null)
                return;
            int n = lr.positionCount;
            if (n >= 1)
            {
                Vector3 last = lr.GetPosition(n - 1);
                if ((p - last).sqrMagnitude < 0.12f)
                    return;
            }
            if (n >= 28)
            {
                for (int i = 1; i < n; i++)
                    lr.SetPosition(i - 1, lr.GetPosition(i));
                lr.SetPosition(n - 1, p);
                return;
            }
            lr.positionCount = n + 1;
            lr.SetPosition(n, p);
        }

        internal static void Spark(Vector3 p, Vector3 n, Color c)
        {
            if (GaPerfFrameGate.SkipPolish() || Live > 10)
                return;
            Vector3 nn = n.sqrMagnitude > 0.01f ? n.normalized : Vector3.up;
            Vector3 at = p + nn * 0.045f;
            SpawnFlash(at, 0.22f, new Color(1f, 0.72f, 0.16f, 1f), 0.07f);
            int nBits = 3;
            for (int i = 0; i < nBits; i++)
            {
                Vector3 dir = (nn * 0.55f + UnityEngine.Random.insideUnitSphere).normalized;
                SpawnBit(at, dir * UnityEngine.Random.Range(6f, 14f),
                    UnityEngine.Random.Range(0.03f, 0.055f),
                    UnityEngine.Random.value > 0.4f
                        ? new Color(1f, 0.62f, 0.12f)
                        : new Color(1f, 0.95f, 0.55f),
                    UnityEngine.Random.Range(0.12f, 0.24f));
            }
        }

        internal static void DressSlug(GameObject go, Color color, float length, bool bright)
        {
            if (go == null)
                return;
            if (length < 0.22f)
                length = 0.22f;
            if (bright && length >= 1.4f && length < 4.2f)
                length = 4.2f;
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(0f, 0f, 0.08f));
            lr.SetPosition(1, new Vector3(0f, 0f, -length));
            lr.startWidth = bright ? 0.062f : 0.028f;
            lr.endWidth = bright ? 0.014f : 0.006f;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false;
            try { lr.alignment = LineAlignment.View; }
            catch { }
            Material add = AddMat();
            if (add != null)
                lr.material = add;
            else
            {
                Material mat = UnlitMat();
                if (mat != null)
                    lr.material = mat;
            }
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g * 0.45f, color.b * 0.12f, 0f);
            TrailRenderer tr = go.AddComponent<TrailRenderer>();
            tr.time = bright ? 0.42f : 0.22f;
            tr.startWidth = bright ? 0.11f : 0.045f;
            tr.endWidth = 0.004f;
            tr.minVertexDistance = 0.25f;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            if (add != null)
                tr.material = add;
            tr.startColor = color;
            tr.endColor = new Color(color.r, color.g * 0.35f, 0.05f, 0f);
            if (!bright) return;
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Tip";
            tip.layer = 0;
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = Vector3.zero;
            tip.transform.localScale = Vector3.one * (bright ? 0.06f : 0.032f);
            GaPhys.Strip(tip);
            Renderer r = tip.GetComponent<Renderer>();
            Material tm = Tint(color);
            if (r != null && tm != null)
            {
                r.sharedMaterial = tm;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.allowOcclusionWhenDynamic = false;
            }
        }

        internal static void Muzzle(Vector3 pos, Vector3 fwd)
        {
            Muzzle(pos, fwd, 1f, false);
        }

        internal static void Muzzle(Vector3 pos, Vector3 fwd, float scale, bool wide)
        {
            if (fwd.sqrMagnitude < 0.01f)
                fwd = Vector3.forward;
            fwd.Normalize();
            if (scale < 0.35f)
                scale = 0.35f;
            if (scale > 2.4f)
                scale = 2.4f;
            Vector3 right;
            Vector3 up;
            Basis(fwd, out right, out up);
            float life = 0.042f;
            FlameLine(pos, pos + fwd * (0.38f * scale), 0.042f * scale, 0.005f * scale,
                new Color(1f, 0.97f, 0.78f, 1f), life);
            FlameLine(pos, pos + fwd * (0.24f * scale), 0.072f * scale, 0.012f * scale,
                new Color(1f, 0.55f, 0.10f, 1f), life * 0.85f);
            float spin = UnityEngine.Random.Range(0f, 60f);
            int petals = (Live > 16) ? (wide ? 3 : 2) : (wide ? 7 : 5);
            for (int i = 0; i < petals; i++)
            {
                float ang = (spin + i * (360f / petals)) * Mathf.Deg2Rad;
                Vector3 radial = right * Mathf.Cos(ang) + up * Mathf.Sin(ang);
                float reach = UnityEngine.Random.Range(0.16f, 0.30f) * scale;
                float flare = (wide ? 0.14f : 0.085f) * scale;
                Vector3 tip = fwd * reach + radial * flare;
                Color petal = i % 2 == 0
                    ? new Color(1f, 0.62f, 0.12f, 1f)
                    : new Color(1f, 0.88f, 0.42f, 1f);
                FlameLine(pos, pos + tip, 0.034f * scale, 0.003f * scale, petal, life);
            }
            PlayFlameJet(pos, fwd, scale, wide);
        }

        internal static void Muzzle(Vector3 pos)
        {
            Vector3 fwd = Vector3.forward;
            Camera cam = GaPerfFrameGate.Cam();
            if (cam != null)
                fwd = cam.transform.forward;
            Muzzle(pos, fwd, 1f, false);
        }

        internal static void Blast(Vector3 pos)
        {
            SpawnFlash(pos, 0.55f, new Color(1f, 0.45f, 0.12f, 1f), 0.12f);
            SpawnFlash(pos, 0.22f, new Color(1f, 0.85f, 0.4f, 1f), 0.08f);
            int bits = GaPerfFrameGate.SkipPolish() ? 2 : 6;
            for (int i = 0; i < bits; i++)
            {
                SpawnBit(pos, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(6f, 18f),
                    UnityEngine.Random.Range(0.02f, 0.045f),
                    new Color(1f, UnityEngine.Random.Range(0.35f, 0.75f), 0.08f),
                    UnityEngine.Random.Range(0.18f, 0.4f));
            }
        }

        internal static void DressBolt(GameObject go, Color color)
        {
            if (go == null)
                return;
            Renderer r = go.GetComponent<Renderer>();
            Material m = Tint(color);
            if (r != null && m != null)
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            TrailRenderer tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.18f;
            tr.startWidth = 0.09f;
            tr.endWidth = 0.01f;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            Material tm = AddMat();
            if (tm != null)
                tr.material = tm;
            tr.startColor = color;
            tr.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        private static Material Tint(Color c)
        {
            Material src = UnlitMat();
            if (src == null)
                return null;
            Material m = new Material(src);
            m.color = c;
            return m;
        }

        private static Material UnlitMat()
        {
            Ensure();
            return _unlit;
        }

        private static Material AddMat()
        {
            Ensure();
            return _add != null ? _add : _unlit;
        }

        private static void SpawnFlash(Vector3 pos, float size, Color color, float life)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GaFlash";
            go.layer = 0;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            GaPhys.Strip(go);
            Renderer r = go.GetComponent<Renderer>();
            Material m = Tint(color);
            if (r != null && m != null)
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Track(go, life, Vector3.zero);
        }

        private static void BurstParticles(Vector3 pos, Vector3 n)
        {
            if (Time.unscaledTime < _nextBurstAt || Live > 24) return;
            _nextBurstAt = Time.unscaledTime + 0.05f;
            try
            {
                GameObject go = new GameObject("GaHitBurst");
                go.transform.position = pos;
                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
                main.duration = 0.2f;
                main.startLifetime = 0.22f;
                main.startSpeed = 8f;
                main.startSize = 0.06f;
                main.startColor = new Color(1f, 0.8f, 0.25f, 1f);
                main.gravityModifier = 1.1f;
                main.maxParticles = 24;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });
                ParticleSystem.ShapeModule sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Hemisphere;
                sh.radius = 0.04f;
                go.transform.rotation = Quaternion.LookRotation(n.sqrMagnitude > 0.01f ? n : Vector3.up);
                ParticleSystemRenderer rr = go.GetComponent<ParticleSystemRenderer>();
                if (rr != null)
                {
                    Material add = AddMat();
                    if (add != null)
                        rr.sharedMaterial = add;
                }
                ps.Play();
                Track(go, 0.45f, Vector3.zero);
            }
            catch { }
        }

        private static void Basis(Vector3 fwd, out Vector3 right, out Vector3 up)
        {
            Vector3 hint = Mathf.Abs(fwd.y) < 0.92f ? Vector3.up : Vector3.right;
            right = Vector3.Cross(hint, fwd);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();
            up = Vector3.Cross(fwd, right);
        }

        private static void FlameLine(Vector3 a, Vector3 b, float startW, float endW, Color c, float life)
        {
            GameObject go = new GameObject("GaFlame");
            go.layer = 0;
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = startW;
            lr.endWidth = endW;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false;
            try { lr.alignment = LineAlignment.View; }
            catch { }
            Material add = AddMat();
            if (add != null)
                lr.material = add;
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g * 0.35f, c.b * 0.08f, 0f);
            UnityEngine.Object.Destroy(go, life);
        }

        private static void PlayFlameJet(Vector3 pos, Vector3 fwd, float scale, bool wide)
        {
            try
            {
                if (_flameJet == null)
                    BuildFlameJet();
                if (_flameJet == null)
                    return;
                Transform t = _flameJet.transform;
                t.position = pos;
                t.rotation = Quaternion.LookRotation(fwd);
                ParticleSystem.MainModule main = _flameJet.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.016f * scale, 0.048f * scale);
                main.startSpeed = new ParticleSystem.MinMaxCurve(
                    (wide ? 5.5f : 4.2f) * scale,
                    (wide ? 11f : 8.5f) * scale);
                ParticleSystem.ShapeModule sh = _flameJet.shape;
                sh.angle = wide ? 24f : 9f;
                sh.radius = 0.0045f * scale;
                int n = wide ? 16 : 9;
                n = Mathf.Clamp(Mathf.RoundToInt(n * scale), 6, 26);
                _flameJet.Emit(n);
            }
            catch { }
        }

        private static void BuildFlameJet()
        {
            GameObject go = new GameObject("GaMuzzleFlame");
            UnityEngine.Object.DontDestroyOnLoad(go);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.032f, 0.068f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.048f);
            main.startColor = new Color(1f, 0.82f, 0.28f, 1f);
            main.gravityModifier = 0f;
            main.maxParticles = 72;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 9f;
            sh.radius = 0.0045f;
            sh.length = 0.02f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.98f, 0.82f), 0f),
                    new GradientColorKey(new Color(1f, 0.52f, 0.10f), 0.42f),
                    new GradientColorKey(new Color(0.45f, 0.08f, 0.02f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.75f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
            sz.enabled = true;
            AnimationCurve shrink = new AnimationCurve();
            shrink.AddKey(0f, 1f);
            shrink.AddKey(1f, 0.12f);
            sz.size = new ParticleSystem.MinMaxCurve(1f, shrink);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 2.4f;
            noise.scrollSpeed = 0.9f;
            ParticleSystemRenderer rr = go.GetComponent<ParticleSystemRenderer>();
            if (rr != null)
            {
                Material add = AddMat();
                if (add != null)
                    rr.sharedMaterial = add;
                rr.shadowCastingMode = ShadowCastingMode.Off;
                rr.receiveShadows = false;
                rr.renderMode = ParticleSystemRenderMode.Stretch;
                rr.velocityScale = 0.06f;
                rr.lengthScale = 2.2f;
                rr.cameraVelocityScale = 0f;
                rr.maxParticleSize = 0.12f;
            }
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _flameJet = ps;
        }

        private static void Track(GameObject go, float life, Vector3 vel)
        {
            Live++;
            GaFxBit bit = go.AddComponent<GaFxBit>();
            bit.Vel = vel;
            bit.DieAt = Time.time + life;
        }

        private static void SpawnBit(Vector3 pos, Vector3 vel, float size, Color color, float life)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "GaSpark";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.transform.rotation = UnityEngine.Random.rotation;
            GaPhys.Strip(go);
            Renderer r = go.GetComponent<Renderer>();
            Material m = Tint(color);
            if (r != null && m != null)
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Track(go, life, vel);
        }

        private static void Ensure()
        {
            if (_tried)
                return;
            _tried = true;
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            if (sh == null)
                sh = Shader.Find("Standard");
            if (sh == null)
                return;
            _unlit = new Material(sh);
            _unlit.hideFlags = HideFlags.HideAndDontSave;
            _unlit.color = Color.white;
            Shader addSh = Shader.Find("Particles/Additive");
            if (addSh == null)
                addSh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (addSh == null)
                addSh = Shader.Find("Mobile/Particles/Additive");
            if (addSh == null)
                addSh = sh;
            _add = new Material(addSh);
            _add.hideFlags = HideFlags.HideAndDontSave;
            _add.color = new Color(1f, 0.85f, 0.35f, 1f);
            try
            {
                _add.SetInt("_SrcBlend", 5);
                _add.SetInt("_DstBlend", 1);
                _add.SetInt("_ZWrite", 0);
                _add.SetInt("_ZTest", 4);
                _add.renderQueue = 3100;
            }
            catch { }
        }
    }

    internal sealed class GaFxBit : MonoBehaviour
    {
        public Vector3 Vel;
        public float DieAt;

        private void Update()
        {
            float dt = Time.deltaTime;
            Vel += Physics.gravity * dt * 1.4f;
            transform.position += Vel * dt;
            if (Time.time >= DieAt)
                UnityEngine.Object.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (GaFx.Live > 0)
                GaFx.Live--;
        }
    }
}
