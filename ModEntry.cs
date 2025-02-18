// Stardew Dialog Testing Mod fixed for v1.6
// Taken from https://github.com/AlanDavison/StardewValleyMods/

using System;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using HarmonyLib;
using System.Threading;

namespace DialogueTester
{
    public class ModEntry : Mod {

        //* Logging
        private static IMonitor monitor;
        private static bool bIgnore = false;

        //* Settings
        private ModConfig config;

        //* Entry-Point
        public override void Entry(IModHelper helper) {

            try{
                //* Load configuration
                this.config = this.Helper.ReadConfig<ModConfig>();
                i18n.gethelpers(this.Helper.Translation, this.config);
                Monitor.Log(i18n.GetTranslation("DialogueTester_LoadedConfig"), LogLevel.Info);
            }
            catch{
                //* Create configuration
                i18n.gethelpers(this.Helper.Translation, this.config);
                Monitor.Log(i18n.GetTranslation("DialogueTester_NoConfig"), LogLevel.Info);
                this.config = new ModConfig();
            }

            monitor = Monitor;

            //* Add our debug commands
            helper.ConsoleCommands.Add(i18n.GetTranslation("DialogueTester_commandName"), i18n.GetTranslation("DialogueTester_commandDescription") + i18n.GetTranslation("DialogueTester_commandUsage", new {_commandName = i18n.GetTranslation("DialogueTester_commandName")}) + i18n.GetTranslation("DialogueTester_commandUsageAdv", new {_commandName = i18n.GetTranslation("DialogueTester_commandName")}), this.TestDialogue);
            helper.ConsoleCommands.Add(i18n.GetTranslation("DialogueTester_commandHelp"), i18n.GetTranslation("DialogueTester_commandDescription"), this.PrintUsage);

            //* Setup Harmony
            if (config.EnableHarmony){
                PatchHarmony();
            }

            //* Setup Event Hooks
            helper.Events.GameLoop.GameLaunched += onLaunched;
        }
        
        //* Setup for GenericModConfigMenu support.
        private void onLaunched(object sender, GameLaunchedEventArgs e)
        {
            //* Hook into GMCM
            var api = this.Helper.ModRegistry.GetApi<GenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
            if (api != null){
                //* Register with GMCM
                api.Register(this.ModManifest, () => this.config = new ModConfig(), () => Helper.WriteConfig(this.config));
                    
                //* Our Options
                api.AddSectionTitle(this.ModManifest,() => i18n.GetTranslation("Config_HarmonySettingTitle"), () => i18n.GetTranslation("Config_HarmonyDescription"));
                api.AddBoolOption(this.ModManifest,() => this.config.EnableHarmony, (bool val) => this.config.EnableHarmony = val, ()  => i18n.GetTranslation("Config_EnableHarmony"), () => i18n.GetTranslation("Config_HarmonyDescription"),"EnableHarmony");
                api.AddBoolOption(this.ModManifest,() => this.config.IgnoreSelf, (bool val) => this.config.IgnoreSelf = val, ()  => i18n.GetTranslation("Config_IgnoreSelfSetting"), () => i18n.GetTranslation("Config_IgnoreSelfDescription"));

                //* Detect changes mid-game.
                api.OnFieldChanged(this.ModManifest,onFieldChanged);
                monitor.Log(i18n.GetTranslation("DialogueTester_GMCMSetup"), LogLevel.Info);
            }

        }

        //* The method invoked when we detect configuration changes.
        private void onFieldChanged(string str, object obj)
        {
            //* Harmony support
            if (str == "EnableHarmony"){
                if((bool)obj){
                    config.EnableHarmony = true;
                }else{
                    config.EnableHarmony = false;
                }
                if (config.EnableHarmony){
                    PatchHarmony();
                }else{
                    DisableHarmony();
                }
            }
            //* Ignore self
            else if(str == "IgnoreSelf"){
                if((bool)obj){
                    config.IgnoreSelf= true;
                }else{
                    config.IgnoreSelf = false;
                }
            }
        }

        private void PatchHarmony(){
            
            //* Setup Harmony
            monitor.Log(i18n.GetTranslation("DialogueTester_SetupHarmony"), LogLevel.Info);
            Harmony harmony = new Harmony(ModManifest.UniqueID);

            //* Patch DialogBox to find the translation keys used.
            harmony.Patch(
                original: AccessTools.Constructor(typeof(DialogueBox), new Type[] { typeof(Dialogue) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Dialogue_Postfix))
            );

        }

