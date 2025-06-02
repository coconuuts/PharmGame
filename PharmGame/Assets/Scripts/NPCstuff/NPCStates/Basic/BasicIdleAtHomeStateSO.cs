// --- START OF FILE BasicIdleAtHomeStateSO.cs ---

using UnityEngine;
using System; // Needed for System.Enum
using Game.NPC.TI; // Needed for TiNpcData
using Game.NPC.BasicStates; // Needed for BasicState enum

namespace Game.NPC.BasicStates // Place Basic States in their own namespace
{
    /// <summary>
    /// Basic state for simulating an inactive TI NPC waiting idly at their home position
    /// before their scheduled day begins. Operates directly on TiNpcData.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicIdleAtHomeState_", menuName = "NPC/Basic States/Basic Idle At Home", order = 10)] // Order appropriately
    public class BasicIdleAtHomeStateSO : BasicNpcStateSO
    {
        // --- BasicNpcStateSO Overrides ---
        public override System.Enum HandledBasicState => BasicState.BasicIdleAtHome;

        // This state waits indefinitely until the day starts, so it does not use the standard timeout.
        public override bool ShouldUseTimeout => false;

        public override void OnEnter(TiNpcData data, BasicNpcStateManager manager)
        {
            base.OnEnter(data, manager); // Call base OnEnter (logs entry, resets timer/target/path data based on ShouldUseTimeout=false)

            // Ensure the NPC is at its home position and rotation while in this state simulation.
            // The base OnEnter clears simulatedTargetPosition and sets the timer to 0 if ShouldUseTimeout is false, which is correct here.
            data.CurrentWorldPosition = data.HomePosition;
            data.CurrentWorldRotation = data.HomeRotation;

            // Also explicitly clear any path simulation data just in case (defensive)
            data.simulatedPathID = null;
            data.simulatedWaypointIndex = -1;
            data.simulatedFollowReverse = false;
            data.isFollowingPathBasic = false; // Not following a path in this state

            Debug.Log($"SIM {data.Id}: BasicIdleAtHome OnEnter. Set position to Home: {data.CurrentWorldPosition}.", data.NpcGameObject);
        }

        public override void SimulateTick(TiNpcData data, float deltaTime, BasicNpcStateManager manager)
        {
            // In this state, the NPC is simply waiting.
            // Movement and state transitions are triggered externally by TiNpcManager
            // when the NPC's startDay schedule begins.
            // Therefore, this SimulateTick method does nothing with position, rotation, or timers.
            // The position and rotation should already be at Home from OnEnter.

            // Simulation logic here *only* if this state needed internal timers for other things,
            // but for simple "wait until schedule starts", the schedule check is external.

            // No movement, no timer, no transitions from here.
        }

        public override void OnExit(TiNpcData data, BasicNpcStateManager manager)
        {
            base.OnExit(data, manager); // Call base OnExit (logs exit)

            // No specific cleanup needed on exit, the next state's OnEnter
            // will set up the new position/target/timers.
        }
    }
}
// --- END OF FILE BasicIdleAtHomeStateSO.cs ---