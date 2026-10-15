using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Cairo;
using HarmonyLib;
using lionfox_RegenTint;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

static class Program
{
    static string VsPath = "";
    static string OutDir = "";

    static int Main()
    {
        VsPath = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "VS_PATH").Value!;
        string lib = System.IO.Path.Combine(VsPath, "Lib");
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            foreach (string dir in new[] { VsPath, lib })
            {
                string candidate = System.IO.Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(candidate))
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            }
            return null;
        };
        // Load native cairo by full path first, so cairo-sharp's by-name import finds it loaded.
        NativeLibrary.Load(System.IO.Path.Combine(lib, "libcairo-2.dll"));
        OutDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "out"));
        Directory.CreateDirectory(OutDir);
        return Run();
    }

    // Kept out of Main so no game type is touched before the resolver above exists.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Run()
    {
        RuntimeEnv.GUIScale = 2f;
        string? capture = null;
        var gui = Fake<IGuiAPI>((m, a) =>
        {
            if (m.Name == "LoadOrUpdateCairoTexture" && capture != null)
                ((ImageSurface)a![0]!).WriteToPng(System.IO.Path.Combine(OutDir, capture + ".png"));
            return null;
        });
        var events = Fake<IClientEventAPI>((m, a) =>
        {
            if (m.Name == "EnqueueMainThreadTask")
                ((Action)a![0]!)();
            return null;
        });
        var capi = Fake<ICoreClientAPI>((m, _) => m.Name switch
        {
            "get_Gui" => gui,
            "get_Event" => events,
            _ => null,
        });

        // The vanilla HUD health bar: 850 * 0.41 wide, 10 high, one tick per HP. Shown at 45%
        // with healing to come up to 85%, the same picture the in-game preview command draws.
        var bounds = ElementBounds.Fixed(0, 0, 850 * 0.41, 10);
        bounds.ParentBounds = ElementBounds.Empty;
        var bar = new GuiElementStatbar(capi, bounds, GuiStyle.HealthBarColor, false, false);
        bar.SetLineInterval(1f);
        Traverse.Create(bar).Field("maxValue").SetValue(15f);
        Traverse.Create(bar).Field("value").SetValue(15f * 0.45f);
        bar.SetFutureValues(15f * 0.85f, 0f);
        MethodInfo compose = AccessTools.Method(typeof(GuiElementStatbar), "ComposeValueOverlay");

        capture = "base";
        bar.ComposeElements(null!, null!);
        capture = "bar_vanilla";
        compose.Invoke(bar, null);

        FutureSegmentPatch.SetColor(ColorUtil.Hex2Doubles(new RegenTintConfig().Color), new RegenTintConfig().Opacity);
        FutureSegmentPatch.Apply(new Harmony("regentint.rendercheck"), new ConsoleLogger());
        capture = "bar_patched";
        compose.Invoke(bar, null);

        Console.WriteLine($"SitesPatched = {FutureSegmentPatch.SitesPatched}");
        Console.WriteLine($"PNGs in {OutDir}");
        return FutureSegmentPatch.SitesPatched == 2 ? 0 : 1;
    }

    static T Fake<T>(System.Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        T proxy = DispatchProxy.Create<T, FakeProxy>();
        ((FakeProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}

public class FakeProxy : DispatchProxy
{
    public System.Func<MethodInfo, object?[]?, object?> Handler = (_, _) => null;

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        object? result = Handler(method!, args);
        if (result == null && method!.ReturnType.IsValueType && method.ReturnType != typeof(void))
            return Activator.CreateInstance(method.ReturnType);
        return result;
    }
}

class ConsoleLogger : LoggerBase
{
    protected override void LogImpl(EnumLogType logType, string format, params object[] args)
        => Console.WriteLine($"[{logType}] {string.Format(format, args)}");
}
