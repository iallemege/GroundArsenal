using NuclearOption.Networking;
using UnityEngine;

namespace GroundArsenal
{
    internal static class GaFunds
    {
        internal static float Read()
        {
            NuclearOption.Networking.Player player = null;
            try
            {
                if (!GameManager.GetLocalPlayer(out player) || player == null)
                    return 0f;
                return player.Allocation;
            }
            catch
            {
                return 0f;
            }
        }

        internal static bool CanPay(float costM)
        {
            if (costM <= 0.001f)
                return true;
            return Read() + 0.001f >= costM;
        }

        internal static bool TryPay(float costM)
        {
            if (costM <= 0.001f)
                return true;
            NuclearOption.Networking.Player player = null;
            try
            {
                if (!GameManager.GetLocalPlayer(out player) || player == null)
                    return false;
                if (player.Allocation + 0.001f < costM)
                    return false;
                player.AddAllocation(-costM);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
