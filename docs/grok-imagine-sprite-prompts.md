# Grok Imagine Sprite Prompt Checklist

Codex handling rule for this file: when asked to add, revise, or refine Grok Imagine sprite prompts, edit this markdown file directly instead of only replying with the prompt text.

현재 에셋 기준 스타일: 고해상도 도트풍, 굵은 검은 외곽선, 귀여운 판타지 몬스터, 단색 배경에서 중앙 정렬된 2D 스프라이트.

## Successful Prompting Approach

- 업로드한 idle 0번 프레임이 캐릭터 디자인의 source of truth다. 프롬프트는 디자인을 길게 다시 설명하기보다 `Use the uploaded ... idle frame as reference`로 시작하고, 색상/방향/배경 유지 지시만 짧게 반복한다.
- 결말 포즈가 중요한 죽음 애니메이션은 시작 idle 프레임과 최종 KO 프레임을 둘 다 업로드하고, 프롬프트에서 `starting-frame reference`와 `ending-frame reference`를 따로 고정한다.
- 성공한 Player 프롬프트는 모두 짧은 전투 애니메이션 지시문이다. `Short 2D 8-bit pixel art battle ... animation`처럼 용도와 길이를 먼저 고정한다.
- 한 프롬프트에는 한 가지 주 동작만 넣는다. 공격은 한 손 투척, 방어는 권투 가드, 죽음은 최종 타격 후 철푸덕 쓰러짐처럼 결과가 즉시 떠오르는 동사로 쓴다.
- 원하는 모션은 최종 결과만 쓰지 말고 3-5개의 선명한 중간 단계로 쓴다. 예: `impact flinch -> paws go limp -> torso pitches forward -> whole body slams down`.
- 자연스럽게 엎드리거나 낙법치는 문제가 있으면, 금지어를 길게 늘리기보다 팔/몸의 역학을 긍정문으로 고정한다. 예: `paws fling loose instead of preparing to catch the fall`.
- 표정은 캐릭터 디자인과 별개로 명시한다. Idle의 웃는 얼굴이 유지되면 안 되는 동작에는 `smile disappears`, `pained defeated face`, `mouth opens in pain`처럼 짧게 넣는다.
- 금지어는 최소한만 쓴다. Grok이 반복적으로 잘못 생성한 요소만 짧게 막고, 나머지는 긍정적인 샷 방향으로 해결한다.
- 마지막 프레임이 중요한 애니메이션은 최종 포즈를 명확히 쓴다. 예: `return to the exact original idle pose`, `End completely still in a limp defeated KO pose`.

## Common Base Prompt

```text
2D pixel art game sprite animation, clean chunky pixels, thick dark outline, cute fantasy RPG style, full body character centered, side-facing battle pose, orthographic view, no camera movement, no zoom, single flat solid color background for chroma key, no ground, no shadow, no text, no UI, no watermark, consistent character design across frames, short animation loop, sprite sheet friendly
```

## Common Negative Prompt

```text
realistic, 3D render, painterly, blurry, anti-aliased, detailed background, scenery, particles covering the character, camera shake, perspective distortion, text, logo, watermark, cropped body, extra limbs, inconsistent costume, huge frame-to-frame design changes
```

## Prompting Notes

- If using image-to-video, upload the existing sprite frame first and keep the prompt short.
- Describe one main motion only.
- Let the reference image carry the character design.
- For image-to-video sprite work, focus the prompt on subject motion, timing, and locked camera. Do not redescribe details already visible in the uploaded frame.
- Put "no camera movement, no zoom" near the motion sentence when extracting sprite frames from video.
- Avoid long lists of facial details; they often make the expression look forced.
- Prefer gentle wording like "slight flinch" or "briefly squints" instead of dramatic emotion words.
- For Grok Imagine image-to-video, write constraints as positive shot direction inside the main prompt instead of appending a labeled `Negative prompt:` block.
- Put the most important motion and camera constraints near the beginning. Later sentences can drift into later parts of the video.
- For Low HP / hurt poses, do not write "Not NSFW" or similar labels. They can pull the model toward the wrong concept. Instead, anchor the prompt to "battle injury", "sports injury", "pale face", "cold sweat", "clenched teeth", and "serious pain".
- If Grok adds cheek blush or a shy/flustered expression, replace that instruction with: "Use pale gray facial shading and under-eye shadows instead of any pink or red cheek coloring."
- Keep image-to-video prompts compact. Prefer a few strong motion constraints over long exhaustive lists.
- For attacks using separate projectile assets, describe only the character motion. Do not ask Grok to draw the projectile, held item, trail, or impact.
- When the action must use one hand, state the body mechanics positively: rear paw/hand performs the action, front paw/hand stays near the chest for balance.
- If Grok makes a heavy two-handed shove/lift, steer toward a familiar sports motion such as "one-handed overhand throw like a baseball pitch".
- Preserve the completed prompt style: uploaded idle frame as reference, exact flat background color, centered full body, planted feet, return to original idle pose, minimal negative wording.

