using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GroundArsenal
{
    /// <summary>
    /// Oritasy-style modular AP on every vehicle / ship / aircraft / building.
    /// Skipped when Oritasy.ModularArmorService is already patched.
    /// </summary>
    internal static class GaVehicleArmor
    {
        private static int _infPen = -1;
        private static Vector3 _infDir;
        private static float _infUntil;
        private static bool _infKe;
        private static readonly Dictionary<int, WeaponInfo> LastWeapon = new Dictionary<int, WeaponInfo>();
        private static FieldInfo _misInfo;
        private static PropertyInfo _misProp;
        private static bool _misInfoTried;

        internal static void RememberInfantryShot(int penGrade, Vector3 incoming, bool pureKe)
        {
            _infPen = GaArmorMath.ClampGrade(penGrade);
            _infDir = incoming;
            _infUntil = Time.unscaledTime + 0.12f;
            _infKe = pureKe;
            GaOritasy.RememberShot(penGrade, incoming, pureKe);
        }

        internal static int PenGradeIncoming(PersistentID dealerID, float pierceDamage)
        {
            Unit dealer = ResolveDealer(dealerID);
            WeaponInfo w = ResolveWeapon(dealer);
            float listed = pierceDamage;
            if (w != null)
            {
                try
                {
                    if (w.pierceDamage > listed)
                        listed = w.pierceDamage;
                }
                catch { }
            }
            Missile mis = dealer as Missile;
            if (mis != null && mis.rb != null)
            {
                float mp = mis.rb.mass * 8f;
                if (mp > listed)
                    listed = mp;
            }
            return GaArmorMath.PenGradeForWeapon(w, listed);
        }

        internal static WeaponInfo WeaponFromDealer(PersistentID dealerID)
        {
            return ResolveWeapon(ResolveDealer(dealerID));
        }

        internal static void RememberStation(Unit owner, WeaponStation station)
        {
            if (owner == null || station == null)
                return;
            WeaponInfo w = null;
            try { w = station.WeaponInfo; }
            catch { }
            if (w == null)
                return;
            LastWeapon[owner.GetInstanceID()] = w;
        }

        internal static void FilterTakeDamage(
            UnitPart part,
            ref float pierceDamage,
            ref float blastDamage,
            ref float fireDamage,
            ref float impactDamage,
            PersistentID dealerID)
        {
            if (part == null)
                return;
            Unit victim = null;
            try { victim = part.parentUnit; }
            catch { }
            if (victim == null)
                return;
            bool vehicle = victim is GroundVehicle;
            bool ship = victim is Ship;
            bool aircraft = victim is Aircraft;
            bool building = victim is Building;
            if (!vehicle && !ship && !aircraft && !building)
                return;

            Unit dealer = ResolveDealer(dealerID);
            int kind = building ? GaArmorMath.KindBuilding : ClassifyUnit(victim);
            Vector3 incoming = Vector3.forward;
            bool infShot = _infPen >= 0 && Time.unscaledTime <= _infUntil;
            if (infShot && _infDir.sqrMagnitude > 0.0001f)
                incoming = _infDir;
            else if (dealer != null && dealer.transform != null && part.transform != null)
                incoming = part.transform.position - dealer.transform.position;
            int face = GaArmorMath.FacingFromIncoming(victim.transform, incoming);
            int tier = 0;
            if (!building && victim.definition != null)
            {
                try { tier = Mathf.RoundToInt(victim.definition.armorTier); }
                catch { }
            }
            int armor = GaArmorMath.ArmorGrade(kind, face, tier);
            if (!building)
            {
                int mod = ClassifyModule(part, vehicle, ship, aircraft);
                armor = GaArmorMath.ClampGrade(armor + GaArmorMath.ModuleArmorAdj(mod));
            }
            int pen;
            int wep;
            WeaponInfo w = ResolveWeapon(dealer);
            if (infShot)
            {
                pen = _infPen;
                wep = _infKe ? GaArmorMath.WepCannon : GaArmorMath.WepRocket;
            }
            else
            {
                float listed = pierceDamage;
                if (w != null)
                {
                    try
                    {
                        if (w.pierceDamage > listed)
                            listed = w.pierceDamage;
                    }
                    catch { }
                }
                pen = GaArmorMath.PenGradeForWeapon(w, listed);
                wep = GaArmorMath.ClassifyKinetic(w);
                if (wep == GaArmorMath.WepUnknown && w != null)
                {
                    try
                    {
                        if (w.gun)
                            wep = GaArmorMath.WepCannon;
                        else if (w.missile)
                            wep = GaArmorMath.WepMissile;
                    }
                    catch { }
                }
            }
            if ((dealer as Missile) != null && wep == GaArmorMath.WepUnknown)
                wep = GaArmorMath.WepMissile;

            bool pureKe = infShot ? _infKe : GaArmorMath.IsPureKeWeapon(wep, w);
            if (GaArmorMath.IsKeNamed(w))
            {
                wep = GaArmorMath.WepCannon;
                pureKe = true;
                blastDamage = 0f;
            }
            if (impactDamage < 1f)
                impactDamage = GaArmorMath.SynthesizeKe(wep, pierceDamage, impactDamage, 0, 0f, 0f);
            if (pureKe && !infShot)
            {
                blastDamage *= 0.06f;
                pierceDamage *= 0.38f;
            }

            pen = GaArmorMath.ApplyPenFloat(pen);
            int hit = GaArmorMath.ResolveHit(pen, armor, wep);
            bool bomb = false;
            if (w != null)
            {
                try { bomb = w.bomb || w.glideBomb; }
                catch { }
            }
            pierceDamage *= GaArmorMath.PierceMul(hit);
            blastDamage *= GaArmorMath.BlastMul(hit, bomb, pureKe);
            impactDamage *= GaArmorMath.ImpactMul(hit);
            if (hit == GaArmorMath.HitBounce || hit == GaArmorMath.HitGlance)
                fireDamage = 0f;
        }

        internal static int ClassifyUnit(Unit u)
        {
            if (u == null)
                return GaArmorMath.KindSoft;
            if (u is Building)
                return GaArmorMath.KindBuilding;
            if (u is Aircraft)
            {
                string blob = UnitBlob(u);
                if (NameHas(blob, "heli") || NameHas(blob, "uh-") || NameHas(blob, "sah"))
                    return GaArmorMath.KindHelo;
                if (NameHas(blob, "bomber") || NameHas(blob, "sfb"))
                    return GaArmorMath.KindBomber;
                if (NameHas(blob, "attack") || NameHas(blob, "cas") || NameHas(blob, "a-10"))
                    return GaArmorMath.KindAttack;
                return GaArmorMath.KindFighter;
            }
            if (u is Ship)
            {
                string blob = UnitBlob(u);
                if (NameHas(blob, "carrier"))
                    return GaArmorMath.KindCarrier;
                if (NameHas(blob, "frigate") || NameHas(blob, "destroyer"))
                    return GaArmorMath.KindEscort;
                return GaArmorMath.KindWarship;
            }
            GroundVehicle gv = u as GroundVehicle;
            if (gv != null)
            {
                try
                {
                    VehicleDefinition vd = gv.definition as VehicleDefinition;
                    if (vd != null)
                    {
                        VehicleType t = vd.vehicleType;
                        if (t == VehicleType.MBT)
                            return GaArmorMath.KindMbt;
                        if (t == VehicleType.TRUCK || t == VehicleType.UGV || t == VehicleType.LCV
                            || t == VehicleType.RDR)
                            return GaArmorMath.KindSoft;
                        return GaArmorMath.KindIfv;
                    }
                }
                catch { }
            }
            return GaArmorMath.KindSoft;
        }

        private static string UnitBlob(Unit u)
        {
            if (u == null || u.definition == null)
                return "";
            UnitDefinition d = u.definition;
            return ((d.unitName != null ? d.unitName : "") + " "
                + (d.code != null ? d.code : "") + " "
                + (d.jsonKey != null ? d.jsonKey : "")).ToLowerInvariant();
        }

        private static bool NameHas(string blob, string token)
        {
            return GaArmorMath.NameHas(blob, token);
        }

        private static int ClassifyModule(UnitPart part, bool vehicle, bool ship, bool aircraft)
        {
            if (part == null)
                return vehicle || ship ? GaArmorMath.ModHull : GaArmorMath.ModAirframe;
            string blob = "";
            try
            {
                blob = ((part.name != null ? part.name : "") + " "
                    + part.GetType().Name).ToLowerInvariant();
            }
            catch { blob = ""; }
            if (NameHas(blob, "ammo") || NameHas(blob, "magazine") || NameHas(blob, "rack")
                || NameHas(blob, "silo") || NameHas(blob, "warhead") || NameHas(blob, "vls"))
                return GaArmorMath.ModAmmo;
            if (NameHas(blob, "fuel") || NameHas(blob, "bunker") || NameHas(blob, "cell"))
                return GaArmorMath.ModFuel;
            if (NameHas(blob, "radar") || NameHas(blob, "sensor") || NameHas(blob, "optic")
                || NameHas(blob, "antenna") || NameHas(blob, "irst"))
                return GaArmorMath.ModSensors;
            if (NameHas(blob, "bridge") || NameHas(blob, "cic") || NameHas(blob, "cockpit")
                || NameHas(blob, "cabin") || NameHas(blob, "crew"))
                return GaArmorMath.ModCommand;
            if (NameHas(blob, "engine") || NameHas(blob, "motor") || NameHas(blob, "turbine")
                || NameHas(blob, "reactor") || NameHas(blob, "propulsion"))
                return GaArmorMath.ModEngine;
            if (NameHas(blob, "track") || NameHas(blob, "wheel") || NameHas(blob, "suspension")
                || NameHas(blob, "sprocket") || NameHas(blob, "landing"))
                return GaArmorMath.ModMobility;
            if (NameHas(blob, "gun") || NameHas(blob, "cannon") || NameHas(blob, "barrel")
                || NameHas(blob, "howitzer") || NameHas(blob, "ciws"))
                return GaArmorMath.ModGun;
            if (NameHas(blob, "turret") || NameHas(blob, "cupola") || NameHas(blob, "barbette"))
                return GaArmorMath.ModTurret;
            if (aircraft)
                return GaArmorMath.ModAirframe;
            return GaArmorMath.ModHull;
        }

        private static WeaponInfo ReadMissileInfo(Missile mis)
        {
            if (mis == null)
                return null;
            if (!_misInfoTried)
            {
                _misInfoTried = true;
                try
                {
                    _misInfo = typeof(Missile).GetField("weaponInfo",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _misProp = typeof(Missile).GetProperty("weaponInfo",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                catch
                {
                    _misInfo = null;
                    _misProp = null;
                }
            }
            try
            {
                if (_misInfo != null)
                    return _misInfo.GetValue(mis) as WeaponInfo;
                if (_misProp != null)
                    return _misProp.GetValue(mis, null) as WeaponInfo;
            }
            catch { }
            return null;
        }

        private static Unit ResolveDealer(PersistentID dealerID)
        {
            if (!dealerID.IsValid)
                return null;
            Unit dealer = null;
            try
            {
                if (!dealerID.TryGetUnit(out dealer))
                    return null;
            }
            catch { return null; }
            return dealer;
        }

        private static WeaponInfo ResolveWeapon(Unit dealer)
        {
            if (dealer == null)
                return null;
            Missile mis = dealer as Missile;
            if (mis != null)
            {
                WeaponInfo fromMis = ReadMissileInfo(mis);
                if (fromMis != null)
                    return fromMis;
            }
            WeaponInfo last;
            if (LastWeapon.TryGetValue(dealer.GetInstanceID(), out last) && last != null)
                return last;
            Aircraft ac = dealer as Aircraft;
            if (ac != null && ac.weaponStations != null)
            {
                WeaponInfo best = null;
                float bestPen = -1f;
                try
                {
                    for (int i = 0; i < ac.weaponStations.Count; i++)
                    {
                        WeaponStation st = ac.weaponStations[i];
                        if (st == null || st.WeaponInfo == null)
                            continue;
                        WeaponInfo w = st.WeaponInfo;
                        if (!w.gun && !w.energy)
                            continue;
                        float p = w.pierceDamage;
                        if (p > bestPen)
                        {
                            bestPen = p;
                            best = w;
                        }
                    }
                }
                catch { }
                if (best != null)
                    return best;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(UnitPart), "TakeDamage")]
    internal static class GaPatch_UnitPartArmor
    {
        [HarmonyPrefix]
        private static void Prefix(
            UnitPart __instance,
            ref float pierceDamage,
            ref float blastDamage,
            ref float fireDamage,
            ref float impactDamage,
            ref PersistentID dealerID)
        {
            if (GaMode.Active && !dealerID.IsValid)
            {
                PersistentID d = GaMode.DealerId();
                if (d.IsValid)
                    dealerID = d;
            }
            if (!GaConfig.IsEnabled() || GaOritasy.HasModularArmor)
                return;
            GaVehicleArmor.FilterTakeDamage(
                __instance, ref pierceDamage, ref blastDamage, ref fireDamage, ref impactDamage, dealerID);
        }
    }

    [HarmonyPatch(typeof(Weapon), "Fire", new System.Type[]
    {
        typeof(Unit), typeof(Unit), typeof(Vector3), typeof(WeaponStation), typeof(GlobalPosition)
    })]
    internal static class GaPatch_WeaponFire
    {
        [HarmonyPostfix]
        private static void Postfix(Unit owner, WeaponStation weaponStation)
        {
            if (!GaConfig.IsEnabled())
                return;
            GaVehicleArmor.RememberStation(owner, weaponStation);
        }
    }
}
