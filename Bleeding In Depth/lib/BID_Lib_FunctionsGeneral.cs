using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using BleedingInDepth;
using BleedingInDepth.config;
using BleedingInDepth.lib;

namespace BleedingInDepth.lib
{
    internal class BID_Lib_FunctionsGeneral
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;


        //internal static void Log_Debug(string DebugOutput, object[] objects) { if (true) { API.Logger.Debug($"[BleedingInDepth]: {DebugOutput}: {(objects.Length > 0 ? objects[0] ?? "null" : "null")}"); }; } //old backup
        internal static void Log_Debug(string DebugMessage, [CallerMemberName] string caller = "", params object[] loggers) { try { if (Config_Reference.Config_Loaded?.Config_System?.System_DebugLogging ?? true) { API.Logger.Debug($"[BleedingInDepth]: ({caller}) {string.Format(DebugMessage, [.. loggers.Select(a => a ?? "null")])}"); } } catch(Exception e) { BID_Lib_FunctionsGeneral.Log_Error("Caught exception: {0}", loggers: [e.Message]); } }
        internal static void Log_Error(string ErrorMessage, [CallerMemberName] string caller = "", params object[] loggers) { try { API?.Logger?.Error($"[BleedingInDepth]: ({caller}) {string.Format(ErrorMessage, [.. loggers.Select(a => a ?? "null")])}"); } catch(Exception e) { API.Logger.Error(e); } }
        internal static void Log_Debug_Verbose(string DebugMessage, [CallerMemberName] string caller = "", params object[] loggers) { try { if (Config_Reference.Config_Loaded?.Config_System?.System_DebugLogging_Verbose ?? true) { API.Logger.Debug($"[BleedingInDepth][V]: ({caller}) {string.Format(DebugMessage, [.. loggers.Select(a => a ?? "null")])}"); } } catch(Exception e) { BID_Lib_FunctionsGeneral.Log_Error("Caught exception: {0}", loggers: [e.Message]); } }


        internal static float Calc_Curve_SingleSigmoid(float x0, float rate, float max, float offset_X, float offset_Y)
        {
            float returnValue = offset_Y + max * (1 / (1 + MathF.Pow(MathF.E, -rate * (x0 - offset_X))));
            if (returnValue > (max * 0.98f) + offset_Y) { returnValue = max + offset_Y; }
            return returnValue;
        }

        internal static float Calc_Curve_DoubleSigmoid(float x0, float y0, float a1, float rate1, float x1, float a2, float rate2, float x2)//TODO: rename these
        {
            float sigmoid1 = a1 / (1f + MathF.Exp(-rate1 * (x0 - x1)));
            float sigmoid2 = a2 / (1f + MathF.Exp(-rate2 * (x0 - x2)));

            float returnValue = MathF.Round(y0 + sigmoid1 + sigmoid2, 2, MidpointRounding.AwayFromZero);
            if (returnValue > a2 * 0.98f) { returnValue = a2; }
            return returnValue;
        }

        internal static float Calc_Curve_ExpoEaseOut(float x0, float rate, float max, float offset_X, float offset_Y)
        {
            float returnValue = offset_Y + max * (1 - MathF.Pow(2, (-rate * (x0 - offset_X))));
            if (returnValue > (max * 0.98f) + offset_Y) { return max + offset_Y; }
            return returnValue;
        }
    }
}
