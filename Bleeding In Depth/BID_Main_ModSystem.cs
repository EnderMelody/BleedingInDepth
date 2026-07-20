using BleedingInDepth.config;
using BleedingInDepth.lib;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BleedingInDepth
{
    public class BID_Main_ModSystem : ModSystem
    {
        private bool isConfigLoadSuccessful;



        public override void StartPre(ICoreAPI api)
        {
            BID_VarRef.API ??= api; //KEEP THIS FIRST

            BID_Config_Manager.Config_Conjure();
            Config_Reference.Config_Loaded ??= new Config_Reference(); //one last attempt to rebuild config from default
            isConfigLoadSuccessful = !string.IsNullOrWhiteSpace(Config_Reference.Config_Loaded?.ToString());
        }

        // Called on server and client; Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            api.RegisterEntityBehaviorClass("bleed", typeof(BID_Manager_Entity.EntityBehavior_Bleed));
            api.Event.OnEntityLoaded += BID_Manager_Entity.EntityBehavior_Bleed.Entity_AddBleedBehavior;
            api.Event.OnEntitySpawn += BID_Manager_Entity.EntityBehavior_Bleed.Entity_AddBleedBehavior;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            BID_VarRef.ServerAPI ??= api; //KEEP THIS FIRST

            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            BID_Manager_Collection.Dicitionary_Freeze();

            //serverside commands
            BID_VarRef.ServerAPI.ChatCommands.GetOrCreate("MakeBleed")
                .WithDesc("Increases targets bleed level")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.commandplayer)
                .HandleWith(BID_Manager_Input.Handle_Command.Command_MakeBleed)
                .WithArgs(new ICommandArgumentParser[] { BID_VarRef.ServerAPI.ChatCommands.Parsers.OptionalFloat("bleed amount", 0.1f), BID_VarRef.ServerAPI.ChatCommands.Parsers.OptionalBool("internal?", "internal"), BID_VarRef.ServerAPI.ChatCommands.Parsers.OptionalBool("target looked entity?", "entity") });
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            BID_VarRef.ClientAPI ??= api; //KEEP THIS FIRST

            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            //clientside commands; optionals pass false by default //TODO: lang files
            BID_VarRef.ClientAPI.ChatCommands.GetOrCreate("ReportBleed")
                .WithDesc("Reports targets bleed levels.")
                .WithAdditionalInformation("Reports the selected entity's bleed levels (true) or your own bleed levels (false). Enforces config values in config: bleed report.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(BID_Manager_Input.Handle_Command.Command_ReportBleed)
                .WithArgs(new ICommandArgumentParser[] { BID_VarRef.ClientAPI.ChatCommands.Parsers.OptionalBool("check looked at entity?") })
                .WithAlias(["BleedReport", "CheckBleed", "BleedCheck"]);

            //hotkeys
            BID_VarRef.ClientAPI.Input.RegisterHotKey("BID:ReportBleed", "Check Bleed Levels (Crouch to check entity's)", GlKeys.T, HotkeyType.GUIOrOtherControls);
            BID_VarRef.ClientAPI.Input.SetHotKeyHandler("BID:ReportBleed", (KeyCombination key) => { return BID_Manager_Input.Handle_Hotkey(key, "BID:ReportBleed"); });
        }

        public override void Dispose()
        {
            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            BID_Config_Manager.Config_Unload();
        }
    }
}