## Bat Prompt Pitfalls

- Current Bat workflow: upload `Assets/Mobs/Sprites/Bat/Idle/0.png` to Grok Imagine as the image-to-video reference, generate a short video, extract frames from that video, then clean/remove the background for Unity sprite frames.
- In this workflow, the uploaded idle frame should carry the bat design. The text prompt should only steer the motion and extraction constraints. Over-describing the character or the incoming hit tends to make Grok add new visual layers.
- The specific recurring issue was a soft purple gradient/aura layer appearing around the bat on a white background. This is not wanted sprite content; it makes background removal harder and creates dirty transparent edges.
- Bat image-to-video easily adds a purple glow, haze, gradient layer, or motion blur around the body because the bat itself is purple. For Bat prompts, put `pure flat white background`, `hard black outline`, `crisp cutout edges`, and `no glow or colored haze` near the beginning.
- Avoid `battle damage animation` for Bat Hit if Grok keeps inventing VFX. Prefer `2D pixel art sprite animation` plus `quick in-place hit-stun motion`.
- Do not mention the hit source, incoming attack, projectile, slash, or impact. Use only the bat's body reaction: `jerks a few pixels right`, `wings tuck inward`, `body briefly squashes`.
- Bat Hit should keep the original idle expression unless a different expression is explicitly requested. Add `same design and original expression`.
- White background is preferred for Bat Hit extraction because it makes chroma/background removal simpler. Still explicitly ban `gradient`, `shadow`, `motion blur`, and `colored haze`.
- Put `Only the bat moves` before negative constraints to reduce background pulses, aura layers, and extra effects.

## Task Checklist

| Done | Target | Needed |
|---|---|---|
| [1] | Player squirrel | Idle |
| [1] | Player squirrel | Small Hit |
| [1] | Player squirrel | Strong Hit |
| [1] | Player squirrel | Low HP / Death Danger |
| [1] | Player squirrel | Attack |
| [1] | Player squirrel | Defense |
| [1] | Player squirrel | Death |
| [1] | Bat | Attack |
| [1] | Bat | Hit |
| [1] | Bat | Dead |
| [1] | Goblin | Attack |
| [1] | Goblin | Hit |
| [1] | Goblin | Dead |
| [ ] | Skeleton archer | Attack |
| [ ] | Skeleton archer | Hit |
| [ ] | Skeleton archer | Dead |
| [ ] | Slime | Attack |
| [ ] | Slime | Hit |
| [ ] | Slime | Dead |
| [ ] | VFX | Defense Effect |
| [ ] | VFX | Player Hit Effect |
| [ ] | VFX | Enemy Hit Effect |
| [ ] | VFX | Player Death Effect |
| [ ] | VFX | Enemy Death Effect |

## Player Prompts

### Player Idle

```text
A cute brown squirrel hero, big sparkling black eyes, cream belly, curled fluffy tail, small paws, cheerful but ready for battle, idle breathing animation, tail gently swaying, 2D pixel art game sprite animation, thick dark outline, centered full body, facing right, flat cyan background, no ground, no shadow, no text, 16 frames
```

### Player Small Hit

```text
Use the uploaded squirrel idle frame as the character reference. Short 2D 8-bit pixel art battle damage animation: a small in-place combat knockback. The squirrel is lightly hit from the front, keeps facing the attacker, shifts only a few pixels backward once, gives a small pained wince, then returns to the exact original idle pose. No walking, no running, no leaving center frame. Preserve the uploaded character colors and plain background color. Full body centered, thick dark outline, 12 frames.
```

### Player Strong Hit

```text
Use the uploaded squirrel idle frame as reference. Short 2D 8-bit pixel art battle damage animation: strong hit reaction on the spot. The squirrel is visibly struck by an outside blow, keeps the same side-view pose and planted feet, body compresses hard, snaps back, then rebounds from the hit. Tail and ears lag from impact, eyes squeeze shut in pain, then returns to the exact original idle pose. No forward motion, no floating, no running, no turn toward camera. Preserve colors and flat background. Full body centered, thick dark outline, 14 frames.
```

### Player Low HP / Death Danger

