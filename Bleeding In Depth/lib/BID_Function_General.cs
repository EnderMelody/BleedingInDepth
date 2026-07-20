using BleedingInDepth.config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace BleedingInDepth.lib
{
    internal class BID_Function_General
    {
        private static ICoreAPI API = BID_VarRef.API;


        internal static void Log_Debug(string DebugMessage, [CallerMemberName] string caller = "", params object[] loggers) { try { if (Config_Reference.Config_Loaded?.Config_System?.System_Debug_Acc.DebugLogging ?? true) { API.Logger.Debug($"[{BID_VarRef.ModName_Trunc}]: ({caller}) {string.Format(DebugMessage, [.. loggers.Select(a => a ?? "null")])}"); } } catch (Exception e) { BID_Function_General.Log_Error("Caught exception: {0}", loggers: [e.Message]); } }
        internal static void Log_Error(string ErrorMessage, [CallerMemberName] string caller = "", params object[] loggers) { try { API?.Logger?.Error($"[{BID_VarRef.ModName_Trunc}]: ({caller}) {string.Format(ErrorMessage, [.. loggers.Select(a => a ?? "null")])}"); } catch (Exception e) { API.Logger.Error(e); } }
        internal static void Log_Debug_Verbose(string DebugMessage, [CallerMemberName] string caller = "", params object[] loggers) { try { if (Config_Reference.Config_Loaded?.Config_System?.System_Debug_Acc.DebugLogging_Verbose ?? true) { API.Logger.Debug($"[{BID_VarRef.ModName_Trunc}][V]: ({caller}) {string.Format(DebugMessage, [.. loggers.Select(a => a ?? "null")])}"); } } catch (Exception e) { BID_Function_General.Log_Error("Caught exception: {0}", loggers: [e.Message]); } }


        internal static float Calc_Curve_SingleSigmoid(float x0, float rate, float max, float offset_X, float offset_Y)
        {
            float returnValue = offset_Y + max * (1 / (1 + MathF.Pow(MathF.E, -rate * (x0 - offset_X))));
            if (returnValue > (max * 0.98f) + offset_Y) { returnValue = max + offset_Y; }
            return returnValue;
        }

        internal static float Calc_Curve_DoubleSigmoid(float input_x, float offset_Y0, float max1, float rate1, float offset_X1, float max2, float rate2, float offset_X2)//TODO: rename these
        {
            float sigmoid1 = max1 / (1f + MathF.Exp(-rate1 * (input_x - offset_X1)));
            float sigmoid2 = max2 / (1f + MathF.Exp(-rate2 * (input_x - offset_X2)));

            float returnValue = MathF.Round(offset_Y0 + sigmoid1 + sigmoid2, 2, MidpointRounding.AwayFromZero);
            if (returnValue > max2 * 0.98f) { returnValue = max2; }
            return returnValue;
        }

        internal static float Calc_Curve_ExpoEaseOut(float x0, float rate, float max, float offset_X, float offset_Y)
        {
            float returnValue = offset_Y + max * (1 - MathF.Pow(2, (-rate * (x0 - offset_X))));
            if (returnValue > (max * 0.98f) + offset_Y) { return max + offset_Y; }
            return returnValue;
        }

        /// <summary>
        /// Sets a specific flag state through a mask
        /// </summary>
        /// <param name="valueToMask"></param>
        /// <param name="bitIndex"></param>
        /// <param name="bitOnOrOff"></param>
        /// <returns></returns>
        internal static int Calc_Flag_SetBit(int valueToMask, byte bitIndex, bool bitOnOrOff)
        {
            byte byteMask = (byte)(1 << bitIndex);
            if (bitOnOrOff)
            { valueToMask |= byteMask; }
            else { valueToMask &= (byte)~byteMask; }

            return valueToMask;
        }

        /// <summary>
        /// check a specific flag state
        /// </summary>
        /// <param name="valueToMask"></param>
        /// <param name="bitIndex"></param>
        /// <returns></returns>
        internal static bool Calc_Flag_CheckBit(int valueToMask, byte bitIndex)
        {
            return (valueToMask & (1 << bitIndex)) is not 0;
        }
    }
}
