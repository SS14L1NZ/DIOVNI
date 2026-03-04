# Item Quality & Wear system
item-quality-verb-text = Quality
item-quality-verb-message = Examine the quality and condition.

# Short examine line (shown on basic shift-examine)
item-quality-examine-short = Quality: {$quality}. Condition: {$condition}.

# Detailed examine lines
item-quality-examine-quality = Quality: {$quality}
item-quality-examine-wear = Condition: {$condition} ({$percent})
item-quality-examine-wear-vague = Condition: {$condition}
item-quality-examine-effectiveness = Overall effectiveness: {$value}
item-quality-examine-effectiveness-vague = Overall effectiveness: {$rating}

# Weapon damage ratings
item-quality-examine-damage = Combat power: {$rating}
item-quality-damage-devastating = Devastating
item-quality-damage-powerful = Powerful
item-quality-damage-moderate = Moderate
item-quality-damage-light = Light
item-quality-damage-negligible = Negligible

# Accelerated degradation warning
item-quality-examine-accelerated-wear = [color=#ff5050][italic]This item is degrading rapidly due to critical condition![/italic][/color]

# Effectiveness ratings
item-quality-effectiveness-outstanding = Outstanding
item-quality-effectiveness-high = Above average
item-quality-effectiveness-normal = Normal
item-quality-effectiveness-reduced = Below average
item-quality-effectiveness-poor = Poor
item-quality-effectiveness-terrible = Terrible

# Quality tiers
item-quality-tier-junk = Junk
item-quality-tier-poor = Poor
item-quality-tier-normal = Normal
item-quality-tier-good = Good
item-quality-tier-excellent = Excellent
item-quality-tier-masterwork = Masterwork

# Wear categories
item-quality-wear-pristine = Pristine
item-quality-wear-good = Good condition
item-quality-wear-worn = Worn
item-quality-wear-damaged = Damaged
item-quality-wear-broken = Barely holding together

# Weapon jamming
item-quality-weapon-jammed = The weapon jammed!
item-quality-weapon-unjammed = You unjam the weapon.

# Destruction
item-quality-weapon-exploded = The weapon explodes from critical wear!
item-quality-item-destroyed = The item falls apart from wear!

# Limb loss
item-quality-weapon-exploded-limb = The explosion tears off a limb!

# Repair
item-quality-repair-not-needed = This item doesn't need repair.
item-quality-repair-no-materials = Not enough materials for repair!
item-quality-repair-no-plasteel = Not enough plasteel for repair!
item-quality-repair-start-weapon = You begin repairing the weapon...
item-quality-repair-start-armor = You begin repairing the armor...
item-quality-repair-success-weapon = Weapon repaired successfully!
item-quality-repair-success-armor = Armor repaired successfully!

# Appraiser lens UI
appraiser-lens-title = Lens Calibration
appraiser-lens-focusing = Rotate the lenses to focus...
appraiser-lens-timing-phase = Click when the dot is in the green zone!
appraiser-lens-complete = + Analysis Complete
appraiser-lens-signal = Signal:
appraiser-lens-melee-damage = Melee damage: {$base} -> {$adjusted}
appraiser-lens-fire-rate = Fire rate: {$value} rps
appraiser-lens-scan-transmitted = [color=#50ff50]+ Scan data transmitted to PDA archive.[/color]
appraiser-lens-scan-accuracy = [color=#88ccff]Calibration accuracy: {$value}%[/color]
appraiser-lens-phase-calibration = Phase 1: Lens Calibration
appraiser-lens-phase-sync = Phase 2: Signal Synchronization
appraiser-lens-phase-complete = + Calibration Complete
appraiser-lens-timing-round = Round {$current}/{$total} — click in the zone!
appraiser-lens-accuracy = Accuracy: {$value}%
appraiser-lens-miss = [color=#ff5555]Miss![/color]

# PDA Appraiser Archive cartridge
appraiser-archive-program-name = Appraiser Archive
appraiser-archive-header = Scan Archive
appraiser-archive-clear = Clear
appraiser-archive-empty = No scanned items yet.
appraiser-archive-type = Type: {$type}
appraiser-archive-category-ranged = Ranged Weapon
appraiser-archive-category-melee = Melee Weapon
appraiser-archive-category-hybrid = Hybrid Weapon
appraiser-archive-category-armor = Armor / Equipment
appraiser-archive-category-item = Item

# Archive expanded view -- sections
appraiser-archive-section-weapon = -- Weapon Stats --
appraiser-archive-section-condition = -- Condition --
appraiser-archive-section-armor = -- Armor Protection --
appraiser-archive-section-price = -- Estimated Value --
appraiser-archive-section-extras = -- Additional Properties --
# Archive -- price estimate
appraiser-archive-price-range = ~ {$min} -- {$max} credits
appraiser-archive-price-unavailable = [color=#ff5050]Price module not installed[/color]

# Archive expanded view -- gun stats
appraiser-archive-gun-spread = Spread: {$min} - {$max} deg
appraiser-archive-gun-speed = Projectile speed: {$value}
appraiser-archive-gun-mode = Fire mode: {$mode}
appraiser-archive-gun-burst = Burst: {$value} rounds

# Archive expanded view -- fire modes
appraiser-archive-firemode-SemiAuto = Semi-Auto
appraiser-archive-firemode-FullAuto = Full-Auto
appraiser-archive-firemode-Burst = Burst

# Archive expanded view -- condition
appraiser-archive-jam-capable = (!) Can jam
appraiser-archive-jammed = (!) JAMMED

# Archive expanded view -- wear summary
appraiser-archive-wear-info = Wear: {$percent}% | Effectiveness: {$effective}%

# Archive expanded view -- extra stats (word-based before decryption)
appraiser-archive-slowdown = Movement: {$rating}
appraiser-archive-explosion = Explosion resistance: {$rating}
appraiser-archive-stamina = Stamina protection: {$rating}

# Archive -- decryption
appraiser-archive-decrypt-button = > Decrypt
appraiser-archive-decrypt-title = Signal Decryption
appraiser-archive-decrypt-instruction = Click when the marker is in the green zone!
appraiser-archive-decrypt-instruction-counter = Click when BOTH markers overlap in the zone!
appraiser-archive-decrypt-instruction-drift = Click when the marker is in the moving zone!
appraiser-archive-decrypt-type-signal = > Signal Lock
appraiser-archive-decrypt-type-counter = > Counter-Sweep
appraiser-archive-decrypt-type-drift = > Drift Lock
appraiser-archive-decrypt-cancel = Cancel
appraiser-archive-decrypt-success = + Data decrypted
appraiser-archive-decrypt-fail = X Decryption failed
appraiser-archive-decrypt-round = Round {$current}/{$total}
