namespace StoryForge.Scribe.Services.Ai;

/// <summary>
/// The no-key experience. Instead of pretending to write, it offers craft guidance that is
/// tailored to the beat — enough to keep the author moving until a model is connected.
/// </summary>
public static class OfflineMuse
{
    public static string Compose(AiRequest request)
    {
        var title = string.IsNullOrWhiteSpace(request.Subject) ? "this beat" : $"“{request.Subject}”";
        var pov = string.IsNullOrWhiteSpace(request.Pov) ? "your viewpoint character" : request.Pov;
        var brief = string.IsNullOrWhiteSpace(request.Brief) ? "no brief yet" : request.Brief.Trim();
        const string header =
            "Offline muse — craft guidance, not generated prose.\n" +
            "Connect a model in AI settings (gear icon) to draft in your voice.\n\n";

        return request.Action switch
        {
            AiAction.Continue => header + $"""
                Ways into the next paragraph of {title}:

                1. Start on a sensory fact {pov} cannot ignore. Not the room — the one detail in it that is wrong.
                2. Make someone arrive who wants something. Give them three lines, no greetings.
                3. Move an object. Let the object's history do the exposition.
                4. Cut the last sentence you wrote and see if the paragraph still stands. Often it ends sooner.

                Questions to write toward:
                • What does {pov} want in this beat, and what makes it impossible right now?
                • What will {pov} refuse to admit by the end of it?

                Brief on the table: {brief}
                """,
            AiAction.DraftBeat => header + $"""
                A four-move scaffold for {title}:

                Brief: {brief}

                1. Want — {pov} enters mid-action, already trying to get something.
                2. Obstacle — the specific thing in the way: a person, a rule, a lie, the weather.
                3. Turn — the situation changes because of a choice, not an accident.
                4. Cost — something is lost or risked; end before the reaction settles.

                Open with a concrete noun and a verb. Avoid waking up, weather reports, and mirrors.
                Write one paragraph per move, then delete the scaffolding.
                """,
            AiAction.BrainstormBeats => header + $"""
                Six beats to test against the scene. Keep what bites; throw the rest away.

                - Complication — Someone with leverage over {pov} arrives and makes the current plan cost more than expected.
                - Quiet beat — A small domestic action carries the tension; the conversation stays polite and entirely about something else.
                - Reveal — A detail already mentioned returns with a different meaning, forcing {pov} to reinterpret the scene.
                - Reversal — {pov} gets what they wanted and discovers it is the wrong thing.
                - Decision — {pov} chooses between two loyalties, and the choice is visible in an action rather than a speech.
                - Consequence — The brief's promise is kept, but at a price someone else has to pay.

                Scene job: {brief}
                """,
            AiAction.TightenProse => header + """
                Line-edit checklist:

                • Delete filter words: felt, saw, heard, realized, seemed, began to, could see.
                • Replace adverb + verb with one precise verb.
                • Cut throat-clearing: the first sentence of a paragraph is often a runway.
                • Name the abstraction. "She felt afraid" → the specific thing she does with her hands.
                • Break the longest sentence in two. Combine the two shortest.
                • Read the last line aloud. If it explains the scene, cut it.

                Do one pass for each bullet. Then read it once more for voice.
                """,
            AiAction.DescribeSetting => header + $"""
                Build the setting from a sensory inventory, not adjectives:

                • Sight — one large shape, one small anomaly.
                • Sound — what {pov} hears that no one else notices.
                • Touch or temperature — the body's first contact with the place.
                • Smell — one specific smell with a memory attached.
                • A detail that will matter later. Plant it now without pointing at it.

                Keep the camera in {pov}'s body. Write three sentences from the inventory; let the fourth contradict them.
                """,
            AiAction.CritiqueScene => header + $"""
                Read the beat once for story, once for sound. Notes to make yourself:

                • {pov} wants something in this beat — is it visible on the page?
                • What changes between the first and last line?
                • Where does the prose explain what the scene already showed?
                • Which sentence would you underline as "voice"? Protect it.
                • Is the tension doing work in dialogue, action, or both?
                • What is the most specific physical detail, and can it recur with more weight?

                Answer two of these in the margin; revise only those.
                """,
            _ => header + $"""
                The model isn't connected, so here is a way to keep moving with your own question:

                "{request.User.Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "What happens next?"}"

                • Answer it in one sentence, in {pov}'s voice.
                • List three concrete images that belong to the answer.
                • Write the scene as if the answer is already true.
                • Cut the first paragraph and see what survives.

                Brief on the table: {brief}
                """
        };
    }
}
