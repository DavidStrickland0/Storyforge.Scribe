using StoryForge.Scribe.Domain;

namespace StoryForge.Scribe.Services;

/// <summary>
/// First-run content. The sample gives new users a manuscript with real structure and prose
/// so every part of the workspace has something to demonstrate.
/// </summary>
public static class SeedBook
{
    public static Book CreateSample() => new()
    {
        Title = "The Cartographer's Daughter",
        Author = "A. Writer",
        WordGoal = 80_000,
        Beats =
        [
            new Beat
            {
                Title = "The bell under the pier",
                Brief = "At the lowest tide, a bell rings beneath the pilings. No one else seems to hear it.",
                Pov = "Mara"
            },
            new Beat
            {
                Title = "Ossic's ledger",
                Brief = "Mara breaks into the harbormaster's office and finds years of payments to a surveyor who officially drowned.",
                Pov = "Mara"
            }
        ],
        Scenes =
        [
            new Scene
            {
                Title = "The Salt Road",
                Pov = "Mara",
                Setting = "A road of packed salt through black pines, the old tide line painted in white bands on the trunks.",
                Summary = "A scene waiting for a chapter: Mara follows the compass away from the harbor.",
                Beats =
                [
                    new Beat
                    {
                        Title = "A door under the waterline",
                        Brief = "At the painted tide line, Mara finds a door standing alone among the pines, half-buried, still locked. The compass swings toward it — and someone answers her knock from the other side.",
                        Pov = "Mara"
                    }
                ]
            }
        ],
        Chapters =
        [
            new Chapter
            {
                Title = "Low Water",
                Summary = "An unnaturally low tide exposes the harbor floor — and a secret the town buried with it.",
                Scenes =
                [
                    new Scene
                    {
                        Title = "The Flats at Dawn",
                        Pov = "Mara",
                        Setting = "Vell Harbor at low tide: fog, gray mudflats, the ribs of a wrecked boat, and the steeple of a drowned church.",
                        Summary = "Mara finds a body on the exposed harbor floor and a compass that points the wrong way.",
                        Beats =
                        [
                            new Beat
                            {
                                Title = "A body on the flats",
                                Brief = "Mara goes out at an unnaturally low tide and finds a drowned man in the mud — with her father's compass in his hand.",
                                Status = BeatStatus.Draft,
                                Pov = "Mara",
                                Content = """
                                    The tide went out farther than it should have.

                                    Mara had lived her whole life above Vell Harbor, and she knew the water's habits the way she knew her own pulse. It should have stopped at the third marker, the rusted iron post her father had set when she was six. Instead it kept going, peeling away from the pilings in long gray sheets, until the harbor floor lay open beneath the fog like a held breath.

                                    Someone was walking out there.

                                    She stood at the breakwater with her boots filling with cold and watched the shape move between the ribs of a wrecked fishing boat. Not walking, she decided. Dragging. A coat, maybe. A bundle of nets. The fog kept taking the edges off the world, and she had learned young that the sea gave back only what it wanted you to see.

                                    The shape stopped. A gull screamed somewhere over the flats, and the sound went flat and strange, as if the air had forgotten how to carry it.

                                    Mara climbed down the wet stones. Halfway out she found the first boot, then the second, then the man they belonged to, face down in the mud with one arm stretched toward the drowned church. Its steeple still broke the surface at the lowest tides, a black finger testing the air.

                                    She should have run for the harbormaster. She knelt instead.

                                    There was a compass in his hand. Her father's compass, brass worn smooth on one side, the little hinge bent the way it had been bent since the winter of her twelfth year. The needle swung once, twice, and settled.

                                    It was not pointing north.

                                    It was pointing inland.
                                    """
                            },
                            new Beat
                            {
                                Title = "The compass points inland",
                                Brief = "The needle refuses north. Mara recognizes the bent hinge and remembers the winter her father vanished. A gull drops something onto the flats: a brass key stamped with the seal of the drowned town.",
                                Pov = "Mara"
                            }
                        ]
                    },
                    new Scene
                    {
                        Title = "The Harbormaster's Office",
                        Pov = "Mara",
                        Setting = "A salt-stained room above the fish market: caged lamps, curling tide charts, the smell of tar and old paper.",
                        Summary = "Mara tries to report the body. The harbormaster knows more than he is willing to say.",
                        Beats =
                        [
                            new Beat
                            {
                                Title = "Tea and lies",
                                Brief = "Ossic pours tea, offers condolences, then calls the dead man by name before anyone has identified him. Mara palms the brass key and says nothing.",
                                Pov = "Mara",
                                Status = BeatStatus.Draft,
                                Content = """
                                    The harbormaster's office smelled of tar and old paper and, underneath both, of the sea pretending to be a house.

                                    Ossic poured the tea without asking. His hands were the hands of a man who had hauled rope most of his life, and the cup looked small and silly in them. Steam climbed between them and did not help.

                                    "A bad business," he said. "Terrible, at that hour. You did right to come to me first."

                                    "I didn't touch him."

                                    "No. No, of course not." He sat down behind the tide charts, and the chair complained. "The sea takes a man now and then. It's not a thing to carry home with you."

                                    Mara wrapped her fingers around the cup. In her coat pocket, the brass key was a small cold weight. She had meant to put it on the table between them. She could not say now why she hadn't.

                                    Ossic sighed and looked past her at the fog in the window. "Poor Ilan," he said. "He never could keep to the shallows."

                                    The name went through her like a hook.

                                    No one had told him the dead man's name. No one in Vell had even seen the body yet — she had come straight up the stairs with her boots still shedding mud.

                                    "You knew him," she said.

                                    Ossic looked at her then, and something behind his face closed like a door.

                                    "Everyone knew Ilan," he said. "Drink your tea."
                                    """
                            }
                        ]
                    }
                ]
            },
            new Chapter
            {
                Title = "The Drowned Road",
                Summary = "Mara follows the compass away from the sea, toward a town that was erased from every map.",
                Scenes =
                [
                    new Scene
                    {
                        Title = "Inland",
                        Pov = "Mara",
                        Setting = "A road of packed salt through black pines. The old tide line is painted in white bands on the trunks.",
                        Summary = "The compass leads Mara to a door standing alone in the woods.",
                        Beats =
                        [
                            new Beat
                            {
                                Title = "A door under the waterline",
                                Brief = "At the painted tide line, Mara finds a door standing alone among the pines, half-buried, still locked. The compass swings toward it — and someone answers her knock from the other side.",
                                Pov = "Mara"
                            }
                        ]
                    },
                    new Scene
                    {
                        Title = "The Surveyor's House",
                        Pov = "Mara",
                        Setting = "A house built on stilts above a dry canal, its rooms full of maps with whole towns cut out of them.",
                        Summary = "Mara learns who her father was really working for.",
                        Beats =
                        [
                            new Beat
                            {
                                Title = "What the water kept",
                                Brief = "The surveyor's widow shows Mara a map with a hole cut where Vell's sister town used to be. The missing piece is in her father's handwriting.",
                                Pov = "Mara"
                            }
                        ]
                    }
                ]
            }
        ]
    };

    public static Book CreateStarter()
    {
        return new Book
        {
            Title = "Untitled Book",
            Author = "",
            WordGoal = 80_000,
            Beats = [new Beat { Status = BeatStatus.Outline }]
        };
    }

    public static Guid FirstBeatId(Book book) =>
        Outline.Locations(book).Select(l => l.Beat.Id).FirstOrDefault();
}
