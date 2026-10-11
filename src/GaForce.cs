using UnityEngine;

namespace GroundArsenal
{
    internal enum GaForceKind
    {
        None = 0,
        Land = 1,
        Sea = 2,
        Air = 3
    }

    internal static class GaForce
    {
        internal const string Pref = "GroundArsenal.Force";

        internal static GaForceKind Kind = GaForceKind.Land;

        internal static void Load()
        {
            try
            {
                int v = PlayerPrefs.GetInt(Pref, (int)GaForceKind.Land);
                if (v < 1 || v > 3)
                    v = (int)GaForceKind.Land;
                Kind = (GaForceKind)v;
            }
            catch
            {
                Kind = GaForceKind.Air;
            }
        }

        internal static void Set(GaForceKind k)
        {
            if (k == GaForceKind.None)
                k = GaForceKind.Air;
            Kind = k;
            try { PlayerPrefs.SetInt(Pref, (int)k); }
            catch { }
        }

        internal static string Label()
        {
            if (Kind == GaForceKind.Land)
                return "LAND";
            if (Kind == GaForceKind.Sea)
                return "SEA";
            return "AIR";
        }

        internal static bool CanRideVehicle()
        {
            return true;
        }

        internal static bool CanBuyAircraft()
        {
            return Kind == GaForceKind.Air || Kind == GaForceKind.Sea;
        }

        internal static bool AllowBuyAt(Airbase ab)
        {
            if (Kind == GaForceKind.Land)
                return false;
            bool carrier = GaHangar.MenuAirbaseIsCarrier(ab);
            if (Kind == GaForceKind.Sea)
                return carrier;
            if (Kind == GaForceKind.Air)
                return !carrier;
            return false;
        }

        internal static bool AllowBuyNearPlayer()
        {
            if (Kind == GaForceKind.Land)
                return false;
            if (Kind == GaForceKind.Sea)
                return GaMode.Active && GaHangar.PlayerOnCarrierDeck();
            return Kind == GaForceKind.Air;
        }
    }
}
