namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// One labelled example for the MCP Power search: a sentence a player might actually say, and
/// which Power (or Powers, where more than one printed entry genuinely answers it) the search
/// ought to return, and how far down the list a caller could reasonably be expected to look.
///
/// <para><b>This set exists because PROGRESS.md item 4 says closing the tie-ordering gap needs
/// one before it can be judged.</b> "Weighting each word by how much of the rulebook uses it"
/// was tried once, fixed "walks through walls", and broke "reads minds" — two examples are not
/// evidence, and a half-tuned scorer is worse than a dull one because it is wrong in places
/// nobody has looked at rather than in the place they tested. This is the set that lets the next
/// attempt be judged on more than the two queries that happened to prompt it.</para>
///
/// <para><b>Written from the Powers, never from the scorer.</b> Every entry below was written by
/// opening <c>data/rules/powers.json</c>, reading a Power's printed <c>description</c>, and
/// writing the sentence a player would say about it — not by running <c>search_powers</c> and
/// recording whatever it happened to return. Running the search first and writing the
/// expectation to match would measure the scorer against itself. The two exceptions are the
/// pair PROGRESS.md item 4 names directly, "walks through walls" (should reach Phasing) and
/// "he shoots fire from his hands" (should reach Blast) — quoted from that entry, not
/// discovered by searching. Both failed when this file was first written, because neither word
/// is in the matching Power's own printed description; both are met now that Phasing and Blast
/// carry a data-driven vocabulary (a wider <c>tags</c> field) reaching past that description —
/// see PROGRESS.md item 4.</para>
///
/// <para><c>AcceptablePowerIds</c> holds more than one id only where the printed rulebook text
/// genuinely supports either answer — "unaffected by poison" is Resistance's own example
/// ("toxins — disease, drugs and poison") but Immunity's generic wording ("immune to specific
/// ... conditions, hazards") covers it too, and a sentence that vague should not be marked wrong
/// for landing on either.</para>
///
/// <para><b>Widened past the original 33, once that set was saturated.</b> All 33 above met
/// their bar, which meant the ratchet could only ever go down from there — it had stopped
/// discriminating between a search that works and a search that has quietly regressed. The set
/// below was written the same way and to the same rule (open <c>data/rules/powers.json</c>, read
/// a Power's own <c>description</c>, write the sentence — never run <c>search_powers</c> first),
/// biased toward the 74 Powers PROGRESS.md item 4 records as still carrying only their original
/// category tags rather than the 67 the vocabulary slice touched, toward sentences that describe
/// an effect landing on someone <em>else</em> rather than the caster, and toward a handful that
/// should find nothing at all — the baker's sentence is the model for those, not an oversight.
/// <see cref="Nothing"/> is the factory for that shape: <c>AcceptablePowerIds</c> is empty and
/// "met" means <c>search_powers</c>' own <c>found</c> count came back zero, never that some id
/// landed inside a window — see <c>PowerSearchEvaluationTests.Evaluate</c>.</para>
/// </summary>
public sealed record PowerSearchExpectation(
    string Query,
    IReadOnlyList<string> AcceptablePowerIds,
    int TopN,
    string Note,
    bool ExpectNothing = false)
{
    public PowerSearchExpectation(string query, string powerId, int topN, string note)
        : this(query, [powerId], topN, note) { }

    /// <summary>
    /// A sentence that should match no Power at all. <c>TopN</c> is unused for this shape — met
    /// is decided by <c>found == 0</c>, not by any id's position — so it is recorded as 0 rather
    /// than a number that would read as a real bar.
    /// </summary>
    public static PowerSearchExpectation Nothing(string query, string note) =>
        new(query, [], 0, note, ExpectNothing: true);
}

