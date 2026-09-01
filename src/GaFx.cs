using UnityEngine;
using UnityEngine.Rendering;

namespace GroundArsenal
{
    /// <summary>Muzzle flash, impact sparks, tracers (LineRenderer — not GL.LINES).</summary>
    internal static class GaFx
    {
        internal static int Live;
        private static Material _unlit;
        private static Material _add;
        private static bool _tried;

        internal static void Tracer(Vector3 a, Vector3 b, Color c)
        {
            GameObject go = new GameObject("GaTracer");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = 0.018f;
            lr.endWidth = 0.004f;
            lr.numCapVertices = 1;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Material add = AddMat();
            if (add != null)
                lr.material = add;
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g * 0.45f, c.b * 0.12f, 0f);
            UnityEngine.Object.Destroy(go, 0.08f);
        }

        internal static void Spark(Vector3 p, Vector3 n, Color c)
        {
            Vector3 nn = n.sqrMagnitude > 0.01f ? n.normalized : Vector3.up;
            Vector3 at = p + nn * 0.045f;
            SpawnFlash(at, 0.28f, new Color(1f, 0.72f, 0.16f, 1f), 0.08f);
            SpawnFlash(at, 0.11f, new Color(1f, 0.98f, 0.82f, 1f), 0.05f);
            int nBits = 14;
            for (int i = 0; i < nBits; i++)
            {
                Vector3 dir = (nn * 0.55f + UnityEngine.Random.insideUnitSphere).normalized;
                SpawnBit(at, dir * UnityEngine.Random.Range(6f, 16f),
                    UnityEngine.Random.Range(0.035f, 0.07f),
                    UnityEngine.Random.value > 0.4f
                        ? new Color(1f, 0.62f, 0.12f)
                        : new Color(1f, 0.95f, 0.55f),
                    UnityEngine.Random.Range(0.18f, 0.38f));
            }
            BurstParticles(at, nn);
        }

        internal static void Muzzle(Vector3 pos)
        {
            SpawnFlash(pos, 0.055f, new Color(1f, 0.82f, 0.35f, 1f), 0.05f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 v = (UnityEngine.Random.insideUnitSphere + Vector3.forward * 1.4f).normalized
                    * UnityEngine.Random.Range(6f, 14f);
                SpawnBit(pos, v, 0.018f, new Color(1f, 0.7f, 0.2f), 0.07f);
            }
        }

        internal static void Blast(Vector3 pos)
        {
            SpawnFlash(pos, 0.55f, new Color(1f, 0.45f, 0.12f, 1f), 0.12f);
            SpawnFlash(pos, 0.22f, new Color(1f, 0.85f, 0.4f, 1f), 0.08f);
            for (int i = 0; i < 22; i++)
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
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.Destroy(col);
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
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.Destroy(col);
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
            _add = new Material(sh);
            _add.hideFlags = HideFlags.HideAndDontSave;
            _add.color = new Color(1f, 0.85f, 0.35f, 1f);
            try
            {
                _add.SetInt("_SrcBlend", 5);
                _add.SetInt("_DstBlend", 10);
                _add.SetInt("_ZWrite", 0);
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
