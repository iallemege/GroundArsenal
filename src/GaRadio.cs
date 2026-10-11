using System;
using System.Collections.Generic;
using System.Reflection;
using NuclearOption.Networking;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>
    /// Infantry radio in the OritasyRadio / Ace Combat Zero subtitle style,
    /// plus Battlefield 2 V-comms. Forwards to OritasyRadio when that dll is loaded.
    /// </summary>
    internal static class GaRadio
    {
        internal static bool Open;

        private const float GlobalGap = 1.15f;
        private const float SpeakerGap = 4.2f;
        private const float HoldSec = 3.05f;
        private const int HistoryCap = 8;

        private static Line _cur;
        private static bool _has;
        private static float _nextGlobal;
        private static GUIStyle _call;
        private static GUIStyle _quote;
        private static GUIStyle _log;
        private static Texture2D _px;
        private static bool _styles;
        private static float _vDownAt = -1f;
        private static bool _skipMelee;
        private static readonly List<Line> History = new List<Line>(HistoryCap + 2);
        private static readonly Dictionary<int, float> LastSpeak = new Dictionary<int, float>(24);
        private static readonly string[] Medic = new string[]
        {
            "I need a medic!", "Medic!", "I'm hit, need a medic!",
            "Somebody get a medic over here!", "Doc, I'm bleeding!"
        };
        private static readonly string[] Ammo = new string[]
        {
            "I need ammo!", "Ammo!", "I'm out, need a resupply!", "Need ammo here!"
        };
        private static readonly string[] Spot = new string[]
        {
            "Enemy spotted!", "Contact!", "Hostile infantry!", "Got eyes on the enemy!",
            "Infantry, twelve o'clock!"
        };
        private static readonly string[] Backup = new string[]
        {
            "I need backup!", "Need assistance!", "Taking fire!", "They're all over us!"
        };
        private static readonly string[] Thanks = new string[] { "Thanks!", "Appreciate it.", "Good looking out." };
        private static readonly string[] Go = new string[] { "Go go go!", "Move up!", "Push forward!", "On me!" };
        private static readonly string[] Hold = new string[] { "Hold this position!", "Hold here!", "Stay put!" };
        private static readonly string[] Roger = new string[] { "Roger that.", "Copy.", "Wilco.", "Affirmative." };
        private static readonly string[] Neg = new string[] { "Negative.", "No can do.", "Can't do that." };
        private static readonly string[] Reload = new string[] { "Reloading!", "Cover me, reloading!", "Changing mags!" };
        private static readonly string[] Kill = new string[] { "Got one!", "Enemy down!", "He's down!", "Kill confirmed!" };
        private static readonly string[] Incoming = new string[] { "Incoming!", "Get down!", "Incoming fire!" };
        private static readonly string[] Frag = new string[] { "Frag out!", "Grenade!", "Throwing frag!" };
        private static readonly string[] Down = new string[] { "I'm down! I need a medic!", "Man down!", "I'm hit, I'm down!" };
        private static readonly string[] MedicGo = new string[] { "Hold on, medic inbound!", "I'm coming!", "Hang on, I'll patch you!" };
        private static readonly string[] Contact = new string[] { "Contact front!", "Engaging!", "Opening fire!" };
        private static readonly string[] Cover = new string[] { "Covering fire!", "I got you covered!" };
        private static readonly string[] Rose = new string[]
        {
            "1  Medic", "2  Ammo", "3  Spotted", "4  Backup",
            "5  Thanks", "6  Go", "7  Hold", "8  Roger",
            "9  Gunship", "0  CAS"
        };

        private static MethodInfo _crew;
        private static bool _crewTried;
        private static readonly object[] _fwdArgs = new object[4];
        private static string _you = "YOU";
        private static float _youUntil;
        private static float _playerHurtAt;
        private static float _playerDownAt;
        private static float _medicReplyAt;
        private static float _nextAuto;
        private static float _nextNet;
        private static string[] _burst;
        private static string _burstCall;
        private static int _burstI;
        private static float _burstAt;
        private static float _burstGap;

        private struct Line
        {
            public string Callsign;
            public string Quote;
            public string Body;
            public string Log;
            public bool Enemy;
            public float Born;
            public float Dies;
        }

        internal static void Clear()
        {
            Open = false;
            _has = false;
            _vDownAt = -1f;
            _burst = null;
            _burstI = 0;
            History.Clear();
            LastSpeak.Clear();
        }

        internal static void OnSpawn()
        {
            Clear();
            Say(PlayerCall(), "Squad deployed. " + GaForce.Label() + ", " + GaClass.Name() + ".", false, 4);
        }

        internal static string PlayerCall()
        {
            float now = Time.unscaledTime;
            if (now < _youUntil)
                return _you;
            _youUntil = now + 3f;
            _you = "YOU";
            try
            {
                Player p;
                if (GameManager.GetLocalPlayer(out p) && p != null)
                {
                    string n = p.GetDisplayName(PlayerNameContext.ChatOrLeaderboard);
                    if (!string.IsNullOrEmpty(n))
                        _you = n.ToUpperInvariant();
                }
            }
            catch { }
            return _you;
        }

        internal static string BotCall(GaDropBot bot)
        {
            if (bot == null)
                return "UNKNOWN";
            return bot.Callsign;
        }

        internal static void Tick()
        {
            float now = Time.unscaledTime;
            TickBurst(now);
            if (_has && now >= _cur.Dies)
                _has = false;
            TickCommo();
            if (GaMode.Active && !GaMode.Dead)
                TickAuto(now);
        }

        internal static void SayMedic(GaDropBot bot)
        {
            SpeakBot(bot, Pick(Medic), 9);
        }

        internal static void SayContact(GaDropBot bot)
        {
            SpeakBot(bot, Pick(Contact), 6);
        }

        internal static void SayKill(GaDropBot bot)
        {
            SpeakBot(bot, Pick(Kill), 7);
        }

        internal static void SayCover(GaDropBot bot)
        {
            SpeakBot(bot, Pick(Cover), 5);
        }

        internal static void ConfirmKill()
        {
            Say(PlayerCall(), Pick(Kill), false, 7);
        }

        internal static void PlayerReload()
        {
            Say(PlayerCall(), Pick(Reload), false, 4);
        }

        internal static void PlayerFrag()
        {
            Say(PlayerCall(), Pick(Frag), false, 6);
        }

        internal static void PlayerHurt()
        {
            float now = Time.unscaledTime;
            if (now < _playerHurtAt)
                return;
            _playerHurtAt = now + 8f;
            Say(PlayerCall(), Pick(Medic), false, 9);
        }

        internal static void PlayerDown()
        {
            float now = Time.unscaledTime;
            if (now < _playerDownAt)
                return;
            _playerDownAt = now + 5f;
            Say(PlayerCall(), Pick(Down), false, 10);
        }

        internal static void SquadDown(GaDropBot bot)
        {
            SpeakBot(bot, Pick(Down), 10);
        }

        internal static void SpotCall()
        {
            Say(PlayerCall(), Pick(Spot), false, 7);
        }

        internal static void IncomingFire()
        {
            Say(PlayerCall(), Pick(Incoming), false, 6);
        }

        internal static void Burst(string callsign, string[] parts, float gap)
        {
            if (parts == null || parts.Length == 0)
                return;
            _burst = parts;
            _burstCall = string.IsNullOrEmpty(callsign) ? "RADIO" : callsign;
            _burstI = 0;
            _burstGap = gap < 0.15f ? 0.15f : gap;
            _burstAt = 0f;
            TickBurst(Time.unscaledTime);
        }

        internal static void Say(string callsign, string quote, bool enemy, int priority)
        {
            if (string.IsNullOrEmpty(quote))
                return;
            if (string.IsNullOrEmpty(callsign))
                callsign = "UNKNOWN";
            float now = Time.unscaledTime;
            if (priority < 9 && now < _nextGlobal)
                return;
            Line ln;
            ln.Callsign = callsign;
            ln.Quote = quote;
            ln.Enemy = enemy;
            ln.Body = "<< " + quote + " >>";
            ln.Log = callsign + ": " + quote;
            ln.Born = now;
            ln.Dies = now + HoldSec;
            _cur = ln;
            _has = true;
            _nextGlobal = now + GlobalGap;
            Push(ln);
            Forward(callsign, quote, enemy, priority);
        }

        internal static string Tip()
        {
            if (!_has)
                return null;
            if (string.IsNullOrEmpty(_cur.Quote))
                return null;
            string call = string.IsNullOrEmpty(_cur.Callsign) ? "RADIO" : _cur.Callsign;
            return call + ": " + _cur.Quote;
        }

        internal static void Draw()
        {
            Ensure();
            DrawSubtitle();
            float a = GaFxUi.Smooth(GaFxUi.Radio);
            if (a < 0.02f)
                return;
            DrawRose(a);
        }

        private static void TickBurst(float now)
        {
            if (_burst == null)
                return;
            if (_burstI >= _burst.Length)
            {
                _burst = null;
                return;
            }
            if (_burstI > 0 && now < _burstAt)
                return;
            Say(_burstCall, _burst[_burstI], false, 10);
            _burstI++;
            _burstAt = now + _burstGap;
            if (_burstI >= _burst.Length)
                _burst = null;
        }

        private static void TickCommo()
        {
            if (!GaMode.Active || GaMode.Dead)
            {
                Open = false;
                _vDownAt = -1f;
                return;
            }
            if (GaHud.BlocksLook && !Open)
                return;
            if (Input.GetKeyDown(KeyCode.V))
                _vDownAt = Time.unscaledTime;
            if (Input.GetKeyUp(KeyCode.V))
            {
                bool skip = _skipMelee;
                _skipMelee = false;
                float held = _vDownAt < 0f ? 99f : Time.unscaledTime - _vDownAt;
                Open = false;
                _vDownAt = -1f;
                if (!skip && held >= 0f && held < 0.22f)
                {
                    if (GaTsf.Owns())
                        GaTsf.ToggleView();
                    else
                        GaFps.Melee();
                }
                return;
            }
            if (Input.GetKey(KeyCode.V) && _vDownAt >= 0f && Time.unscaledTime - _vDownAt >= 0.22f)
            {
                Open = true;
                for (int i = 0; i < 8; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    {
                        FireCommo(i);
                        Open = false;
                        _vDownAt = -1f;
                        _skipMelee = true;
                        return;
                    }
                }
                if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))
                {
                    FireCommo(8);
                    Open = false;
                    _vDownAt = -1f;
                    _skipMelee = true;
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
                {
                    FireCommo(9);
                    Open = false;
                    _vDownAt = -1f;
                    _skipMelee = true;
                    return;
                }
            }
            else
                Open = false;
        }

        private static void FireCommo(int i)
        {
            switch (i)
            {
                case 0:
                    Say(PlayerCall(), Pick(Medic), false, 9);
                    break;
                case 1:
                    Say(PlayerCall(), Pick(Ammo), false, 6);
                    break;
                case 2:
                    GaFps.SpotFromEye();
                    break;
                case 3:
                    Say(PlayerCall(), Pick(Backup), false, 8);
                    break;
                case 4:
                    Say(PlayerCall(), Pick(Thanks), false, 4);
                    break;
                case 5:
                    Say(PlayerCall(), Pick(Go), false, 6);
                    GaDropSquad.OrderFollow();
                    break;
                case 6:
                    Say(PlayerCall(), Pick(Hold), false, 6);
                    GaDropSquad.OrderHold();
                    break;
                case 8:
                    if (GaSupport.RequestGunship())
                        Say(PlayerCall(), "Requesting gunship support on marked position.", false, 10);
                    break;
                case 9:
                    if (GaSupport.RequestCas())
                        Say(PlayerCall(), "Requesting CAS on marked position.", false, 10);
                    break;
                default:
                    Say(PlayerCall(), Pick(Roger), false, 3);
                    break;
            }
        }

        private static void TickAuto(float now)
        {
            if (GaMode.Downed)
            {
                if (now >= _playerDownAt)
                    PlayerDown();
                return;
            }
            if (GaMode.Hp < 22f)
                PlayerHurt();
            TickNet(now);
            if (now < _nextAuto)
                return;
            _nextAuto = now + 0.45f;
            if (now < _medicReplyAt)
                return;
            if (GaMode.Hp < 28f || GaMode.Downed)
            {
                GaDropBot med = GaDropSquad.NearestKit(GaMode.EyePos, true, GaKitId.Medic);
                if (med != null && (med.transform.position - GaMode.EyePos).sqrMagnitude < 1600f)
                {
                    _medicReplyAt = now + 10f;
                    SpeakBot(med, Pick(MedicGo), 9);
                }
            }
        }

        private static void SpeakBot(GaDropBot bot, string quote, int pri)
        {
            if (bot == null || string.IsNullOrEmpty(quote))
                return;
            if (!HearBot(bot, pri))
                return;
            int id = bot.GetInstanceID();
            float last;
            if (LastSpeak.TryGetValue(id, out last) && Time.unscaledTime < last + SpeakerGap && pri < 9)
                return;
            LastSpeak[id] = Time.unscaledTime;
            if (bot.Friendly && bot.Kit == GaClass.Picked && pri < 8)
                pri = 8;
            quote = MaybeJam(quote);
            Say(bot.Callsign, quote, !bot.Friendly, pri);
        }

        private static bool HearBot(GaDropBot bot, int pri)
        {
            if (bot == null)
                return false;
            Vector3 ear = GaMode.EyePos;
            float d2 = (bot.transform.position - ear).sqrMagnitude;
            if (d2 > 140f * 140f && pri < 9)
                return false;
            if (bot.Friendly && bot.Kit == GaClass.Picked && d2 <= 95f * 95f)
                return true;
            if (pri >= 9 && d2 <= 130f * 130f)
                return true;
            if (d2 <= 72f * 72f && UnityEngine.Random.value < 0.16f)
                return true;
            return false;
        }

        private static string MaybeJam(string quote)
        {
            float roll = UnityEngine.Random.value;
            if (roll > 0.22f)
                return quote;
            if (roll < 0.10f)
                return CrossTalk(quote);
            return Scramble(quote);
        }

        private static readonly string[] JamNets = new string[] { "NET-2", "NET-4", "EW", "UNK" };

        private static string CrossTalk(string quote)
        {
            string net = JamNets[UnityEngine.Random.Range(0, JamNets.Length)];
            return "[" + net + "] " + quote;
        }

        private static string Scramble(string quote)
        {
            if (string.IsNullOrEmpty(quote))
                return "kzzzt";
            char[] c = quote.ToCharArray();
            int n = c.Length;
            int hits = 1 + n / 7;
            for (int i = 0; i < hits; i++)
            {
                int at = UnityEngine.Random.Range(0, n);
                c[at] = "k#z- "[UnityEngine.Random.Range(0, 5)];
            }
            string body = new string(c);
            if (UnityEngine.Random.value < 0.45f)
                return "kzzzt— " + body;
            return body + " —shh";
        }

        private static readonly string[] NetTalk = new string[]
        {
            "Contact front!", "Moving!", "Reloading!", "I got you covered!",
            "Push up!", "Hold here!", "Eyes on!", "He's down!",
            "Need a mag!", "On your six!"
        };
        private static readonly string[] AtTalk = new string[]
        {
            "Tank front!", "Launcher up!", "Keep clear of the backblast!"
        };
        private static readonly string[] MedTalk = new string[]
        {
            "Stay with me!", "Cover me, patching!", "Man down, moving!"
        };
        private static readonly string[] SupTalk = new string[]
        {
            "Ammo here!", "Suppressing!", "Saw them, lighting it up!"
        };

        private static void TickNet(float now)
        {
            if (now < _nextNet)
                return;
            _nextNet = now + UnityEngine.Random.Range(2.6f, 5.4f);
            Vector3 ear = GaMode.EyePos;
            GaDropBot bot = GaDropSquad.NearestKit(ear, true, GaClass.Picked);
            bool same = bot != null && (bot.transform.position - ear).sqrMagnitude <= 95f * 95f;
            if (!same)
            {
                if (UnityEngine.Random.value > 0.20f)
                    return;
                bot = GaDropSquad.NearbyAny(ear, 70f);
                if (bot == null)
                    return;
            }
            string line = Pick(NetTalk);
            if (bot.Kit == GaKitId.AntiTank)
                line = Pick(AtTalk);
            else if (bot.Kit == GaKitId.Medic)
                line = Pick(MedTalk);
            else if (bot.Kit == GaKitId.Support)
                line = Pick(SupTalk);
            int pri = same ? 8 : 5;
            SpeakBot(bot, line, pri);
        }

        private static void Forward(string call, string quote, bool enemy, int pri)
        {
            if (!_crewTried)
            {
                _crewTried = true;
                try
                {
                    Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < asms.Length; i++)
                    {
                        Type t = asms[i].GetType("OritasyRadio.AceRadioChatter");
                        if (t == null)
                            continue;
                        _crew = t.GetMethod("TryQueueCrew",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                            null,
                            new Type[] { typeof(string), typeof(string), typeof(bool), typeof(int) },
                            null);
                        if (_crew != null)
                            break;
                    }
                }
                catch { }
            }
            if (_crew == null)
                return;
            _fwdArgs[0] = call;
            _fwdArgs[1] = quote;
            _fwdArgs[2] = enemy;
            _fwdArgs[3] = pri < 1 ? 6 : pri;
            try { _crew.Invoke(null, _fwdArgs); }
            catch { }
        }

        private static void DrawSubtitle()
        {
            if (!_has && History.Count == 0)
                return;
            Line ln = _has ? _cur : History[History.Count - 1];
            float now = Time.unscaledTime;
            float age = now - ln.Born;
            float left = ln.Dies - now;
            float alpha = 1f;
            if (age < 0.12f)
                alpha = Mathf.Clamp01(age / 0.12f);
            else if (left < 0.4f)
                alpha = Mathf.Clamp01(left / 0.4f);
            if (alpha <= 0.05f)
                return;
            string call = string.IsNullOrEmpty(ln.Callsign) ? "UNKNOWN" : ln.Callsign;
            Color nameCol = ln.Enemy
                ? new Color(1f, 0.28f, 0.28f, 0.98f * alpha)
                : new Color(0.45f, 0.78f, 1f, 0.98f * alpha);
            Color bodyCol = new Color(1f, 1f, 1f, alpha);
            string body = ln.Body;
            if (string.IsNullOrEmpty(body))
                body = "<< " + ln.Quote + " >>";
            float w = Mathf.Clamp(Screen.width * 0.72f, 480f, 1100f);
            float x = (Screen.width - w) * 0.5f;
            float y = Mathf.Max(36f, Screen.height * 0.065f);
            _call.normal.textColor = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.Label(new Rect(x + 1.5f, y + 1.5f, w, 28f), call, _call);
            _call.normal.textColor = nameCol;
            GUI.Label(new Rect(x, y, w, 28f), call, _call);
            _quote.normal.textColor = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.Label(new Rect(x + 1.5f, y + 30f, w, 32f), body, _quote);
            _quote.normal.textColor = bodyCol;
            GUI.Label(new Rect(x, y + 28.5f, w, 32f), body, _quote);
        }

        private static void DrawHistory()
        {
            if (History.Count == 0)
                return;
            Ensure();
            float x = 16f;
            float y = 178f;
            Color prev = GUI.color;
            GUI.color = new Color(0.02f, 0.05f, 0.07f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, 268f, 18f + History.Count * 16f), _px);
            GUI.color = Color.white;
            for (int i = 0; i < History.Count; i++)
            {
                Line ln = History[i];
                _log.normal.textColor = ln.Enemy
                    ? new Color(1f, 0.42f, 0.32f, 0.92f)
                    : new Color(0.55f, 0.88f, 1f, 0.92f);
                GUI.Label(new Rect(x + 6f, y + 4f + i * 16f, 256f, 16f),
                    string.IsNullOrEmpty(ln.Log) ? (ln.Callsign + ": " + ln.Quote) : ln.Log, _log);
            }
            GUI.color = prev;
        }

        private static void DrawRose(float a)
        {
            Ensure();
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color prev = GUI.color;
            GaFxUi.Push(a, new Vector2(cx, cy), 18f);
            Color prevFill = GUI.color;
            GUI.color = new Color(0.02f, 0.05f, 0.06f, 0.78f * a);
            GUI.DrawTexture(new Rect(cx - 150f, cy - 148f, 300f, 312f), _px);
            GUI.color = prevFill;
            GUI.color = new Color(0.92f, 0.95f, 0.9f, a);
            GUI.Label(new Rect(cx - 140f, cy - 140f, 280f, 22f), "RADIO  (V)", _call);
            for (int i = 0; i < Rose.Length; i++)
            {
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(new Rect(cx - 120f, cy - 108f + i * 24f, 240f, 22f), Rose[i], _quote);
            }
            GaFxUi.Pop();
            GUI.color = prev;
        }

        private static void Push(Line ln)
        {
            History.Add(ln);
            while (History.Count > HistoryCap)
                History.RemoveAt(0);
        }

        private static string Pick(string[] bank)
        {
            if (bank == null || bank.Length == 0)
                return "";
            return bank[UnityEngine.Random.Range(0, bank.Length)];
        }

        private static void Ensure()
        {
            if (_styles)
                return;
            _styles = true;
            Font f = null;
            try { if (GUI.skin != null) f = GUI.skin.font; }
            catch { }
            _call = new GUIStyle(GUI.skin.label);
            _call.alignment = TextAnchor.MiddleCenter;
            _call.fontStyle = FontStyle.Bold;
            _call.fontSize = 22;
            _call.wordWrap = false;
            if (f != null) _call.font = f;
            _quote = new GUIStyle(GUI.skin.label);
            _quote.alignment = TextAnchor.MiddleCenter;
            _quote.fontSize = 20;
            _quote.wordWrap = false;
            if (f != null) _quote.font = f;
            _log = new GUIStyle(GUI.skin.label);
            _log.alignment = TextAnchor.MiddleLeft;
            _log.fontSize = 12;
            _log.wordWrap = false;
            _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }
    }
}
