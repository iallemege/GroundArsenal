using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NuclearOption.MissionEditorScripts.Buttons;

namespace GroundArsenal
{
    /// <summary>
    /// Infantry kits appear as extra buttons in the vanilla hangar aircraft list.
    /// Fly deploys infantry instead of spawning a plane.
    /// </summary>
    internal static class GaVanillaPick
    {
        internal static bool Armed;

        private static readonly FieldInfo FlyButtonField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "flyButton");
        private static readonly FieldInfo NameField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "aircraftName");
        private static readonly FieldInfo InfoField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "info");
        private static readonly FieldInfo HoverField =
            AccessTools.Field(typeof(AircraftInventoryMenu), "hoverText");
        private static readonly FieldInfo LabelField =
            AccessTools.Field(typeof(AircraftSelectionButton), "label");
        private static readonly FieldInfo OwnedField =
            AccessTools.Field(typeof(AircraftSelectionButton), "ownedValue");
        private static readonly FieldInfo SupplyField =
            AccessTools.Field(typeof(AircraftSelectionButton), "supplyValue");
        private static readonly FieldInfo ImageField =
            AccessTools.Field(typeof(AircraftSelectionButton), "image");
        private static readonly FieldInfo BorderField =
            AccessTools.Field(typeof(AircraftSelectionButton), "border");
        private static readonly FieldInfo BgField =
            AccessTools.Field(typeof(AircraftSelectionButton), "background");

        private static readonly string[] ForceCode = new string[] { "LND", "SEA", "AIR" };
        private static readonly string[] KitCode = new string[]
        {
            "ASLT", "MED", "ENGI", "SUP", "SPEC", "AT", "SNP", "AA"
        };

        internal static void Inject(AircraftInventoryMenu inv)
        {
            if (inv == null || inv.buttonPrefab == null || inv.buttonsContainer == null)
                return;
            if (inv.buttonsContainer.Find("GaInf_LND") != null)
                return;
            int n = inv.buttonsContainer.childCount;
            for (int i = 0; i < n; i++)
            {
                Transform ch = inv.buttonsContainer.GetChild(i);
                if (ch != null)
                    GaFxUi.AttachFade(ch.gameObject, i * 0.028f);
            }
            int slot = n;
            for (int i = 0; i < 3; i++)
                Make(inv, "GaInf_" + ForceCode[i], ForceCode[i], i, -1, slot++);
            for (int i = 0; i < 8; i++)
                Make(inv, "GaInf_" + KitCode[i], KitCode[i], -1, i, slot++);
        }

        internal static void ClickForce(int force)
        {
            Armed = true;
            if (force == 0)
                GaForce.Set(GaForceKind.Land);
            else if (force == 1)
                GaForce.Set(GaForceKind.Sea);
            else
                GaForce.Set(GaForceKind.Air);
            PaintMenu();
        }

        internal static void ClickKit(int kit)
        {
            Armed = true;
            if (kit < 0)
                kit = 0;
            if (kit >= GaClass.Count)
                kit = 0;
            GaClass.Set((GaKitId)kit);
            PaintMenu();
        }

        internal static bool TryFly()
        {
            if (!Armed)
                return false;
            GaMode.QueueBranchSpawn();
            GaHangar.CloseUi();
            Armed = false;
            return true;
        }

        internal static void PaintMenu()
        {
            AircraftSelectionMenu menu = Menu();
            if (menu == null)
                return;
            try
            {
                TMP_Text name = NameField != null ? NameField.GetValue(menu) as TMP_Text : null;
                if (name != null)
                    name.text = "Infantry  " + GaForce.Label() + "  " + GaClass.Name();
            }
            catch { }
            try
            {
                TMP_Text info = InfoField != null ? InfoField.GetValue(menu) as TMP_Text : null;
                if (info != null)
                    info.text = Blurb();
            }
            catch { }
            try
            {
                Button fly = FlyButtonField != null ? FlyButtonField.GetValue(menu) as Button : null;
                if (fly != null)
                {
                    fly.interactable = true;
                    TMP_Text lab = fly.GetComponentInChildren<TMP_Text>(true);
                    if (lab != null)
                        lab.text = "DEPLOY";
                    else
                    {
                        Text t = fly.GetComponentInChildren<Text>(true);
                        if (t != null)
                            t.text = "DEPLOY";
                    }
                }
            }
            catch { }
        }

        internal static void Disarm()
        {
            Armed = false;
        }

        private static void Make(AircraftInventoryMenu inv, string name, string code, int force, int kit, int sibling)
        {
            GameObject go = UnityEngine.Object.Instantiate(inv.buttonPrefab, inv.buttonsContainer);
            go.name = name;
            AircraftSelectionButton asb = go.GetComponent<AircraftSelectionButton>();
            Text label = null;
            Image icon = null;
            Image border = null;
            Image bg = null;
            if (asb != null)
            {
                try
                {
                    if (LabelField != null)
                        label = LabelField.GetValue(asb) as Text;
                    if (OwnedField != null)
                    {
                        Text owned = OwnedField.GetValue(asb) as Text;
                        if (owned != null)
                            owned.enabled = false;
                    }
                    if (SupplyField != null)
                    {
                        Text supply = SupplyField.GetValue(asb) as Text;
                        if (supply != null)
                            supply.enabled = false;
                    }
                    if (ImageField != null)
                        icon = ImageField.GetValue(asb) as Image;
                    if (BorderField != null)
                        border = BorderField.GetValue(asb) as Image;
                    if (BgField != null)
                        bg = BgField.GetValue(asb) as Image;
                }
                catch { }
                UnityEngine.Object.Destroy(asb);
            }
            if (label == null)
                label = go.GetComponentInChildren<Text>(true);
            Color col = Color.white;
            if (label != null)
            {
                label.text = code;
                label.color = col;
            }
            if (icon != null)
                icon.color = col;
            if (border != null)
            {
                border.enabled = false;
                border.color = col;
            }
            if (bg != null)
                bg.color = 0.45f * Color.gray;
            Button btn = go.GetComponentInChildren<Button>(true);
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int f = force;
                int k = kit;
                GameObject goc = go;
                btn.onClick.AddListener(delegate
                {
                    GaFxUi.Pulse(goc);
                    if (f >= 0)
                        ClickForce(f);
                    else
                        ClickKit(k);
                });
            }
            GaFxUi.AttachFade(go, sibling * 0.028f);
            ShowHoverText hover = go.GetComponent<ShowHoverText>();
            if (hover != null)
            {
                try
                {
                    HoverText ht = HoverField != null ? HoverField.GetValue(inv) as HoverText : null;
                    if (ht != null)
                        hover.SetHover(ht);
                }
                catch { }
                if (force >= 0)
                    hover.SetText("Infantry " + ForceCode[force] + "\nBranch spawn");
                else
                    hover.SetText("Infantry " + KitCode[kit] + "\nBattlefield kit");
            }
        }

        private static string Blurb()
        {
            string branch;
            if (GaForce.Kind == GaForceKind.Land)
                branch = "Land: airfield spawn. Ride vehicles. Cannot buy aircraft.";
            else if (GaForce.Kind == GaForceKind.Sea)
                branch = "Sea: ship spawn. Buy aircraft on the carrier.";
            else
                branch = "Air: airfield spawn. Buy aircraft with B.";
            return branch + "\nKit " + GaClass.Name()
                + "\nT board   V radio   Q spot   F frag   C crouch";
        }

        private static AircraftSelectionMenu Menu()
        {
            try
            {
                GameplayUI ui = SceneSingleton<GameplayUI>.i;
                if (ui == null)
                    return null;
                return ui.GetComponentInChildren<AircraftSelectionMenu>(true);
            }
            catch
            {
                return null;
            }
        }
    }
}
