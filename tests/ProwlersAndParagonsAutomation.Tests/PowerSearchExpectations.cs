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
/// </summary>
public sealed record PowerSearchExpectation(
    string Query,
    IReadOnlyList<string> AcceptablePowerIds,
    int TopN,
    string Note)
{
    public PowerSearchExpectation(string query, string powerId, int topN, string note)
        : this(query, [powerId], topN, note) { }
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
            + "sentence names the same idea Stretching's own name states.")
    ];
}