```text
Use the uploaded squirrel idle frame as the character reference. Short 2D 8-bit pixel art low HP game sprite animation: the same squirrel hero is exhausted because HP is low, standing still in a tired battle idle. Keep the same squirrel design, round tail, face, colors, side-facing pose, and exact flat background color from the reference. Feet stay planted in the same place for the whole animation. Only use small upper-body motion: slow heavy breathing, slightly hunched shoulders, drooping ears, tail resting lower, one paw held near the ribs. Very subtle idle loop, almost still, no dance-like movement. Full body centered, thick dark outline, flat solid background, no ground, no shadow, no text, 16 frames loop.
```

### Player Attack

```text
Use the uploaded squirrel idle frame as reference. Short 2D 8-bit pixel art battle attack animation: the squirrel performs a quick one-handed overhand throw like a baseball pitch. Keep the same design, colors, side-facing pose, and exact flat background color. Facing right, full body centered. Rear paw pulls behind the head, shoulder twists back, then that single paw whips forward and releases. Front paw stays close to the chest and does not join the throw. Feet stay planted with only a tiny weight shift. Tail balances the motion, determined expression, then returns to the exact original idle pose. Show only the squirrel, no projectile or held item. No two-handed motion, no camera movement, no scenery, no ground, no shadow, no text, 12 frames.
```

### Player Defense

```text
Use the uploaded squirrel idle frame as the character reference. Short 2D 8-bit pixel art battle defense animation: the same squirrel hero stands in place with both paws raised in a boxing guard near the face and chest. Keep the same squirrel design, round tail, face, colors, side-facing pose, and exact flat background color from the reference. Facing right, full body centered. Feet stay planted in the same place for the whole animation. Only use small defensive motion: elbows tucked in, shoulders slightly braced, body makes a tiny guarded bounce, eyes focused forward, tail held close for balance. Then returns to the exact original idle pose. No shield, no weapon, no crouching too low, no walking, no camera movement, no scenery, no ground, no shadow, no text, 12 frames.
```

### Player Death

```text
Use the uploaded squirrel idle frame as reference. Short 2D 8-bit pixel art battle defeat animation: the squirrel takes a final invisible hit, lets out a pained yelp, loses the battle, and crashes forward with a heavy flop in place. Keep the same design, colors, side-facing pose, and exact flat background color. Facing right, full body centered. Clear fall sequence: sudden impact flinch, mouth opens in pain, smile disappears, paws fling loose instead of preparing to catch the fall, torso pitches forward, then the whole body slams down at once with weight. End completely still in a limp defeated KO pose, tail dropped beside the body. No tears, no smile, no careful prone pose, no blood, no gore, no camera movement, no scenery, no ground, no shadow, no text, 16 frames.
```

## Enemy Prompts

### Bat Idle

```text
A cute purple fantasy bat monster, wide wings, red glowing eyes, tiny fangs, chunky pixel art, hovering idle animation with slow wing flap, centered full body, facing left, thick dark outline, flat light green background, no cave, no shadow, no text, 16 frames loop
```

### Bat Attack

```text
Use the uploaded bat idle frame as the character reference. Short 2D 8-bit pixel art battle attack animation: the bat performs a quick in-place dive-bite toward the left. Keep the same purple bat design, wide wings, red eyes, tiny fangs, colors, side-facing pose, and exact flat light green background color. Facing left, full body centered. Wings pull back for a brief anticipation, body lunges only a few pixels forward, mouth opens for one bite, then the wings snap back and the bat returns to the exact original idle hovering pose. Keep hovering in place, no landing, no walking, no leaving center frame. Show only the bat, no bite marks, no slash trail, no impact effect. No camera movement, no scenery, no ground, no shadow, no text, 12 frames.
```

### Bat Hit

```text
Use the uploaded bat idle 0 frame as reference. 2D pixel art sprite animation, pure flat white background, hard black outline, crisp cutout edges, no glow or colored haze. Quick in-place hit-stun motion: the bat jerks a few pixels right, wings tuck inward, body briefly squashes, then returns to the exact original idle hover. Keep facing left, centered, same design and original expression. Only the bat moves. No projectile, no impact effect, no motion blur, no gradient, no shadow, no text, 10 frames.
```

### Bat Dead

```text
A cute purple fantasy bat monster defeated, wings folding, body falling limp and fading downward, red eyes dimming, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat light green background, no text, 16 frames
```

### Goblin Idle

```text
A mischievous green goblin rogue, yellow eyes, sharp teeth, patched brown hood, ragged red-brown tunic, wooden club in hand, idle breathing animation, sinister grin, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat cyan background, no ground, no text, 16 frames loop
```

### Goblin Attack

```text
Use the uploaded goblin idle frame as reference. Short 2D pixel art attack animation: with one hand, pull the held weapon slightly behind the back, then swing it down and forward to the left like a baseball bat, return to idle. Same design and colors, facing left, centered, flat neutral light gray background. No slash effect, no camera movement, 12 frames.
```

### Goblin Hit

