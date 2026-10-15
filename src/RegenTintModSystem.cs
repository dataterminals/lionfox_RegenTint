using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace lionfox_RegenTint
{
    // While a bandage or poultice heals over time, the health bar shows the health still to come
    // as a green segment past the red. Vanilla draws it so faintly it is easy to miss; this mod
    // redraws it in a configurable, much stronger colour. See FutureSegmentPatch for the how.
    // Purely a client-side drawing change: nothing is sent to or needed on the server.
    public class RegenTintModSystem : ModSystem
    {
        public const string HarmonyId = "lionfoxregentint";
        const string ConfigFile = "lionfoxregentint.json";

        ICoreClientAPI? capi;
        Harmony? harmony;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            LoadConfig();
            harmony = new Harmony(HarmonyId);
            FutureSegmentPatch.Apply(harmony, Mod.Logger);

            api.ChatCommands.Create("regentint")
                .WithDescription("Reload the RegenTint config and preview the healing segment on the health bar")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(_ =>
                {
                    LoadConfig();
                    return Preview.Show(api);
                });
        }

        void LoadConfig()
        {
            var defaults = new RegenTintConfig();
            RegenTintConfig? config;
            try
            {
                config = capi!.LoadModConfig<RegenTintConfig>(ConfigFile);
                if (config == null)
                {
                    // First run: write the defaults out so there is a file to edit.
                    config = defaults;
                    capi.StoreModConfig(config, ConfigFile);
                }
            }
            catch (Exception e)
            {
                // A broken file is left alone for the player to fix, never overwritten.
                Mod.Logger.Warning("Could not read ModConfig/{0}, using the defaults: {1}", ConfigFile, e.Message);
                config = defaults;
            }

            string color = config.Color?.Trim() ?? "";
            if (!Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
            {
                Mod.Logger.Warning("Color '{0}' in {1} is not #RRGGBB, using {2}.", config.Color ?? "", ConfigFile, defaults.Color);
                color = defaults.Color;
            }
            FutureSegmentPatch.SetColor(ColorUtil.Hex2Doubles(color), GameMath.Clamp(config.Opacity, 0.0, 1.0));
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }
    }
}