        private void DisableHarmony(){

            //* Remove Harmony Patching
            monitor.Log(i18n.GetTranslation("DialogueTester_ShutdownHarmony"), LogLevel.Info);
            Harmony harmony = new Harmony(ModManifest.UniqueID);
            harmony.UnpatchAll(harmony.Id);
        }
        //* Harmony Postfix to print out dialogue sources.
        public static void Dialogue_Postfix(DialogueBox __instance, ref Dialogue dialogue) {   

            try {

                if(bIgnore){
                    //* Skip own commands, re-enable for next dialogue.
                    bIgnore = false;
                    return;
                }
                //* Log NPC
                monitor.Log($"NPC: {dialogue.speaker.Name}", LogLevel.Warn);
                //* There isn't always a key.
                if (dialogue.TranslationKey is not null){
                    monitor.Log($"Key: {dialogue.TranslationKey}", LogLevel.Warn);
                }
                //* Log dialogue(s).
                for (int i = 0; i < dialogue.dialogues.Count; i++){
                    monitor.Log($"Dialogue[{i}]: {dialogue.dialogues[i].Text}", LogLevel.Warn);
                }
                
            }
            //* ERROR!
            catch (Exception e) {
                monitor.Log($"{i18n.GetTranslation("DialogueTester_PostfixError")} {nameof(Dialogue_Postfix)}:\n{e}", LogLevel.Error);
            }
        }

        private void PrintUsage(string command, string[] args) {
            //* Output to Debug Console
            monitor.Log(i18n.GetTranslation("DialogueTester_commandUsage", new {_commandName = i18n.GetTranslation("DialogueTester_commandName")}) + i18n.GetTranslation("DialogueTester_commandUsageAdv", new {_commandName = i18n.GetTranslation("DialogueTester_commandName")}) + i18n.GetTranslation("DialogueTester_commandExample", new {_commandName = i18n.GetTranslation("DialogueTester_commandName")}) + i18n.GetTranslation("DialogueTester_commandExampleAdv", new {_commandName = i18n.GetTranslation("DialogueTester_commandName")}), LogLevel.Info);
        }

        private void TestDialogue(string command, string[] args) {

            //* We only want to do this if we're actually in-game. May not be necessary.
            if (!Context.IsWorldReady){
                return;
            }

            string dialogueIdOrKey, npcName;
            bool manualDialogueKey = false;

            //* If we don't have the required amount of parameters, we quit.
            if (args.Length < 2 || args.Length > 3){
                monitor.Log(i18n.GetTranslation("DialogueTester_InvalidArguments"), LogLevel.Error);
                return;
            }

            //* If our third argument is the -manual option, we set a bool to tell us to use the passed dialogue key verbatim.
            if (args.Length == 3) manualDialogueKey = args[2].Equals("-manual");

            // Parameter 0 = Dialogue ID or full key
            // Parameter 1 = NPC name
            // Parameter 2 = Manual option
            dialogueIdOrKey = args[0];
            npcName = args[1];

            var speakingNpc = Utility.fuzzyCharacterSearch(npcName);
            string finalDialogue;

            if (speakingNpc == null){
                //* If the NPC doesn't exist, warn as appropriate and return.
                monitor.Log(i18n.GetTranslation("DialogueTester_InvalidNPC"), LogLevel.Error);
                return;
            }

            if (manualDialogueKey){
            //* Manual Key supplied
                try
                {
                    finalDialogue = Game1.content.LoadStringReturnNullIfNotFound(dialogueIdOrKey);
                }
                catch (Exception)
                {
                    finalDialogue = null;
                }
                if (finalDialogue == null){
                    //* FAIL NOT FOUND
                    monitor.Log(i18n.GetTranslation("DialogueTester_InvalidDialogue"), LogLevel.Error);
                    return;
                }
            }else{
            //* Lookup Dialogue from NPC:Key
                try
                {
                    finalDialogue = Game1.content.LoadStringReturnNullIfNotFound($"Characters\\Dialogue\\{npcName}:{dialogueIdOrKey}");
                }
                catch (Exception)
                {
                    finalDialogue = null;
                }
                if (finalDialogue == null){
                    //* FAIL NOT FOUND
                    monitor.Log(i18n.GetTranslation("DialogueTester_InvalidDialogue"), LogLevel.Error);
                    return;
                }
            }
            //* SUCCESS!
            if (config.IgnoreSelf){
                //* Tell Harmony Postfix to ignore us.
                bIgnore = true;
            }

            //* Display Dialogue now!
            Game1.DrawDialogue(new Dialogue(speakingNpc, dialogueIdOrKey, finalDialogue));
        }
    }
}
