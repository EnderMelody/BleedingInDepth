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
    public class BleedingInDepthModSystem : ModSystem
    {
        internal static ICoreAPI API; //provides globally used API callers
        internal static ICoreClientAPI clientAPI;
        internal static ICoreServerAPI serverAPI;
        private bool isConfigLoadSuccessful;



        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api); //these are unneeded but im leaving them here just in case
            API ??= api; //KEEP THIS FIRST

            BID_Config_Manager.Config_Conjure();
            Config_Reference.Config_Loaded ??= new Config_Reference(); //one last attempt to rebuild config from default
            isConfigLoadSuccessful = !string.IsNullOrWhiteSpace(Config_Reference.Config_Loaded?.ToString());
        }

        // Called on server and client; Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            api.RegisterEntityBehaviorClass("bleed", typeof(BID_Lib_EntityManager.EntityBehavior_Bleed));
            api.Event.OnEntityLoaded += BID_Lib_EntityManager.EntityBehavior_Bleed.Entity_AddBleedBehavior;
            api.Event.OnEntitySpawn += BID_Lib_EntityManager.EntityBehavior_Bleed.Entity_AddBleedBehavior;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            serverAPI ??= api; //KEEP THIS FIRST

            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            BID_Lib_CollectionManager.Dicitionary_Freeze();

            //serverside commands
            serverAPI.ChatCommands.GetOrCreate("MakeBleed")
                .WithDesc("Increases targets bleed level")
                //.RequiresPlayer()
                .RequiresPrivilege(Privilege.commandplayer)
                .WithArgs(new ICommandArgumentParser[] { serverAPI.ChatCommands.Parsers.OptionalFloat("bleed amount", 0.1f), serverAPI.ChatCommands.Parsers.OptionalBool("internal?", "internal"), serverAPI.ChatCommands.Parsers.OptionalBool("target looked entity?") })
                .HandleWith(BID_Lib_InputManager.Handle_Command.Command_MakeBleed);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            clientAPI ??= api; //KEEP THIS FIRST

            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            //clientside commands; optionals pass false by default //TODO: lang files
            serverAPI.ChatCommands.GetOrCreate("ReportBleed")
                .WithDesc("Reports targets bleed levels.")
                .WithAdditionalInformation("Reports the selected entity's bleed levels (true) or your own bleed levels (false). Enforces config values in config: bleed report.")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(new ICommandArgumentParser[] { serverAPI.ChatCommands.Parsers.OptionalBool("check looked at entity?") })
                .HandleWith(BID_Lib_InputManager.Handle_Command.Command_ReportBleed)
                .WithAlias(["BleedReport", "CheckBleed", "BleedCheck"]);

            //hotkeys
            clientAPI.Input.RegisterHotKey("BID:ReportBleed", "Check Bleed Levels (Crouch to check entity's)", GlKeys.T, HotkeyType.GUIOrOtherControls);
            clientAPI.Input.SetHotKeyHandler("BID:ReportBleed", (KeyCombination key) => { return BID_Lib_InputManager.Handle_Hotkey(key, "BID:ReportBleed"); });
        }

        public override void Dispose()
        {
            base.Dispose();

            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            BID_Lib_FunctionsGeneral.Log_Debug("Unloading Config", loggers: []);
            BID_Config_Manager.Config_Unload();
        }
    }
}
