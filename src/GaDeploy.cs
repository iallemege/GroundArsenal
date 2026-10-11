using UnityEngine;

namespace GroundArsenal
{
    internal static class GaDeploy
    {
        internal static bool Open;

        private static bool _closed;
        private static GUIStyle _body;
        private static GUIStyle _btn;
        private static GUIStyle _btnOn;
        private static GUIStyle _title;
        private static bool _styles;
        private static Rect _win = new Rect(40f, 80f, 560f, 420f);

        internal static void Load()
        {
            GaForce.Load();
            GaClass.Load();
        }

        internal static void AfterDeath()
        {
            _closed = true;
            Open = false;
        }

        internal static void Tick()
        {
            if (GaMode.Active || GaMode.IsMenuScene())
            {
                Open = false;
                return;
            }
            if (GaHangar.JoinMenuOpen())
            {
                Open = false;
                return;
            }
            if (!HasHq())
                return;
            Aircraft ac;
            try
            {
                if (GameManager.GetLocalAircraft(out ac) && ac != null)
                {
                    Open = false;
                    return;
                }
            }
            catch { }
            if (!_closed)
                Open = false;
            if (!Open)
                return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _closed = true;
                Open = false;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                GaMode.QueueBranchSpawn();
        }

        internal static void Draw()
        {
            if (!Open || GaMode.Active)
                return;
            Ensure();
            _win = GUI.Window(0x47414450, _win, DrawWin, "DEPLOY  " + GaForce.Label() + " / " + GaClass.Name());
        }

        internal static bool BlocksWorld()
        {
            return Open;
        }

        private static void DrawWin(int id)
        {
            GUILayout.Label("Join a branch, pick a Battlefield kit, then deploy. T boards. B buys aircraft (AIR at airfield, SEA on carrier). LAND cannot buy planes and can ride vehicles.", _body);
            GUILayout.BeginHorizontal();
            ForceBtn(GaForceKind.Land, "LAND  airfield  vehicles  no planes");
            ForceBtn(GaForceKind.Sea, "SEA  ships  carrier planes");
            ForceBtn(GaForceKind.Air, "AIR  airfield  buy planes");
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < GaClass.Count; i++)
            {
                if (i == 4)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                GaKitId k = (GaKitId)i;
                if (GUILayout.Button(GaClass.Name(k), k == GaClass.Picked ? _btnOn : _btn, GUILayout.Height(36f)))
                    GaClass.Set(k);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            string kit = "Kit  " + GaClass.Name() + "   " + GaCombat.ClassCostM().ToString("0.00") + "M";
            GUILayout.Label(kit, _body);
            if (GUILayout.Button("DEPLOY  " + GaForce.Label() + "  " + GaClass.Name(), GUILayout.Height(48f)))
                GaMode.QueueBranchSpawn();
            if (GUILayout.Button("Close (vanilla map / hangar)", GUILayout.Height(28f)))
            {
                _closed = true;
                Open = false;
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static void ForceBtn(GaForceKind k, string lab)
        {
            if (GUILayout.Button(lab, GaForce.Kind == k ? _btnOn : _btn, GUILayout.Height(44f)))
                GaForce.Set(k);
        }

        private static bool HasHq()
        {
            return GaPerfFrameGate.Hq() != null;
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            _body = new GUIStyle(GUI.skin.label);
            _body.fontSize = 14;
            _body.wordWrap = true;
            _body.normal.textColor = Color.white;
            _btn = new GUIStyle(GUI.skin.button);
            _btn.fontSize = 13;
            _btn.wordWrap = true;
            _btnOn = new GUIStyle(_btn);
            _btnOn.fontStyle = FontStyle.Bold;
            _btnOn.normal.textColor = new Color(1f, 0.9f, 0.3f);
            _title = _btnOn;
        }
    }
}
