# Hold'em Image Prompts

Generation path: Codex built-in image generation. The explicit local `gpt-image-2` API/CLI path
was not used because `OPENAI_API_KEY` was not set in the local shell.

Intended import folder: `Assets/Holdem/Sprites/Cards/`

Final front-card sprites are assembled deterministically from the generated blank card frame,
rank labels, and suit pips. Do not rely on image generation to produce a perfect 52-card sheet.

## Expected Filenames

- `Assets/Holdem/Sprites/Cards/card_back_acorn.png`
- `Assets/Holdem/Sprites/Cards/card_front_template.png`
- `Assets/Holdem/Sprites/Cards/pip_club.png`
- `Assets/Holdem/Sprites/Cards/pip_diamond.png`
- `Assets/Holdem/Sprites/Cards/pip_heart.png`
- `Assets/Holdem/Sprites/Cards/pip_spade.png`
- `Assets/Holdem/Sprites/Cards/Fronts/2C.png` through `Assets/Holdem/Sprites/Cards/Fronts/AS.png`

## Prompts

### Card Back

Prompt:

```text
Create a single pixel-art poker card back sprite for a retro 2D game UI. Real poker-card proportion, 5:7 aspect ratio. Brown-toned palette with a centered acorn motif. Decorative but readable border, warm beige and brown colors, oak leaf accents, symmetrical design, crisp pixel-art rendering, no text, no watermark, no extra scene background.
```

### Blank Card Face

Prompt:

```text
Create a single blank pixel-art poker card front sprite for a retro 2D game UI. Real poker-card proportion, 5:7 aspect ratio. Standard playing-card look with clean ivory face, black outline, moderate border, and space for rank/suit symbols in corners and center. Crisp pixel-art rendering, no text, no numbers, no suit symbols, no watermark.
```

### Suit Pips

Prompt:

```text
Create a clean pixel-art playing-card suit icon sprite for a retro 2D game UI: [club/diamond/heart/spade]. Small, readable, crisp, centered, transparent or plain clean background, no text, no watermark.
```

## Generation Status

Generation and deterministic assembly completed with 240x336 card sprites and 64x64 pip sprites.
The Hold'em scene builder and `HoldemCardTexturePostprocessor` enforce pixel-art import settings
for the `Assets/Holdem/Sprites/Cards/` folder.
