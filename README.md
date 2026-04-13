# Unity FPS Gameplay Systems
**A modular first-person gameplay framework for Unity 6** — physics-based movement, free climbing, physical grab, interaction system, and a localized trauma health system.

Built as a prototype testbed for an action RPG, referencing Skyrim, Dying Light, and Zelda:BoTW.

<img width="1542" height="784" alt="image" src="https://github.com/user-attachments/assets/bc9d363b-ba64-4a3d-ac6d-c107fd71c4aa" />

---

## Systems

### Player Movement & Camera
- Rigidbody-based first-person controller with Walk, Sprint, Crouch, and Vault states
- Camera controller with head bob, landing bob, strafe tilt, vault dip, FOV shift, and anti-clip SphereCast
- Viewmodel controller — arms locked to camera via LateUpdate with configurable offset
- Full Input System integration via `PlayerInputHandler` abstraction layer

### Free Climbing
- Wall detection via SphereCast — triggers on jump input or approach velocity
- Continuous wall adhesion with normal tracking and auto-fall on surface loss
- Wall jump, hop up, and crouch-to-drop exits
- Procedural ledge grab — two-phase pull up and step over coroutine
- Yaw correction on wall exit to prevent camera snap

### Physical Grab & Throw
- SphereCast grab detection with mass limit
- Spring-force hold system with smoothed target to prevent jitter on fast camera movement
- Direct MovePosition snap at close range to eliminate micro-oscillations
- Throw via impulse force, drop with momentum preservation
- Auto-release on distance threshold
- Grab-to-interact pipeline — grabbed physics items can trigger `IInteractable`

### Interaction System
- `IInteractable` interface — open contract for any world object
- SphereCast detection from camera with configurable radius and distance
- Grab blocks interaction prompt to prevent input conflicts
- Included implementations: `InteractableDoor`, `InteractableItem`, `InteractablePhysicsItem`

### Trauma Health System *(in progress)*
- 10 body parts with individual HP pools (`BodyPartReceiver`)
- Per-part damage modifiers: weaknesses and resistances by `DamageType` (Slashing, Blunt, Pierce, Magic, Fire)
- Vital part death triggers global `OnEntityDeath` event
- Ragdoll transition with agony phase — high damping on death, relaxes after configurable duration
- Directional impulse applied to the fatal bone on death
- `EnemyAnimator` decoupled from health via C# events — no direct references

---

## Requirements

- Unity 6 (6000.x)
- Unity Input System package
- Rigidbody-based character (no CharacterController)

---

## Project Structure

```
Assets/
└── _Project/
    └── Scripts/
        ├── Camera/
        │   ├── CameraController.cs
        │   └── ViewmodelController.cs
        ├── Combat/
        │   ├── BodyPartReceiver.cs
        │   ├── DamageType.cs
        │   ├── EnemyAnimator.cs
        │   ├── EnemyRagdoll.cs
        │   └── EntityHealth.cs
        ├── Core/
        │   └── PlayerState.cs
        ├── Interaction/
        │   ├── GrabPoint.cs
        │   ├── IInteractable.cs
        │   ├── InteractableDoor.cs
        │   ├── InteractableItem.cs
        │   ├── InteractablePhysicsItem.cs
        │   └── PlayerInteraction.cs
        └── Player/
            ├── PlayerClimb.cs
            ├── PlayerGrab.cs
            └── PlayerMovement.cs
```

---

## Known Limitations

- Combat system is partially implemented — hitbox registration and ranged weapon systems are work in progress
- Grab requires manual configuration of `GrabPoint` transform in Inspector
- Climb wall detection relies on a dedicated Climbable layer

---

## License

MIT — free for personal and commercial use.

---

Made by [Bababuyyy](https://github.com/bababuyyy)
