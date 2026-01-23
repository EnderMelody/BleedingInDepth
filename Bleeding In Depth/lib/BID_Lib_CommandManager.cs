using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using static BleedingInDepth.lib.BID_Lib_EntityManager;

namespace BleedingInDepth.lib
{
    internal class BID_Lib_CommandManager
    {
        private static ICoreAPI API = BleedingInDepthModSystem.API;

        internal static TextCommandResult MakeBleed(TextCommandCallingArgs args)//TODO: fix up command; allow to apply to looked at entity; TODO: switch to lang
        {
            if (args.Caller.Entity is not Entity entity) { return TextCommandResult.Error("Entity was missing or null"); }
            if (entity.GetBehavior<EntityBehavior_Bleed>() is not EntityBehavior_Bleed entity_BleedBehavior) { return TextCommandResult.Error("entity was missing BehaviorBleed:"); }

            if (args[1] is not true)
            { entity_BleedBehavior.Bleed_CurrentLevel_Internal += (float)args[0]; }
            else { entity_BleedBehavior.Bleed_CurrentLevel_External += (float)args[0]; }
            
            return TextCommandResult.Success($"{entity.GetPrefixAndCreatureName()} gained {args[0]} {((bool)args[1] ? "internal" : "external")} bleed");
        }
    }
}
