# The account of finished work

One file per slice, named `YYYY-MM-DD-a-short-slug.md`. Newest is whichever date sorts last.

**Open work is not here — it is in [`../../PROGRESS.md`](../../PROGRESS.md)**, which is still the
file to read before starting anything.

## Why this is a directory and not a section

It was a section, at the head of `PROGRESS.md`, and every slice appended its account to the same
anchor in the same file. **That made two concurrent branches conflict on bytes neither had anything
to do with.** The roster slice rebased five times in one afternoon and every single conflict was
that heading — twice on the entry, three times on the `| Tests |` row above it. Nothing about
either was a disagreement; both sides were right and both were new.

A file per slice cannot collide: two branches finished on the same day write two filenames, and git
merges them without an opinion. The one case that still conflicts is two slices choosing the *same*
slug on the same day, which is a real collision and reads as one.

The second reason is size. The archive was **85% of `PROGRESS.md`** — 6,700 lines of finished work
in front of the 1,300 that say what is left. `CLAUDE.md` sends every reader there first, and what
they were sent for was the smaller half.

## What one file holds

Whatever the slice deserves. These are arguments, not changelog lines: the reasoning is the
expensive part to recover, and a commit message is the wrong place to keep it. The rules that held
for entries in the old section still hold here.

- **Say what changed and why.** A finding that turned out to be wrong is worth as much as one that
  held — several entries here are mostly a correction of their own first paragraph.
- **Figures are measured, not carried.** If a number is in the file, say what produced it and when.
- **Link the pull request.**
- **Do not edit an entry once its slice is merged.** A later slice that contradicts it writes its
  own file saying so. Rewriting an archive to match today's code is what makes an archive
  worthless — the same reason `docs/notes/s11-undo.md` still describes a method that has been
  renamed.

## What the tooling knows about this directory

- **`ProgressArchiveTests`** requires every file here to open with an `# ` title, requires this
  README and the pre-split archive to exist, and requires `PROGRESS.md` to keep no `###` entries
  under its own **Completed work** heading — which is what stops the section growing back.
- **`build.yml` does *not* ignore this directory, and that was decided rather than overlooked.**
  It was nearly added to `paths-ignore` beside `PROGRESS.md` on the reasoning that finished work is
  inert — but it is not inert while `ProgressArchiveTests` reads it, and skipping it would hide the
  one change that test exists to catch. `WorkflowFilterTests.ADirectoryReadByWildcardIsNeverSkipped`
  states that rule for both this directory and `docs/guide/`. The cost is a build on a pull request
  that touches nothing else, which is rare: a slice's own code triggers one anyway.
