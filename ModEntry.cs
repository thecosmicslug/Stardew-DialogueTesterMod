// Stardew Dialog Testing Mod fixed for v1.6
// Taken from https://github.com/AlanDavison/StardewValleyMods/

using System;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;
using HarmonyLib;

namespace DialogueTester
{
    public class ModEntry : Mod {

        //* Strings
        //TODO: Add translation support.
        private static readonly string _commandName = "dialogue_tester";
        private static readonly string _commandDescription = "Test some dialogue while modding, show which translation key is used ingame.";
        private static readonly string _InvalidDialogue = "Invalid dialogue ID. Maybe this character doesn't have this dialogue?";
        private static readonly string _InvalidArguments = "Invalid number of arguments.";
        private static readonly string _InvalidNPC = "Invalid NPC name.";

        private static readonly string _commandUsage = $"\nUsage: \t\t\t{_commandName} <Dialogue ID> <NPC name>\n" +
        $"Advanced usage: \t{_commandName} <Full dialogue path> <NPC name> -manual\n" +
        $"Regular example: \t{_commandName} summer_Wed2 Abigail\n" +
        $"Advanced example: \t{_commandName} Characters\\Dialogue\\Abigail:summer_Wed2 Abigail -manual\n";

        //* Logging
        private static IMonitor monitor;
        private static bool bIgnore = false;

        //* Settings
        private ModConfig config;

        public override void Entry(IModHelper helper) {

            //* Load configuration
            try{
                this.config = this.Helper.ReadConfig<ModConfig>();
            }
            catch{
                this.config = new ModConfig();
            }

            //* Add our debug command
            monitor = Monitor;
            helper.ConsoleCommands.Add(_commandName, $"{_commandDescription}\n\n{_commandUsage}", this.TestDialogue);

            //TODO: Add GMCM Support?

            //TODO: Move Harmony Patching so we can enable/disable dynamically
            if (config.EnableHarmony){
                //* Setup Harmony
                monitor.Log("Setting up harmony postfix...", LogLevel.Info);
                Harmony harmony = new(ModManifest.UniqueID);
                
                //* Patch DialogBox to find the translation keys used.
                harmony.Patch(
                    original: AccessTools.Constructor(typeof(DialogueBox), new Type[] { typeof(Dialogue) }),
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(Dialogue_Postfix))
                );
            }


            monitor.Log("Setup is complete.", LogLevel.Info);

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

        private void PrintUsage(string error) {

            //* Output to Debug Console
            monitor.Log(error, LogLevel.Error);
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
                this.PrintUsage(_InvalidArguments);
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
                this.PrintUsage(_InvalidNPC);
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
                    this.PrintUsage(_InvalidDialogue);
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
                    this.PrintUsage(_InvalidDialogue);
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
