using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace lionfox_RegenTint
{
    // Shows the healing segment without having to get hurt and bandage up: for five seconds the
    // HUD draws the health bar at 45% with healing to come up to 85%, then goes back to the real
    // values. Only the HUD's drawing changes; the player's health is never touched.
    // HudStatbar only redraws when the health attributes change, so the preview holds until the
    // callback restores it (or until real healing or damage arrives, which simply wins).
    static class Preview
    {
        const int DurationMs = 5000;

        public static TextCommandResult Show(ICoreClientAPI capi)
        {
            GuiElementStatbar? bar = FindHealthBar(capi);
            ITreeAttribute? health = capi.World.Player?.Entity?.WatchedAttributes.GetTreeAttribute("health");
            float max = health?.GetFloat("maxhealth") ?? 0f;
            if (bar == null || max <= 0f)
                return TextCommandResult.Error("RegenTint: couldn't find the health bar to preview on.");

            bar.SetFutureValues(max * 0.85f, 0f);
            bar.SetValue(max * 0.45f);
            capi.Event.RegisterCallback(_ => Restore(capi, bar), DurationMs);
            return TextCommandResult.Success("RegenTint: config reloaded, previewing for 5 seconds.");
        }

        static void Restore(ICoreClientAPI capi, GuiElementStatbar bar)
        {
            ITreeAttribute? health = capi.World.Player?.Entity?.WatchedAttributes.GetTreeAttribute("health");
            if (health == null)
                return;
            bar.SetFutureValues(health.TryGetFloat("futureHealth"), health.GetFloat("healthChangeVelocity"));
            bar.SetValue(health.GetFloat("currenthealth"));
        }

        // HudStatbar lives in VintagestoryLib, not the API, so it is matched by name rather than
        // referenced. Its health bar is the private "healthbar" field.
        static GuiElementStatbar? FindHealthBar(ICoreClientAPI capi)
        {
            foreach (GuiDialog dialog in capi.Gui.LoadedGuis)
            {
                if (dialog.GetType().FullName == "Vintagestory.Client.NoObf.HudStatbar")
                    return Traverse.Create(dialog).Field("healthbar").GetValue() as GuiElementStatbar;
            }
            return null;
        }
    }
}
