using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace BleedingInDepth
{
    internal class BID_VarRef
    {
        internal const string ModName = "BleedingInDepth";
        internal const string ModName_Trunc = "BID";

        //globally used API callers
        internal static ICoreAPI API;
        internal static ICoreClientAPI API_Client;
        internal static ICoreServerAPI API_Server;

        internal static float CancelAndReturnValue = 1024.4201f; //Arbitrary number returned to caller function to tell it that bleed conversion failed and to fall back to vanilla damage handler
        internal static float normalizer_InternalBleed = 8f;//(Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_Internal / Config_Reference.Config_Loaded.Config_Rate.Rate_BleedDamage_External);//TODO: desire around 8?f from dynamic
        
        //variable names

    }
}
