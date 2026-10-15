using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace lionfox_RegenTint
{
    // GuiElementStatbar.ComposeValueOverlay draws the "future value" segment - the health a
    // bandage or poultice is still going to give back - with two hardcoded Cairo colours:
    //     ctx.SetSourceRGBA(0.0, 1.0, 0.0, 0.35);   // fill
    //     ctx.SetSourceRGBA(0.0, 0.5, 0.0, 0.35);   // outline
    // and then blurs the whole bar. At 35% over the dark trough that comes out as a dull smear.
    // The transpiler swaps those eight constants for loads of the static fields below. The fields
    // are read each time the bar is recomposed, so a config reload needs no re-patch.
    // Only the health bar ever gets a future value (HudStatbar.UpdateHealth, from the
    // "futureHealth" attribute the server writes during heal-over-time), so the oxygen,
    // saturation and modded bars that share this class are untouched.
    public static class FutureSegmentPatch
    {
        public static double FillR, FillG, FillB, FillA;
        public static double LineR, LineG, LineB, LineA;

        static readonly double[] VanillaFill = { 0.0, 1.0, 0.0, 0.35 };
        static readonly double[] VanillaLine = { 0.0, 0.5, 0.0, 0.35 };

        // How many of the two colour calls the transpiler rewrote. 2 means patched. 0 means the
        // vanilla method no longer matches and was left exactly as it was.
        public static int SitesPatched { get; private set; }

        public static void SetColor(double[] rgb, double opacity)
        {
            FillR = rgb[0]; FillG = rgb[1]; FillB = rgb[2]; FillA = opacity;
            LineR = rgb[0] * 0.4; LineG = rgb[1] * 0.4; LineB = rgb[2] * 0.4; LineA = opacity;
        }

        public static void Apply(Harmony harmony, ILogger logger)
        {
            MethodInfo? target = AccessTools.Method(typeof(GuiElementStatbar), "ComposeValueOverlay");
            if (target == null)
            {
                logger.Warning("GuiElementStatbar.ComposeValueOverlay not found; the health bar is left as vanilla.");
                return;
            }
            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(FutureSegmentPatch), nameof(Transpiler)));
            if (SitesPatched == 2)
                logger.Notification("Patched the health bar's healing segment colour.");
            else
                logger.Warning("The health bar's healing segment colours were not where expected (vanilla changed?); left as vanilla.");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int fillAt = Find(code, VanillaFill);
            int lineAt = Find(code, VanillaLine);
            if (fillAt < 0 || lineAt < 0)
            {
                // Both or neither: half a recolour would be worse than none.
                SitesPatched = 0;
                return code;
            }
            Redirect(code, fillAt, nameof(FillR), nameof(FillG), nameof(FillB), nameof(FillA));
            Redirect(code, lineAt, nameof(LineR), nameof(LineG), nameof(LineB), nameof(LineA));
            SitesPatched = 2;
            return code;
        }

        // Index of the first of four ldc.r8 loads holding exactly these values and followed directly
        // by a SetSourceRGBA call. -1 unless there is exactly one such site.
        static int Find(List<CodeInstruction> code, double[] rgba)
        {
            int found = -1;
            for (int i = 0; i + 4 < code.Count; i++)
            {
                bool match = true;
                for (int k = 0; k < 4 && match; k++)
                    match = code[i + k].opcode == OpCodes.Ldc_R8 && code[i + k].operand is double d && d == rgba[k];
                if (!match || !(code[i + 4].operand is MethodInfo m && m.Name == "SetSourceRGBA"))
                    continue;
                if (found >= 0)
                    return -1;
                found = i;
            }
            return found;
        }

        // Rewritten in place, so any label or exception block on the first load stays put.
        static void Redirect(List<CodeInstruction> code, int at, params string[] fields)
        {
            for (int k = 0; k < 4; k++)
            {
                code[at + k].opcode = OpCodes.Ldsfld;
                code[at + k].operand = AccessTools.Field(typeof(FutureSegmentPatch), fields[k]);
            }
        }
    }
}
