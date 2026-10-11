using System.Reflection.Emit;
using HarmonyLib;
using JolyMod.Config;
using MelonLoader;

namespace JolyMod.Compatibility
{
    internal static class AntiLogSpam
    {
        static readonly Dictionary<string, (int count, long since, int hidden)> seen = new();

        public static void Start()
        {
            if (!Settings.ModCompatibility) return;
            var bootstrap = typeof(MelonLogger).Assembly.GetType("MelonLoader.InternalUtils.BootstrapInterop");
            var library = AccessTools.Property(bootstrap, "Library").GetValue(null)!;
            Filter(library, "LogMsg", section: 4, texts: new[] { 1, 6 });
            Filter(library, "LogError", section: 2, texts: new[] { 0 });
        }

        static void Filter(object library, string name, int section, int[] texts)
        {
            var property = AccessTools.Property(library.GetType(), name);
            var original = (Delegate)property.GetValue(library)!;
            var invoke = original.GetType().GetMethod("Invoke")!;
            var types = invoke.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var filter = new DynamicMethod(name, null, types.Prepend(original.GetType()).ToArray(), typeof(AntiLogSpam).Module, true);
            var code = filter.GetILGenerator();
            var line = code.DeclareLocal(typeof(string));
            var pass = code.DefineLabel();
            code.Emit(OpCodes.Ldarg, (short)(section + 1));
            code.Emit(OpCodes.Ldarg, (short)(texts[0] + 1));
            code.Emit(OpCodes.Call, AccessTools.Method(typeof(AntiLogSpam), nameof(Pass)));
            code.Emit(OpCodes.Stloc, line);
            code.Emit(OpCodes.Ldloc, line);
            code.Emit(OpCodes.Brtrue_S, pass);
            code.Emit(OpCodes.Ret);
            code.MarkLabel(pass);
            code.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < types.Length; i++)
            {
                if (texts.Contains(i)) code.Emit(OpCodes.Ldloc, line);
                else if (texts.Contains(i - 1))
                {
                    code.Emit(OpCodes.Ldloc, line);
                    code.Emit(OpCodes.Callvirt, typeof(string).GetProperty(nameof(string.Length))!.GetMethod!);
                }
                else code.Emit(OpCodes.Ldarg, (short)(i + 1));
            }
            code.Emit(OpCodes.Callvirt, invoke);
            code.Emit(OpCodes.Ret);
            property.SetValue(library, filter.CreateDelegate(original.GetType(), original));
        }

        static string? Pass(string section, string text)
        {
            lock (seen)
            {
                string key = section + text;
                var last = seen.GetValueOrDefault(key);
                long now = Environment.TickCount64;
                if (last.count < 5)
                {
                    seen[key] = (last.count + 1, now, 0);
                    return text;
                }
                if (now - last.since < 1000)
                {
                    seen[key] = (last.count, last.since, last.hidden + 1);
                    return null;
                }
                seen[key] = (last.count, now, 0);
                return $"[x{last.hidden + 1}] {text}";
            }
        }
    }
}
