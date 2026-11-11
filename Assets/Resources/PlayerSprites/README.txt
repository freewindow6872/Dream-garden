Dream Garden player sprite frames

Existing frames (kept unchanged):
- Idle: idel_01..idel_05 (loop)
- Run: run_01..run_08 (loop)
- Dash: dash_01..dash_02 (loop)

New frames split from Animations.zip:
- Jump ascent: jump_01..jump_02
- Airborne near-apex: air_01..air_02
- Falling: fall_01..fall_02
- Attack hits 1 and 2: attack_1_01..attack_1_04
- Attack hit 3: attack_2_01..attack_2_04
- Hurt: hurt_01
- Death: death_01..death_06

PlayerSpriteAnimator reads PlayerController2D, PlayerCombat and Health.
Movement, hit detection, combo timing and existing gameplay systems are unchanged.
All frames are imported as individual Sprite assets under Resources/PlayerSprites.
