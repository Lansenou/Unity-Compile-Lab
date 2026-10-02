// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

namespace Game.UI
{
    // Game.UI references Game.Chat, which references Game.Core: Game.Core's analyzer reaches it through the chain.
    public static class BadgeBar
    {
        public static int Width => Game.Chat.ChatLog.Lines + Example.Json.Generated.JsonContextMarker.Value
            + UclEditorGenerated.EditorGeneratorMarker.Value;
    }
}
