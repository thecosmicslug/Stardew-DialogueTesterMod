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

        //* Strings
        //TODO: Add translation support.
        //use like = i18n.GetTranslation("Config.CarolineSprite");
        private static readonly string _commandName = "dialogue_tester";
        private static readonly string _commandHelp = "dialogue_tester_help";
        private static readonly string _commandDescription = "Test some dialogue while modding, show which translation key is used ingame.";
        private static readonly string _InvalidDialogue = "Invalid dialogue ID. Maybe this character doesn't have this dialogue?";
        private static readonly string _InvalidArguments = "Invalid number of arguments.";
        private static readonly string _InvalidNPC = "Invalid NPC name.";

        private static readonly string _commandUsage = $"\nUsage: \t\t\t{_commandName} <Dialogue ID> <NPC name>\n" +
        $"Advanced usage: \t{_commandName} <Full dialogue path> <NPC name> -manual\n\n" +
        $"Regular example: \t{_commandName} summer_Wed2 Abigail\n" +
        $"Advanced example: \t{_commandName} Characters\\Dialogue\\Abigail:summer_Wed2 Abigail -manual\n";

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
                Monitor.Log("Configuration loaded.", LogLevel.Info);
            }
            catch{
                //* Create configuration
                Monitor.Log("No config found, using defaults.", LogLevel.Info);
                this.config = new ModConfig();
            }
            
            //* Setup Logging, Translation Support.
            monitor = Monitor;
            i18n.gethelpers(this.Helper.Translation, this.config);

            //* Add our debug command
            helper.ConsoleCommands.Add(_commandName, $"{_commandDescription}\n\n{_commandUsage}", this.TestDialogue);
            helper.ConsoleCommands.Add(_commandHelp, "Show Help", this.PrintUsage);

            //* Setup Harmony
            if (config.EnableHarmony){
                PatchHarmony();
            }

            //* Setup Event Hooks
            helper.Events.GameLoop.GameLaunched += onLaunched;
            Monitor.Log("Setup is complete.", LogLevel.Info);
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
                api.AddSectionTitle(this.ModManifest,() => "Harmony Settings", () => "DialogueTester can display the keys used when NPCs talk in-game if we use a Harmony postfix." );
                api.AddBoolOption(this.ModManifest,() => this.config.EnableHarmony, (bool val) => this.config.EnableHarmony = val, ()  => "Enable Harmony support.", () => "Will log dialogue keys to the console when used.","EnableHarmony");
                api.AddBoolOption(this.ModManifest,() => this.config.IgnoreSelf, (bool val) => this.config.IgnoreSelf = val, ()  => "Ignore Dialogue Requested.", () => "Don't bother logging dialogue we have asked for.","IgnoreSelf");

                //* Detect changes mid-game.
                api.OnFieldChanged(this.ModManifest,onFieldChanged);
                monitor.Log("GenericModConfigMenu setup complete.", LogLevel.Info);
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

        //* Enable Harmony Patching
        private void PatchHarmony(){
            
            //* Setup Harmony
            monitor.Log("Setting up harmony postfix...", LogLevel.Info);
            Harmony harmony = new Harmony(ModManifest.UniqueID);

            //* Patch DialogBox to find the translation keys used.
            harmony.Patch(
                original: AccessTools.Constructor(typeof(DialogueBox), new Type[] { typeof(Dialogue) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Dialogue_Postfix))
            );

        }

        //* Remove Harmony Patching
        private void DisableHarmony(){

            monitor.Log("removing harmony postfix...", LogLevel.Info);
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
                //* Log dialogue.
                //TODO: Loop all dialogues?
                monitor.Log($"Dialogue: {dialogue.dialogues[dialogue.currentDialogueIndex].Text}", LogLevel.Warn);
            }
            //* ERROR!
            catch (Exception e) {
                monitor.Log($"Failed in {nameof(Dialogue_Postfix)}:\n{e}", LogLevel.Error);
            }
        }

        private void PrintUsage(string command, string[] args) {
            //* Output to Debug Console
            monitor.Log(_commandUsage, LogLevel.Info);
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
                monitor.Log(_InvalidArguments, LogLevel.Error);
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
                monitor.Log(_InvalidNPC, LogLevel.Error);
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
                    monitor.Log(_InvalidDialogue, LogLevel.Error);
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
                    monitor.Log(_InvalidDialogue, LogLevel.Error);
                    return;
                }
            }
            //* SUCCESS!
            if (config.IgnoreSelf){
                //* Tell Harmony Postfix to ignore us.
                bIgnore = true;
            }

            //* Display Dialogue now!
            Game1.DrawDialogue(new Dialogue(speakingNpc, null, finalDialogue));
        }
    }
}
