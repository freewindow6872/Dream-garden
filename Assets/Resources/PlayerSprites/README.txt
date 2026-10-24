Dream Garden player sprite frames

These frame PNGs are loaded at runtime by PlayerSpriteAnimator.cs.
Idle: idel_01..idel_05 (loop)
Run: run_01..run_08 (loop)
Dash: dash_01..dash_02 (loop while dash is active)

PlayerSpriteAnimator is attached to the player object in SampleScene and Attack test.
Sprite frames use a shared transparent canvas and bottom pivot to reduce animation jitter.
