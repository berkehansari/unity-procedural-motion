// -----------------------------------------------------------------------------
// Copyright (c) 2026 Berkehan Sarı. All rights reserved.
// https://github.com/berkehansari/unity-procedural-motion.git
// This software is provided "as is", without warranty of any kind.
// For licensing details and usage, refer to the LICENSE file in the repository.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace Org.BerkehanSari.ProceduralMotion
{
    /// <summary>
    /// Provides physical motion data required for procedural animations without tight coupling.
    /// </summary>
    public interface IMotionStateProvider
    {
        bool IsGrounded { get; }
        Vector3 Velocity { get; }
    }
}