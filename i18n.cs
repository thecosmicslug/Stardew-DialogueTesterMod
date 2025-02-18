using System;
using StardewValley;
using StardewModdingAPI;


namespace DialogueTester
{
    internal static class i18n
    {
        private static ITranslationHelper translation;
        private static ModConfig config;
        
        public static void gethelpers(ITranslationHelper translation, ModConfig config)
        {
            i18n.translation = translation;
            i18n.config = config;
        }

        public static Translation GetTranslation(string key, object tokens = null)
        {
            if (i18n.translation == null)
            {
                throw new InvalidOperationException($"You must call {nameof(i18n)}.{nameof(i18n.gethelpers)} from the mod's entry method before reading translations.");
            }
                
            return i18n.translation.Get(key, tokens);
        }
    }
}
