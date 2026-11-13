# Version Detection Patterns

This document explains how the Content Filter plugin detects different version types of tracks.

## Version Detection Logic

The plugin uses pattern matching to identify different version types. Each version type has its own detection logic and confidence level.

### 1. Remix Detection (Confidence: 0.95)

Remix detection identifies tracks that are remixes, edits, bootlegs, reinterpretations, or reworks of original tracks.

#### Detection Patterns:
- **Remix keywords**: remix, edit, bootleg, reinterpretation, rework, remake, reimagined, reinterpreted, remastered, extended mix, club mix, dance mix, house mix, techno mix, trance mix, dub mix, instrumental, flip, rework

#### Examples:
- "Song Name (Remix)" - Remixed version
- "Song Name (Extended Mix)" - Extended remix
- "Song Name (Bootleg)" - Bootleg remix
- "Song Name (Reinterpretation)" - Reinterpreted version
- "Song Name (Re-work)" - Re-worked version
- "Song Name (Instrumental)" - Instrumental version
- "Song Name (Radio Edit)" - Radio edit

#### How it works:
1. Check if track title contains remix indicators
2. Remove remix keywords from title
3. Search for original track without remix keywords
4. Match by normalized title or duration (within tolerance)

### 2. Edit Detection (Confidence: 0.90)

Edit detection identifies edited versions of tracks like radio edits, album edits, and extended versions.

#### Detection Patterns:
- **Edit keywords**: edit, radio edit, album edit, single edit, short edit, compact edit, clean edit, intro edit, outro edit, cut, short version, long version, extended, extended version

#### Examples:
- "Song Name (Radio Edit)" - Radio edit
- "Song Name (Album Edit)" - Album edit
- "Song Name (Single Edit)" - Single edit
- "Song Name (Extended)" - Extended version
- "Song Name (Clean Edit)" - Clean edit

#### How it works:
1. Check if track title contains edit indicators
2. Remove edit keywords from title
3. Search for original track without edit keywords
4. Match by normalized title or duration (within tolerance)

### 3. Version Detection (Confidence: 0.85)

Version detection identifies tracks marked as versions, alternative versions, or alternate versions.

#### Detection Patterns:
- **Version keywords**: version, alternative version, alternate version, different version, special version, unique version, exclusive version

#### Examples:
- "Song Name (Version)" - Version of track
- "Song Name (Alternative Version)" - Alternative version
- "Song Name (Alternate Version)" - Alternate version
- "Song Name (Special Version)" - Special version

#### How it works:
1. Check if track title contains version indicators
2. Remove version keywords from title
3. Search for original track without version keywords
4. Match by normalized title or duration (within tolerance)

### 4. Live Version Detection (Confidence: 0.80)

Live version detection identifies live recordings vs studio versions.

#### Detection Patterns:
- **Live keywords**: live, live version, live at, live from, live in, live on, acoustic live, electric live, unplugged

#### Examples:
- "Song Name (Live)" - Live version
- "Song Name (Live Version)" - Live version
- "Song Name (Live at Budokan)" - Live at specific venue
- "Song Name (Live from Abbey Road)" - Live from specific location
- "Song Name (Acoustic Live)" - Acoustic live version
- "Song Name (Unplugged)" - Unplugged version

#### How it works:
1. Check if track title contains live indicators
2. Remove live keywords from title
3. Search for studio version without live keywords
4. Match by normalized title or duration (within tolerance)

### 5. Acoustic Version Detection (Confidence: 0.80)

Acoustic version detection identifies acoustic versions vs original recordings.

#### Detection Patterns:
- **Acoustic keywords**: acoustic, acoustic version, unplugged

#### Examples:
- "Song Name (Acoustic)" - Acoustic version
- "Song Name (Acoustic Version)" - Acoustic version
- "Song Name (Unplugged)" - Unplugged version

#### How it works:
1. Check if track title contains acoustic indicators
2. Remove acoustic keywords from title
3. Search for original version without acoustic keywords
4. Match by normalized title or duration (within tolerance)

### 6. Demo Detection (Confidence: 0.75)

Demo detection identifies demo versions vs final releases.

#### Detection Patterns:
- **Demo keywords**: demo, demo version, demo recording, demo session, demo take

#### Examples:
- "Song Name (Demo)" - Demo version
- "Song Name (Demo Version)" - Demo version
- "Song Name (Demo Recording)" - Demo recording
- "Song Name (Demo Session)" - Demo session
- "Song Name (Demo Take)" - Demo take

#### How it works:
1. Check if track title contains demo indicators
2. Remove demo keywords from title
3. Search for original version without demo keywords
4. Match by normalized title or duration (within tolerance)

## Matching Algorithm

For each version type, the plugin uses a tiered matching approach:

### Tier 1: Exact Title Match
- Normalize both titles (lowercase, remove featuring, remove trailing punctuation)
- Check if normalized titles match exactly
- High confidence match

### Tier 2: Duration Match
- If title match fails, check duration difference
- Duration must be within configured tolerance (default: 3000ms)
- Medium confidence match

### Tier 3: Combined Match
- Both title and duration must match
- Highest confidence match

## Decision Logic

When analyzing an album:

1. **All tracks are versions**: Unmonitor entire album
2. **Majority are versions** (>50%): Unmonitor entire album
3. **Mixed content** (some versions, some originals): Flag for review
4. **No versions found**: Leave album as-is

## Configuration Options

Users can configure which version types to detect:

- **Enable remix filtering**: Detect remixes, edits, bootlegs
- **Enable version filtering**: Detect version markers
- **Enable live version filtering**: Detect live versions
- **Enable acoustic filtering**: Detect acoustic versions
- **Enable edit filtering**: Detect edit versions
- **Enable demo filtering**: Detect demo versions

Users can also configure:

- **Release types to filter**: Album, EP, Single, Compilation
- **Duration tolerance**: Maximum duration difference for matching
- **Minimum/maximum track duration**: Ignore tracks outside this range
- **Version filter action**: Flag for review, Unmonitor, or Skip

## Example Scenarios

### Scenario 1: Remix Album
```
Track 1: "Song A (Remix)" - Remix of "Song A"
Track 2: "Song B (Extended Mix)" - Extended version of "Song B"
Track 3: "Song C (Bootleg)" - Bootleg of "Song C"
```
Result: All tracks identified as versions → Album unmonitored

### Scenario 2: Mixed Album
```
Track 1: "Song A" - Original
Track 2: "Song B (Acoustic)" - Acoustic version
Track 3: "Song C (Live)" - Live version
```
Result: 1 original, 2 versions → Flagged for review

### Scenario 3: Original Album
```
Track 1: "Song A"
Track 2: "Song B"
Track 3: "Song C"
```
Result: No versions found → Album left as-is

## Performance Considerations

- Title normalization is cached for efficiency
- Version matching uses early exit when a match is found
- Duration comparison is O(n) per track
- Large libraries benefit from configured duration ranges

## Limitations

- Relies on proper tagging in track titles
- May miss versions with unusual naming conventions
- Duration-based matching can have false positives for same-length tracks
- No MusicBrainz metadata integration (uses title patterns only)
