# 🔫 Procedural Motion Toolkit for Unity

[![Unity Automated Test Suite](https://github.com/berkehansari/unity-procedural-motion/actions/workflows/unity-test.yml/badge.svg)](https://github.com/berkehansari/unity-procedural-motion/actions/workflows/unity-test.yml)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg?style=flat&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Cross--Platform-lightgrey.svg)]()
[![OpenUPM](https://img.shields.io/badge/UPM-Compatible-orange.svg)]()

A highly optimized, production-ready, AAA-quality procedural animation and physics inertia system for Unity. This package decouples complex first-person movement constraints from visual rendering, utilizing **Second Order Dynamics (Mass-Spring Systems)** to provide hyper-realistic weapon sway, kinetic recoil impulse, directional physical lag, and organic micro-drifts.

Designed with **Clean Architecture** principles, it introduces complete controller independence, enabling developers to plug the system into any custom movement implementation (Rigidbody, CharacterController, or Raycast-based) via standard interface signatures.

## Visual Demonstrations

<p align="center">
  <img src="Documentation/GIFs/weapon_sway.gif" width="48%" title="Second Order Inertia Sway" />
  <img src="Documentation/GIFs/weapon_bobbing.gif" width="48%" title="Kinetic Recoil & Wobble" />
</p>

---

## 📂 Package Architecture

Built natively according to the **Unity Package Manager (UPM)** distribution layouts:

```plaintext
unity-procedural-motion/
├── Runtime/                  # Core package abstraction layer
│   ├── org.berkehansari.proceduralmotion.asmdef
│   ├── IMotionStateProvider.cs
│   └── ProWeaponAnimator.cs
├── Samples~/                 # Importers for sandbox evaluation
│   └── DemoSetup/
│       ├── PlayerController.cs
│       └── DemoScene.unity
├── Tests/                    # High-coverage algorithmic test suite
│   └── Runtime/
│       ├── org.berkehansari.proceduralmotion.Tests.asmdef
│       └── WeaponAnimatorTests.cs
├── package.json              # UPM package manifest data
└── README.md                 # Technical documentation
```

---

## 📐 Mathematical Foundations

Standard linear interpolation (`Mathf.Lerp`) often feels robotic and lacks physical weight. To achieve true AAA gunplay (inspired by _Modern Warfare 2019_ and _Escape from Tarkov_), this toolkit completely abandons traditional interpolation in favor of **Second Order Dynamics (Hooke's Law)**.

### ⚙️ Mass-Spring System (Kinetic Inertia)

We calculate the weapon as a physical mass attached to a spring. When the player moves or looks around, the weapon overshoots slightly and snaps back, providing realistic weight and momentum:

$$ \mathbf{F}_{spring} = (\mathbf{X}_{target} - \mathbf{X}_{current}) \cdot k $$
$$ \mathbf{v}_{current} = (\mathbf{v}_{current} + \mathbf{F}_{spring} \cdot \Delta t) \cdot (1 - d \cdot \Delta t) $$
$$ \mathbf{X}_{current} = \mathbf{X}_{current} + \mathbf{v}\_{current} \cdot \Delta t $$

_Where $k$ is the structural stiffness, $d$ is the friction/damping multiplier, and $\mathbf{v}$ is the current kinetic velocity vector._

### 🌊 Organic Micro-Drift & Bobbing

Rather than utilizing perfectly symmetric sine waves which feel artificial, the idle sway incorporates 3D **Perlin Noise** to simulate natural human breathing and muscle fatigue (micro-drift).

Furthermore, rhythmic stepping injects absolute values of Lissajous curves to simulate tactile downward shoulder impacts (Jolts) upon foot grounding.

---

## 🚀 Installation Guide

### Via Unity Package Manager (Git URL)

1. Open your Unity project and navigate to **Window** $\rightarrow$ **Package Manager**.
2. Click the **+** (plus) icon in the top-left corner and select **Add package from git URL...**
3. Input the following target repository reference:
   ```text
   https://github.com/berkehansari/unity-procedural-motion.git
   ```
4. Click **Add**. Unity will automatically fetch the codebase, optimize compilation configurations via Assembly Definitions, and index the package utilities.

---

## 💻 Seamless Integration Blueprint

To make your custom movement script compatible with the animation network, implement the decoupled interface signature `IMotionStateProvider`:

```csharp
using UnityEngine;
using Org.BerkehanSari.ProceduralMotion;

public class MyCustomController : MonoBehaviour, IMotionStateProvider
{
    // 1. Fulfill the contract requirements
    public bool IsGrounded => CheckIfGrounded();
    public Vector3 Velocity => GetPhysicalVelocity();

    private bool CheckIfGrounded() { /* Your custom logic */ return true; }
    private Vector3 GetPhysicalVelocity() { /* Your custom velocity vector */ return Vector3.zero; }
}
```

Then, simply ensure your visual weapon object hierarchy hosts the `ProWeaponAnimator` component. It will automatically traverse its parent transforms at initialization, latch onto your contract provider, and run physical calculations out of the box.

---

## 🦾 API & Feedback Controls

Trigger cinematic impacts and kinetic impulses directly from your gameplay scripts:

```csharp
public class WeaponCombatHandler : MonoBehaviour
{
    public ProWeaponAnimator weaponAnimator;

    public void FireWeapon()
    {
        // Injects raw kinetic energy into the spring system.
        // Causes actual barrel wobble and chaotic directional snap-back (MW19 style).
        weaponAnimator.RecoilFire(new Vector3(0, 0.01f, -0.08f), new Vector3(-8f, 2.5f, 3f));
    }

    public void TakeDamage()
    {
        // Triggers massive directional trauma shake matrices on the physical weapon geometry.
        weaponAnimator.TriggerDamageEffect(intensityMultiplier: 1.5f);
    }
}
```

---

## 🛡️ License

Distributed under the MIT License. See `LICENSE` file for more details.
