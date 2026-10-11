using UnityEngine;

namespace GroundArsenal
{
    /// <summary>Clone the vanilla ejected-pilot mesh onto AI infantry. Visual only.</summary>
    internal static class GaBody
    {
        private static GameObject _prefab;
        private static bool _tried;
        private static Material _unlit;

        internal static bool Has(Transform parent)
        {
            return parent != null && parent.Find("VanillaPilotBody") != null;
        }

        internal static void Attach(Transform parent)
        {
            Attach(parent, true);
        }

        internal static void Attach(Transform parent, bool near)
        {
            if (parent == null)
                return;
            if (parent.Find("VanillaPilotBody") != null)
                return;
            GameObject src = Resolve();
            if (src == null)
                return;
            bool srcOn = src.activeSelf;
            GameObject vis = null;
            try
            {
                if (srcOn)
                    src.SetActive(false);
                vis = UnityEngine.Object.Instantiate(src);
                if (srcOn)
                    src.SetActive(true);
            }
            catch
            {
                try
                {
                    if (srcOn && src != null)
                        src.SetActive(true);
                }
                catch { }
                return;
            }
            if (vis == null)
                return;
            vis.name = "VanillaPilotBody";
            vis.transform.SetParent(parent, false);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = Vector3.one;
            vis.SetActive(false);
            Strip(vis, near);
            HideChute(vis);
            vis.SetActive(true);
            vis.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            GaBodyPose driver = vis.GetComponent<GaBodyPose>();
            if (driver == null)
                driver = vis.AddComponent<GaBodyPose>();
            Drive(driver, 0f, false);
        }

