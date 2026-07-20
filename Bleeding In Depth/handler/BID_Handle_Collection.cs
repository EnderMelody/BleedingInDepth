using BleedingInDepth.config;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace BleedingInDepth.handler
{
    internal class BID_Handle_Collection
    {
        private static ICoreAPI API = BID_VarRef.API;

        internal static void Dicitionary_Freeze()
        {
            BID_Handle_Bleed.DamageType_Dict_ConfigCache = Config_Reference.Config_Loaded.Config_TypeModifier.TypeMod_Damage_Acc.Dict_DamageType.ToFrozenDictionary();
        }
    }
}
