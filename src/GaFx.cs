using UnityEngine;

namespace GroundArsenal
{
    internal static class GaFx
    {
        private static Material _mat;

        internal static void Tracer(Vector3 a, Vector3 b, Color c)
        {
            Material m = Mat();
            if (m == null)
                return;
            Color old = m.color;
            m.color = c;
            GL.PushMatrix();
            m.SetPass(0);
            GL.Begin(GL.LINES);
            GL.Color(c);
            GL.Vertex(a);
            GL.Vertex(b);
            GL.End();
            GL.PopMatrix();
            m.color = old;
        }

        internal static void Spark(Vector3 p, Vector3 n, Color c)
        {
            Debug.DrawRay(p, n * 0.4f, c, 0.12f, false);
        }

        private static Material Mat()
        {
            if (_mat != null)
                return _mat;
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            if (sh == null)
                return null;
            _mat = new Material(sh);
            _mat.hideFlags = HideFlags.HideAndDontSave;
            return _mat;
        }
    }
}