```text
Use the uploaded goblin idle frame as reference. Short 2D pixel art battle damage animation: quick in-place hit-stun motion. The goblin keeps facing left and stays centered, feet planted, body jerks only a few pixels backward from an invisible hit, head snaps back, shoulders hunch, weapon arm drops slightly, yellow eyes squint in pain, teeth clench, then returns to the exact original idle pose. Same green goblin design, patched hood, wooden club, colors, and flat neutral light gray background. Only the goblin moves. No projectile, no slash effect, no impact flash, no camera movement, no scenery, no ground, no shadow, no text, 10 frames.
```

### Goblin Dead

```text
Animate the provided standing goblin sprite as frame 0.

Keep the exact same pixel-art goblin design, colors, outline thickness, white background, and 2D side-scroller sprite style.

The goblin starts falling immediately, with no idle hold. In the first beat, he drops the wooden club. Then he loses balance backward toward screen-right, falls only within the flat 2D screen plane like a side-scroller sprite, hits the ground, and ends in a limp knockout pose.

Final pose: head on screen-right, feet on screen-left, body low and horizontal near the ground, side-view face with X eyes, club lying separately on the ground.

Fixed camera, strict 2D side view, no perspective depth, no 3D turn, no particles.
```

### Skeleton Archer Idle

```text
A cute skeleton archer monster, oversized skull, black shiny eye sockets, wooden bow, quiver of arrows on back, idle breathing bone rattle animation, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat magenta background, no scenery, no text, 16 frames loop
```

### Skeleton Archer Attack

```text
A cute skeleton archer monster drawing a wooden bow and firing an arrow, oversized skull, black shiny eye sockets, quiver on back, clear anticipation release follow-through, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat magenta background, no text, 14 frames
```

### Skeleton Archer Hit

```text
A cute skeleton archer monster taking damage, bones rattling apart slightly but staying intact, skull tilting, bow lowered, startled eye sockets, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat magenta background, no text, 8 frames
```

### Skeleton Archer Dead

```text
A cute skeleton archer monster defeated, bones collapsing into a small pile, bow falling beside it, skull rolling slightly, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat magenta background, no gore, no text, 16 frames
```

### Slime Idle

```text
A cute green slime monster, glossy jelly body, simple black eyes, tiny smile, gentle wobbling idle animation, translucent highlights, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat magenta background, no ground, no text, 16 frames loop
```

### Slime Attack

```text
A cute green slime monster attacking by stretching forward and bouncing, glossy jelly body, simple black eyes, elastic squash and stretch, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat magenta background, no text, 12 frames
```

### Slime Hit

```text
A cute green slime monster taking damage, jelly body squashed sideways, startled eyes, small droplets popping outward but remaining readable, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat magenta background, no text, 8 frames
```

### Slime Dead

```text
A cute green slime monster defeated, jelly body melting into a puddle, glossy highlights fading, tiny bubbles popping, centered full body, facing left, 2D pixel art game sprite animation, thick dark outline, flat magenta background, no text, 16 frames
```

## VFX Prompts

### Defense Effect

```text
2D pixel art game VFX only, golden translucent shield flash, circular guard barrier, small spark pixels, expands then fades, centered, no character, flat magenta background, no ground, no text, no camera movement, 12 frames
```

### Player Hit Effect

```text
2D pixel art game VFX only, small red-orange impact burst for player damage, star-shaped hit flash, a few tiny sparks, quick pop and fade, centered, no character, flat cyan background, no text, 8 frames
```

### Enemy Hit Effect

```text
2D pixel art game VFX only, sharp white-yellow slash impact burst for enemy damage, diagonal hit streak, small pixel sparks, quick pop and fade, centered, no character, flat magenta background, no text, 8 frames
```

### Player Death Effect

```text
2D pixel art game VFX only, soft blue-white soul sparkle and fading dust, gentle upward particles, sad but cute RPG defeat effect, centered, no character, flat magenta background, no text, 16 frames
```

### Enemy Death Effect

```text
2D pixel art game VFX only, purple smoke puff with small dark pixel fragments, monster defeat vanish effect, expands then dissolves, centered, no character, flat light green background, no text, 16 frames
```

## Background Color Notes

- Player: cyan background.
- Goblin Attack: neutral light gray background, because cyan can cause Grok to shift the goblin's colors.
- Goblin Hit: neutral light gray background, matching Goblin Attack to reduce color shifts.
- Goblin Dead: neutral light gray background, matching Goblin Attack and Hit to reduce color shifts.
- Bat: light green background.
- Skeleton / Slime / most VFX: magenta background.
- If Grok adds floor, shadow, or scenery, append this sentence:

```text
absolutely flat single-color background, character does not touch any floor
```
