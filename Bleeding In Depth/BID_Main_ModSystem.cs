using System;
using BleedingInDepth.config;
using BleedingInDepth.handler;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace BleedingInDepth
{
    public class BID_Main_ModSystem : ModSystem
    {
        private bool isConfigLoadSuccessful;



        public override void StartPre(ICoreAPI api)
        {
            BID_VarRef.API ??= api; //KEEP THIS FIRST

            try { BID_Config_Manager.Config_Conjure(); }
            catch (Exception ex) { api.Logger.Error("[{0}]: (Config_Conjure) Exception caught: {1}", [BID_VarRef.ModName, ex.Message]); Config_Reference.Config_Loaded = new Config_Reference(); }
            
            isConfigLoadSuccessful = !string.IsNullOrWhiteSpace(Config_Reference.Config_Loaded?.ToString());
        }

        public override void Start(ICoreAPI api)
        {
            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            api.RegisterEntityBehaviorClass("bleed", typeof(BID_Handle_Entity.EntityBehavior_Bleed));
            api.Event.OnEntityLoaded += BID_Handle_Entity.EntityBehavior_Bleed.Entity_AddBehavior_Bleed;
            api.Event.OnEntitySpawn += BID_Handle_Entity.EntityBehavior_Bleed.Entity_AddBehavior_Bleed;
            BID_Handle_Entity.BleedHandle_SubscribeEvent();
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            BID_VarRef.API_Server ??= sapi; //KEEP THIS FIRST
            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            BID_Handle_Collection.Dicitionary_Freeze();

            //serverside commands
            sapi.ChatCommands.GetOrCreate($"{BID_VarRef.ModName_Trunc}")
                .RequiresPrivilege(Privilege.chat)

                .BeginSubCommand("MakeBleed")
                    .WithDesc("Increases targets bleed level")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.commandplayer)
                    .HandleWith(BID_Handle_Input.Handle_Command.Command_MakeBleed)
                    .WithArgs(new ICommandArgumentParser[] { BID_VarRef.API_Server.ChatCommands.Parsers.OptionalFloat("bleed amount", 0.1f), BID_VarRef.API_Server.ChatCommands.Parsers.OptionalBool("internal?", "internal"), BID_VarRef.API_Server.ChatCommands.Parsers.OptionalBool("target looked entity?", "entity") })
                    .EndSubCommand()

                .BeginSubCommand("ReloadConfig")
                    .WithDesc("Reloads the config for the current server.")
                    //.WithAdditionalInformation("")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(BID_Handle_Input.Handle_Command.Command_ReloadConfig)
                    .WithAlias([])
                    .EndSubCommand();
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            BID_VarRef.API_Client ??= capi; //KEEP THIS FIRST
            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            //clientside commands; optionals pass false by default //TODO: lang files //TODO: move into BID_Handle_Input
            capi.ChatCommands.GetOrCreate("BID")
                .RequiresPrivilege(Privilege.chat)

            //capi.ChatCommands.GetOrCreate("ReportBleed")
                .BeginSubCommand("ReportBleed")
                    .WithDesc("Reports targets bleed levels.")
                    .WithAdditionalInformation("Reports the selected entity's bleed levels (true) or your own bleed levels (false). Enforces config values in config: bleed report.")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(BID_Handle_Input.Handle_Command.Command_ReportBleed)
                    .WithArgs(new ICommandArgumentParser[] { BID_VarRef.API_Client.ChatCommands.Parsers.OptionalBool("check looked at entity?") })
                    .WithAlias(["BleedReport", "CheckBleed", "BleedCheck"])
                    .EndSubCommand()

            //capi.ChatCommands.GetOrCreate("ReloadConfig")
                .BeginSubCommand("ReloadConfig")
                    .WithDesc("Reloads the config for the current client.")
                    //.WithAdditionalInformation("")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.chat)
                    .HandleWith(BID_Handle_Input.Handle_Command.Command_ReloadConfig)
                    //.WithArgs(new ICommandArgumentParser[] { BID_VarRef.ClientAPI.ChatCommands.Parsers.OptionalBool("also reload server?") })//TODO: try to combine these if possible without netcode
                    .WithAlias([])
                    .EndSubCommand();


            //hotkeys
            BID_VarRef.API_Client.Input.RegisterHotKey("BID:ReportBleed", "Check Bleed Levels (Crouch to check entity's)", GlKeys.T, HotkeyType.GUIOrOtherControls);
            BID_VarRef.API_Client.Input.SetHotKeyHandler("BID:ReportBleed", (KeyCombination key) => { return BID_Handle_Input.Handle_Hotkey(key, "BID:ReportBleed"); });
        }

        public override void Dispose()
        {
            if (!isConfigLoadSuccessful) { return; } //if config fails to load do not run any mod code

            BID_Config_Manager.Config_Unload();
        }
    }
}