public static class PowerSearchExpectations
{
    public static readonly IReadOnlyList<PowerSearchExpectation> All =
    [
        new("walks through walls", "phasing", 3,
            "PROGRESS.md item 4's own reproduction. Phasing's own description says 'pass "
            + "through solid matter', never 'walls' — no reordering of that word could ever "
            + "reach it; it now carries 'walls'/'walk' as data-driven vocabulary instead."),

        new("he shoots fire from his hands", "blast", 3,
            "PROGRESS.md item 4's other reproduction. Blast's own description is 'a damaging "
            + "ranged attack. Name the type of damage it inflicts when you buy it' — that "
            + "naming is the whole Power, so 'fire'/'shoot'/'shooting' are vocabulary drawn "
            + "from Blast's own printed invitation, not from this sentence."),

        new("she can read anyone's mind from across the room", "telepathy", 3,
            "Telepathy: 'You can read minds and send thoughts within Distant Range.'"),

        new("his wounds knit themselves back together over time", "regeneration", 3,
            "Regeneration: 'You heal 1 point of damage per hour.'"),

        new("he can turn completely invisible", "invisibility", 3,
            "Invisibility: 'You can turn invisible.'"),

        new("she can fly through the sky", "flight", 3,
            "Flight: 'A Travel Power: you can fly.'"),

        new("he moves faster than anyone can follow", "super_speed", 5,
            "Super Speed: 'You move at superhuman speed.'"),

        new("she lays hands on someone and mends their wounds", "healing", 5,
            "Healing: 'You can heal anyone you touch.'"),

        new("he makes people obey his commands against their will", "mind_control", 5,
            "Mind Control: 'You can make any living being ... obey you for the duration.'"),

        new("she vanishes and reappears somewhere else instantly", "teleportation", 5,
            "Teleportation: 'You can travel instantly, through solid objects but not force "
            + "fields.' Not Vanish, which only moves you as far as a page and does not "
            + "reappear anywhere in particular."),

        new("he can shrink down to the size of an insect", "shrinking", 5,
            "Shrinking: 'You can shrink, dropping your weight rank by your current Shrinking "
            + "rank.'"),

        new("she can grow to giant size", "growth", 5,
            "Growth: 'You can grow enormous.'"),

        new("he throws a devastating punch that puts his opponent down", "strike", 8,
            "Strike: 'A damaging close combat attack — claws, energised fists, a signature "
            + "melee weapon.' A generic phrase like this could also mean Martial Arts, which "
            + "is why the bar is 8 rather than 3."),

        new("she is trained in unarmed combat and can disarm anyone", "martial_arts", 5,
            "Martial Arts: 'You are a master of unarmed combat.'"),

        new("he wears armor plating that shrugs off bullets", "armor", 5,
            "Armor: 'Armour or a personal force field that turns damage aside.'"),

        new("she projects a force field to protect her friends", "force_field", 5,
            "Force Field: 'A projected field that shields everyone inside it.'"),

        new("he senses danger before it happens, like a sixth sense", "danger_sense", 5,
            "Danger Sense: 'You feel immediate physical danger coming.'"),

        new("she can rewind a moment and undo what just happened", "precognition", 8,
            "Precognition: 'You can rewrite what has just happened ... actually winding time "
            + "back.' Phrased around the effect rather than the Power's own vocabulary, so a "
            + "wider bar than the plainer descriptions above."),

        new("he can make an exact copy of himself", "duplication", 5,
            "Duplication: 'Spend an action to create an exact duplicate of yourself.'"),

        new("she can command animals to do her bidding", "animal_control", 5,
            "Animal Control: 'You can make animals do what you want.'"),

        new("he conjures illusions that fool everyone's eyes", "illusions", 5,
            "Illusions: 'You can create illusions of any size ... with sight and sound.'"),

        new("she can move objects around with her mind", "telekinesis", 5,
            "Telekinesis: 'You can move objects out to Distant Range with your mind.'"),

        new("he can climb straight up a wall like a spider", "wall_crawling", 5,
            "Wall-Crawling: 'You can stick to walls and ceilings and move along them.'"),

        new("she can jump over a building in a single bound", "leaping", 5,
            "Leaping: 'A Travel Power: you jump enormous distances.'"),

        new("he can breathe underwater and swim like a fish", "swimming", 5,
            "Swimming: 'you move through water at speed, breathe it.'"),

        new("she can dig through solid rock and tunnel underground", "tunneling", 5,
            "Tunneling: 'A Travel Power: you travel by digging through the ground.'"),

        new("he's completely unaffected by poison", ["resistance", "immunity"], 8,
            "Resistance names 'toxins — disease, drugs and poison' outright; Immunity's wider "
            + "'immune to specific attacks, energies, conditions, hazards' also covers it. A "
            + "sentence this generic reasonably lands on either."),

        new("she's improbably lucky, the dice always seem to go her way", "luck", 5,
            "Luck: 'You are absurdly lucky ... Luck dice ... addable to any challenge roll.'"),

        new("he can find the exact weak spot on anything", "weakness_detection", 8,
            "Weakness Detection: 'You can find the weak point of any person or object you "
            + "observe.'"),

        new("she can shut down another power that's affecting someone", "dispel", 8,
            "Dispel: 'You can end any ongoing Power affecting a being or object.' Not Nullify, "
            + "which is a standing field rather than a one-time end to a single effect."),

        new("he can shut off everyone's powers in the area at once", "nullify", 8,
            "Nullify: 'A dampening field that shuts down the Abilities, Talents and Powers of "
            + "everyone in range, friend and foe alike.'"),

        new("she brings a stone statue to life to fight for her", "animation", 8,
            "Animation: 'You can bring images and objects to life ... They obey your mental "
            + "commands.'"),

        new("his body stretches and squeezes through tiny gaps", ["plasticity", "stretching"], 5,
            "Plasticity: 'Your body is rubbery and elastic, so you can mould yourself into "
            + "shapes and squeeze through the narrowest gaps' — the closer match, with "
            + "Stretching ('You can stretch or extend your body') accepted too since the "
            + "sentence names the same idea Stretching's own name states."),

        // ── Widened set: 39 more, biased toward the 74 Powers left with only their original
        // category tags, toward an effect landing on someone else, and toward a few that
        // should find nothing at all. See the class remark above. ─────────────────────────

        new("he can breathe underwater or shrug off a subzero blizzard without missing a step",
            "adaptation", 5,
            "Adaptation: 'Your body adapts instantly to hostile or alien surroundings, letting "
            + "you breathe, move, sense and survive there — growing gills and pressure "
            + "resistance underwater, for instance.'"),

        new("wild animals trust her on sight and won't attack unless she gives them a reason",
            "animal_empathy", 5,
            "Animal Empathy: 'You share an empathic understanding with animals. They will not "
            + "attack you without good reason, and even then you may be able to talk them "
            + "down.' An effect on the animals around her, not a power she does to herself."),

        new("her skin shifts color to match whatever's behind her, like a living chameleon",
            "blending", 5,
            "Blending: 'Your colouring shifts to match your surroundings, giving +3d on Covert "
            + "rolls to avoid being seen.'"),

        new("he can blind an opponent so they can barely fight back", "blind", 5,
            "Blind: 'You can blind a living target for the Power's duration ... A blinded "
            + "target takes -3d on attack and active defence rolls.' The effect lands on the "
            + "opponent, not on the caster — Blind is one of the powers this widened set was "
            + "written to reach that the original 33 under-covered."),

        new("she fights just as well blindfolded as she does with her eyes open",
            "blind_fighting", 5,
            "Blind Fighting: 'You do not rely on sight in a fight, so darkness or an unseen "
            + "opponent costs you nothing, whatever the cause.'"),

        new("he rallies everyone nearby and the whole team fights harder for it", "buff", 8,
            "Buff: 'Spend 1 Resolve to bolster yourself and every ally in the area with +1d on "
            + "all challenge rolls for the rest of the scene.' A wide bar because 'rallies' "
            + "and 'fights harder' are this project's words for the effect, not Buff's own."),

        new("she can make an entire room simply forget she was ever there", "cloud_minds", 5,
            "Cloud Minds: 'You cloud the minds of everyone within Distant Range, becoming "
            + "undetectable to them by any sense.' The genre phrase the task brief itself "
            + "names — 'makes people forget' — applied to the Power it actually describes."),

        new("he can shape solid force into a cage or a battering ram out of thin air",
            "constructs", 5,
            "Constructs: 'You can build and move shapes of solid force — cages, ramps, walls, "
            + "hammers, nets and the like.' Deliberately avoids 'wall', which is Phasing's own "
            + "vocabulary word and would tie the two together for no reason connected to this "
            + "sentence."),

        new("she can step sideways out of this reality and into another dimension entirely",
            "dimensional_travel", 8,
            "Dimensional Travel: 'You can travel to other dimensions, at the cost of an "
            + "action.' A wide bar since Star Gate and Time Travel share the word "
            + "'dimension(al)' and a generic phrasing like this could plausibly reach any of "
            + "the three; only Dimensional Travel is asserted because the sentence names no "
            + "world, no era and no portal — just 'another dimension'."),

        new("he can shape and hurl ice however he likes, freezing enemies solid",
            "elemental_control", 8,
            "Elemental Control: 'You create and control one element — fire, ice, gravity, "
            + "sound, plants, whatever you choose — and through it imitate other Powers at "
            + "this rank.' A wide bar because naming an element this way could plausibly land "
            + "on a Blast-shaped reading instead."),

        new("she can flood a room with sudden overwhelming fear", "emotion_control", 5,
            "Emotion Control: 'You can flood anyone in range with an overwhelming feeling: "
            + "rage, fear, love, confusion, despair.' The effect lands on everyone else in the "
            + "room, never on the caster."),

        new("he has extra arms that make his grip nearly impossible to break", "extra_limbs", 5,
            "Extra Limbs: 'Extra arms, tentacles, a prehensile tail or similar, worth +2d on "
            + "attack and defence rolls when grappling.'"),

        new("she can turn her whole body into a cloud of gas and slip under a door",
            "form_gaseous", 5,
            "Form — Gaseous: 'You can become a gas ... able to slip through any opening a gas "
            + "could.'"),

        new("his powered form is actually two different people sharing one body", "gestalt", 8,
            "Gestalt: 'Your powered form is two people merged, each alter ego a separate "
            + "character with their own Abilities, Talents and possibly Powers.' One of the "
            + "obscurest entries in the book — a genuinely hard case for a word search, kept "
            + "in rather than dropped for being hard, since an honest score is the point of "
            + "this widened set."),

        new("she drains the life out of someone she touches to heal her own wounds",
            "life_drain", 5,
            "Life Drain: 'You drain the life from a living being you touch ... If you are "
            + "wounded, you heal 1 point for every 2 you inflict.' The effect lands on the "
            + "victim; Healing (already in the original 33) is the opposite direction."),

        new("nobody can ever catch him off guard or get the drop on him",
            "lightning_reflexes", 5,
            "Lightning Reflexes: '... which under the Gritty Combat Rules makes it impossible "
            + "for anyone to get the drop on you.' Phrased around the printed consequence "
            + "rather than the flat Edge bonus, which no player would ever say out loud."),

        new("he can touch an enemy and steal their powers right out of them, knocking them cold",
            "power_absorption", 5,
            "Power Absorption: 'You can steal a character's Abilities, Talents and Powers of "
            + "one chosen Source by touch ... Success knocks them out.' An effect on the "
            + "victim, and the deliberate opposite of the next entry."),

        new("she can touch someone and copy their powers without taking anything away from them",
            "power_mimicry", 5,
            "Power Mimicry: 'You can copy a character's Abilities, Talents and Powers of one "
            + "chosen Source by touch ... the target loses nothing.' Paired with Power "
            + "Absorption above on purpose — the same touch, the opposite consequence, and "
            + "the sentence is written to tell them apart rather than to be generic."),

        new("no telepath can get past his defenses and into his head", "psi_screen", 5,
            "Psi-Screen: 'You also know when someone tries to read your surface thoughts, and "
            + "can use this Power to stop them probing your mind.'"),

        new("she can touch an old locket and see who owned it and how", "psychometry", 5,
            "Psychometry: 'By touching an inanimate object you read impressions from it and "
            + "may ask the GM its basic history.'"),

        new("he can deliver a killing blow with a single touch", "slay", 5,
            "Slay: 'You can wound a living target you touch ... inflicting 2 damage per net "
            + "success. Armor does not protect against it at all.'"),

        new("he can see clean through solid steel doors if he concentrates hard enough",
            "super_senses_x_ray_vision", 5,
            "Super Senses — X-Ray Vision: 'Concentrating, you can see through solid objects.'"),

        new("she can see perfectly even in absolute pitch darkness",
            "super_senses_night_vision", 3,
            "Super Senses — Night Vision: 'You can see in absolute darkness.' About as close a "
            + "paraphrase as this set allows, so the bar is tight."),

        new("he can always tell who's really behind a disguise or an illusion",
            "super_senses_true_sight", 5,
            "Super Senses — True Sight: 'Concentrating, you see a being's true form through "
            + "disguise, illusion and transformation of any kind.'"),

        new("she just knows the moment someone starts lying to her face",
            "super_senses_lie_detection", 3,
            "Super Senses — Lie Detection: 'You can tell when someone within Close Range is "
            + "lying.' The exact genre phrase the task brief names — 'he can tell when "
            + "someone's lying' — a player's own words rather than the printed one."),

        new("he can track someone across the whole city just by their scent",
            "super_senses_tracking_scent", 5,
            "Super Senses — Tracking Scent: 'You can identify, recognise and track people by "
            + "scent, and even read their mood.'"),

        new("she can send a demon back to whatever dimension it crawled out of", "banish", 5,
            "Banish: 'You can banish a target to another time, place or dimension.' The demon "
            + "is a genre flourish this rulebook never uses — the entry names none."),

        new("he shrugged off something that should have killed anybody else outright",
            "hard_to_kill", 5,
            "Hard to Kill: 'You are hard to kill — it would take something extreme, like "
            + "beheading or a volcano.'"),

        new("he can slip into someone else's body and pilot it as if it were his own",
            "possession", 5,
            "Possession: 'You can take over a living body within Distant Range ... You use the "
            + "host's physical Traits and your own mental ones.' A genre phrase — 'pilot a "
            + "body' — for an effect that lands entirely on somebody else, and the host's body "
            + "is not the caster's, which is the whole point of the Power."),

        new("he can freeze time completely so everyone around him stands still", "time_stop", 5,
            "Time Stop: 'You can freeze time in the area for a number of pages equal to your "
            + "rank ... Everything in the area is stuck; you can move about and move objects "
            + "and people around freely.'"),

        new("he disappears behind a burst of smoke and is simply gone before anyone can react",
            "vanish", 5,
            "Vanish: 'You vanish from sight in an instant and move as far as you can in one "
            + "page without anyone seeing where you went, often behind a flash or a puff of "
            + "smoke.' Left with only its original category tags, unlike Teleportation, which "
            + "the original 33 already distinguishes it from."),

        new("she can flash an entire room with a burst of light bright enough to stun everyone, "
            + "friend and foe alike",
            "dazzle", 5,
            "Dazzle: 'A blinding flash that overwhelms every living being in range, friend and "
            + "foe alike ... Targets are stunned for one page.' An effect on everyone else in "
            + "the room, deliberately worded around 'stun' and 'flash' rather than 'blind', "
            + "which is a different Power's own name."),

        new("his skin burns anyone who so much as touches him", "aura", 5,
            "Aura: 'Your body deals a chosen type of energy damage on contact — anyone "
            + "touching or striking you in unarmed combat takes an attack at your Aura rank.' "
            + "The effect lands on whoever is foolish enough to touch him, not on the caster."),

        new("she inspires her whole team to push past their limits in the middle of a fight",
            "leadership", 5,
            "Leadership: 'You lead your team in battle ... spend a free action to let each "
            + "ally reroll one challenge roll per combat scene.' 'Inspires' is Leadership's own "
            + "printed vocabulary word, which is why this one gets a tight bar despite Buff "
            + "covering similar ground above."),

        new("he can turn an enemy into a harmless frog", "polymorph", 3,
            "Polymorph: 'You can turn a living being into something harmless or helpless — a "
            + "frog, a statue.' The rulebook's own example, turned into a sentence a player "
            + "would actually say, and an effect that lands entirely on the target."),

        // ── Four that should find nothing at all — the baker's sentence is the model, not an
        // oversight. A search that returns something for every sentence handed to it is as
        // broken as one that returns nothing for a real Power. ──────────────────────────────

        PowerSearchExpectation.Nothing(
            "he always double-checks the numbers before submitting his quarterly expense "
            + "report",
            "Ordinary office diligence. Nothing in the rulebook prices being careful with a "
            + "spreadsheet."),

        PowerSearchExpectation.Nothing(
            "she takes her coffee with two sugars, first thing every morning",
            "A habit, not an effect. Deliberately avoids 'black' — Darkness's own tags carry "
            + "'blackout', close enough by the shared-prefix rule to have been a coincidental "
            + "match for the wrong reason."),

        PowerSearchExpectation.Nothing(
            "he can never quite manage to parallel park on the first attempt",
            "The negative of every 'he can' sentence above — ordinary human clumsiness, which "
            + "is exactly what a Power is not."),

        PowerSearchExpectation.Nothing(
            "she collects vintage postage stamps from countries that don't exist anymore",
            "A hobby. No Power in this rulebook is about philately.")
    ];
}