        internal static Transform Grip(Transform parent)
        {
            if (parent == null)
                return null;
            Transform grip = FindNamed(parent, "GaGrip");
            if (grip != null)
                return grip;
            Transform hand = RightHand(parent);
            GameObject go = new GameObject("GaGrip");
            if (hand != null)
            {
                go.transform.SetParent(hand, false);
                go.transform.localPosition = new Vector3(0.03f, -0.02f, 0.06f);
                go.transform.localRotation = Quaternion.Euler(-8f, 85f, 88f);
            }
            else
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0.22f, 1.12f, 0.28f);
                go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            }
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        internal static Transform RightHand(Transform parent)
        {
            if (parent == null)
                return null;
            Animator anim = parent.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                try
                {
                    if (anim.isHuman)
                    {
                        Transform bone = anim.GetBoneTransform(HumanBodyBones.RightHand);
                        if (bone != null)
                            return bone;
                    }
                }
                catch { }
            }
            return FindHand(parent);
        }

        internal static void PoseHeld(GameObject gun, Transform bodyRoot)
        {
            Transform hand = null;
            FollowHeld(gun, bodyRoot, bodyRoot, ref hand);
        }

        internal static void PoseHeld(GameObject gun, Transform bodyRoot, ref Transform hand)
        {
            FollowHeld(gun, bodyRoot, bodyRoot, ref hand);
        }

        /// <summary>
        /// World-space rifle: parent stays the soldier root (scale 1) so tiny
        /// hand bones cannot explode the mesh. Muzzle follows walk-forward.
        /// </summary>
        internal static void FollowHeld(GameObject gun, Transform character, Transform bodyRoot, ref Transform hand)
        {
            if (gun == null || character == null)
                return;
            if (gun.transform.parent != character)
            {
                gun.transform.SetParent(character, false);
                gun.transform.localScale = Vector3.one;
            }
            if (hand == null)
                hand = RightHand(bodyRoot != null ? bodyRoot : character);
            Vector3 fwd = character.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = character.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            if (right.sqrMagnitude < 0.0001f)
                right = character.right;
            else
                right.Normalize();
            Vector3 pos;
            if (hand != null)
                pos = hand.position + fwd * 0.10f + right * 0.04f + Vector3.up * 0.02f;
            else
                pos = character.position + Vector3.up * 1.16f + right * 0.22f + fwd * 0.20f;
            gun.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
            gun.transform.localScale = Vector3.one;
        }

        private static Transform FindNamed(Transform parent, string name)
        {
            Transform[] xs = parent.GetComponentsInChildren<Transform>(true);
            if (xs == null)
                return null;
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] != null && xs[i].name == name)
                    return xs[i];
            }
            return null;
        }

        private static Transform FindHand(Transform parent)
        {
            Transform[] xs = parent.GetComponentsInChildren<Transform>(true);
            if (xs == null)
                return null;
            Transform best = null;
            int bestScore = -1;
            for (int i = 0; i < xs.Length; i++)
            {
                Transform x = xs[i];
                if (x == null)
                    continue;
                string n = x.name;
                if (string.IsNullOrEmpty(n))
                    continue;
                string l = n.ToLowerInvariant();
                if (IsLeftBone(l))
                    continue;
                int score = -1;
                if (l.IndexOf("righthand") >= 0 || l.IndexOf("hand_r") >= 0
                    || l.IndexOf("right_hand") >= 0 || l.IndexOf("hand.r") >= 0
                    || l.IndexOf("r_hand") >= 0 || l.IndexOf("rhand") >= 0
                    || l.IndexOf("handr") >= 0 || l == "hand.r")
                    score = 10;
                else if (l.IndexOf("hand") >= 0 && l.IndexOf("right") >= 0)
                    score = 8;
                else if (l.IndexOf("wrist") >= 0 && l.IndexOf("right") >= 0)
                    score = 5;
                else if (l.IndexOf("palm") >= 0 && l.IndexOf("right") >= 0)
                    score = 4;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = x;
                }
            }
            if (bestScore < 8)
                return null;
            return best;
        }

        private static bool IsLeftBone(string l)
        {
            if (l.IndexOf("left") >= 0 || l.IndexOf("lhand") >= 0 || l.IndexOf("handl") >= 0)
                return true;
            if (l.IndexOf("hand_l") >= 0 || l.IndexOf("hand.l") >= 0 || l.IndexOf("_l_") >= 0)
                return true;
            if (l.EndsWith("_l") || l.EndsWith(".l") || l.EndsWith(":l"))
                return true;
            return false;
        }

        internal static void Tick(Transform parent, float speed, bool dead)
        {
            if (parent == null)
                return;
            Transform body = parent.Find("VanillaPilotBody");
            if (body == null)
                return;
            GaBodyPose driver = body.GetComponent<GaBodyPose>();
            if (driver == null)
                driver = body.gameObject.AddComponent<GaBodyPose>();
            driver.Speed = speed;
            driver.Dead = dead;
            driver.EnsureAnim();
            Drive(driver, speed, dead);
        }

        internal static void SkinCapsule(Renderer r, Color col)
        {
            if (r == null)
                return;
            Material m = Unlit(col);
            if (m != null)
                r.sharedMaterial = m;
            else
            {
                try
                {
                    if (r.material != null)
                        r.material.color = col;
                }
                catch { }
            }
        }

        internal static void LateDrive(GaBodyPose driver)
        {
            if (driver == null || !driver.gameObject.activeInHierarchy)
                return;
            Drive(driver, driver.Speed, driver.Dead);
            SnapFeet(driver);
        }

        private static void SnapFeet(GaBodyPose driver)
        {
            if (driver == null)
                return;
            Transform vis = driver.transform;
            Transform parent = vis.parent;
            if (parent == null)
                return;
            Animator anim = driver.Anim;
            float minY;
            bool have = false;
            if (anim != null && anim.isHuman)
            {
                Transform lf = null;
                Transform rf = null;
                try
                {
                    lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                }
                catch { }
                minY = parent.position.y;
                if (lf != null)
                {
                    minY = lf.position.y;
                    have = true;
                }
                if (rf != null)
                {
                    float y = rf.position.y;
                    if (!have || y < minY)
                        minY = y;
                    have = true;
                }
            }
            else
            {
                minY = parent.position.y;
            }
            if (!have)
            {
                if (vis.localPosition.y > 0.02f || vis.localPosition.y < -0.02f)
                    vis.localPosition = new Vector3(0f, 0.04f, 0f);
                return;
            }
            float dy = parent.position.y - minY + 0.02f;
            if (dy > -0.03f && dy < 0.03f)
                return;
            if (dy > 0.12f)
                dy = 0.12f;
            if (dy < -0.12f)
                dy = -0.12f;
            Vector3 lp = vis.localPosition;
            lp.y += dy;
            if (lp.y < -0.02f)
                lp.y = -0.02f;
            if (lp.y > 0.22f)
                lp.y = 0.22f;
            vis.localPosition = new Vector3(0f, lp.y, 0f);
        }

        private static void Drive(GaBodyPose driver, float speed, bool dead)
        {
            if (driver == null)
                return;
            driver.EnsureAnim();
            Animator anim = driver.Anim;
            if (anim == null)
                return;
            try
            {
                anim.SetBool("isSeated", false);
                anim.SetBool("isEjecting1", false);
                anim.SetBool("isDetaching", false);
                anim.SetBool("isParachuting", false);
                anim.SetBool("isLanded", false);
                anim.applyRootMotion = false;
                anim.SetInteger("PilotState", dead ? 5 : 0);
                float run = 0f;
                if (!dead && speed > 0.25f)
                    run = Mathf.Clamp(speed * 0.25f, 0.15f, 1.4f);
                anim.SetFloat("RunSpeed", run);
                if (anim.layerCount > 1)
                    anim.SetLayerWeight(1, dead ? 0f : Mathf.Clamp01(speed * 0.35f));
                if (dead)
                {
                    int want = Animator.StringToHash("dead");
                    if (anim.HasState(0, want))
                    {
                        AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(0);
                        if (st.shortNameHash != want)
                            anim.Play(want, 0, 0f);
                    }
                }
            }
            catch { }
        }

        private static GameObject Resolve()
        {
            if (_prefab != null)
                return _prefab;
            if (_tried)
                return null;
            _tried = true;
            PilotDismounted[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<PilotDismounted>(); }
            catch { return null; }
            if (all == null)
                return null;
            int bestScore = -1;
            GameObject best = null;
            for (int i = 0; i < all.Length; i++)
            {
                PilotDismounted pd = all[i];
                if (pd == null)
                    continue;
                GameObject go = pd.gameObject;
                if (go == null)
                    continue;
                string nm = go.name ?? "";
                if (nm.IndexOf("VanillaPilotBody", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (nm.IndexOf("GaDrop", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
                int n = rs != null ? rs.Length : 0;
                if (n <= 0)
                    continue;
                bool prefab = false;
                try { prefab = !go.scene.IsValid() || !go.scene.isLoaded; }
                catch { }
                int score = n + (prefab ? 80 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = go;
                }
            }
            _prefab = best;
            return _prefab;
        }

        private static Material Unlit(Color col)
        {
            if (_unlit == null)
            {
                Shader sh = Shader.Find("Unlit/Color");
                if (sh == null)
                    sh = Shader.Find("Sprites/Default");
                if (sh == null)
                    sh = Shader.Find("UI/Default");
                if (sh == null)
                    return null;
                _unlit = new Material(sh);
            }
            Material m = new Material(_unlit);
            m.color = col;
            return m;
        }

        private static void Strip(GameObject vis, bool near)
        {
            MonoBehaviour[] mbs = vis.GetComponentsInChildren<MonoBehaviour>(true);
            if (mbs != null)
            {
                for (int i = 0; i < mbs.Length; i++)
                {
                    MonoBehaviour mb = mbs[i];
                    if (mb == null || mb is GaBodyPose)
                        continue;
                    mb.enabled = false;
                }
            }
            Collider[] cols = vis.GetComponentsInChildren<Collider>(true);
            if (cols != null)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null)
                        cols[i].enabled = false;
                }
            }
            Rigidbody[] rbs = vis.GetComponentsInChildren<Rigidbody>(true);
            if (rbs != null)
            {
                for (int i = 0; i < rbs.Length; i++)
                {
                    if (rbs[i] == null)
                        continue;
                    rbs[i].isKinematic = true;
                    rbs[i].detectCollisions = false;
                }
            }
            Animator anim = vis.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.enabled = true;
                anim.applyRootMotion = false;
                anim.cullingMode = near
                    ? AnimatorCullingMode.AlwaysAnimate
                    : AnimatorCullingMode.CullUpdateTransforms;
            }
            SkinnedMeshRenderer[] skins = vis.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins != null)
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    if (skins[i] == null)
                        continue;
                    skins[i].updateWhenOffscreen = near;
                    skins[i].skinnedMotionVectors = false;
                    skins[i].quality = near ? SkinQuality.Bone4 : SkinQuality.Bone2;
                }
            }
        }

        private static void HideChute(GameObject vis)
        {
            Transform[] xs = vis.GetComponentsInChildren<Transform>(true);
            if (xs == null)
                return;
            for (int i = 0; i < xs.Length; i++)
            {
                Transform x = xs[i];
                if (x == null || x == vis.transform)
                    continue;
                string n = x.name;
                if (string.IsNullOrEmpty(n))
                    continue;
                string l = n.ToLowerInvariant();
                if (l.IndexOf("chute") >= 0 || l.IndexOf("parachute") >= 0
                    || l.IndexOf("ejectionseat") >= 0 || l.IndexOf("ejection_seat") >= 0
                    || l == "seat")
                    x.gameObject.SetActive(false);
            }
        }
    }

    internal sealed class GaBodyPose : MonoBehaviour
    {
        internal float Speed;
        internal bool Dead;
        internal Animator Anim;

        internal void EnsureAnim()
        {
            if (Anim != null)
                return;
            Anim = GetComponentInChildren<Animator>(true);
        }

        private void LateUpdate()
        {
            GaBody.LateDrive(this);
        }
    }
}
