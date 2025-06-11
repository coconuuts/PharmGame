// Systems/CraftingMinigames/PillCraftingMinigame.cs
using UnityEngine;
using System; // Needed for Action event
using System.Collections;
using System.Collections.Generic;
using Systems.Inventory;
using TMPro;
using UnityEngine.UI;
using Systems.CameraControl;
using Utils.Pooling;
using System.Linq;
using DG.Tweening;
using Systems.Minigame.Animation;
using Systems.Minigame.Config;
using Systems.Minigame.UI;
using Systems.Minigame.Clicking;
using Systems.Minigame.Movement;

namespace Systems.CraftingMinigames
{
    /// <summary>
    /// Specific implementation of the Crafting Minigame for creating pills.
    /// Manages game state (Pouring, Counting, Packaging), handles player input for counting,
    /// and delegates visual animations to PillCraftingAnimator.
    /// --- MODIFIED: Configuration data partially moved to ScriptableObject. ---
    /// --- MODIFIED: Uses MinigameUIHandler for managing UI elements. --- // <--- ADDED
    /// </summary>
    public class PillCraftingMinigame : CraftingMinigameBase
    {
        [Header("Configuration Data")]
        [Tooltip("Reference to the Pill Minigame ScriptableObject configuration asset.")]
        [SerializeField] private PillMinigameConfigData pillConfig;

        [Header("Camera Targets (Scene References)")]
        [Tooltip("Transform representing where the camera should be during pouring (Initial).")]
        [SerializeField] private Transform pouringCameraTarget;
        [Tooltip("Transform representing where the camera should be during counting.")]
        [SerializeField] private Transform countingCameraTarget;
        [Tooltip("Transform representing where the camera should be during packaging.")]
        [SerializeField] private Transform packagingCameraTarget;

        [Header("Spawn Points (Scene References)")]
        [Tooltip("Transform where the Pill Stock Container should be instantiated.")]
        [SerializeField] private Transform pouringContainerSpawnPoint;
        [Tooltip("Transform where the Prescription Container should be instantiated.")]
        [SerializeField] private Transform prescriptionContainerSpawnPoint;
        [Tooltip("Transform where the Prescription Container Lid should be instantiated.")]
        [SerializeField] private Transform prescriptionContainerLidSpawnPoint;
        [Tooltip("Transform where the Pill Stock Container should move after pouring.")]
        [SerializeField] private Transform stockContainerSetdown;

        [Header("Object Points (Scene References)")]
        [Tooltip("Transform representing the point where pills are logically poured from (Fallback).")]
        [SerializeField] private Transform pillSpawnPoint; // Fallback if animator point is missing

        [Tooltip("Transform representing the point where discarded pills are moved.")]
        [SerializeField] private Transform pillDiscardPoint;
        [Tooltip("Transform representing the point where reclaimed pills are moved back to.")]
        [SerializeField] private Transform pillReclaimPoint;


        [Header("UI References (Component Reference)")] // <--- Header updated
        [Tooltip("Reference to the MinigameUIHandler component for managing UI.")]
        [SerializeField] private MinigameUIHandler uiHandler; // <--- ADDED

        [Header("Input Handler (Component Reference)")] // <--- ADDED Header
        [Tooltip("Reference to the ClickInteractor component for handling input.")]
        [SerializeField] private ClickInteractor clickInteractor;
        [Header("Movement Handler (Component Reference)")] // <--- ADDED Header
        [Tooltip("Reference to the MinigameMovementManager component for handling movement.")]
        [SerializeField] private MinigameMovementManager movementManager;


        [Header("Animator (Component Reference)")]
        [Tooltip("The PillCraftingAnimator component on this GameObject.")]
        private PillCraftingAnimator pillCraftingAnimator; // Get this in Awake

        private GameObject instantiatedContainer;
        private GameObject instantiatedPrescriptionContainer;
        private GameObject instantiatedPrescriptionContainerLid;

        private HashSet<GameObject> instantiatedPills = new HashSet<GameObject>();
        private HashSet<GameObject> discardedPills = new HashSet<GameObject>();
        private List<GameObject> countedPills = new List<GameObject>();


        private int currentPillCountOnTable = 0;
        private int targetPillCount = 0;

        // Get animator reference in Awake and check UI Handler
        private void Awake()
        {
            pillCraftingAnimator = GetComponent<PillCraftingAnimator>();
            if (pillCraftingAnimator == null)
            {
                Debug.LogError($"PillCraftingMinigame ({gameObject.name}): PillCraftingAnimator component is missing on this GameObject!", this);
                enabled = false; // Disable the script if animator is missing
                return;
            }
            if (uiHandler == null)
            {
                Debug.LogError($"PillCraftingMinigame ({gameObject.name}): MinigameUIHandler reference is missing! UI will not function.", this);
                // We won't disable the script entirely, but log a warning
            }
            if (clickInteractor == null)
            {
                Debug.LogError($"PillCraftingMinigame ({gameObject.name}): ClickInteractor reference is missing! Input will not be handled.", this);
                // We won't disable the script entirely, but log a warning
            }
             if (movementManager == null)
             {
                  Debug.LogError($"PillCraftingMinigame ({gameObject.name}): MinigameMovementManager reference is missing! Object movement and physics control will not function.", this);
             }
         }

        public override void SetupAndStart(CraftingRecipe recipe, int batches)
        {
            if (pillConfig == null)
            {
                Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Pill Config Data ScriptableObject is not assigned! Cannot start minigame.", this);
                minigameSuccessStatus = false;
                EndMinigame(true); // Signal failure/abort due to setup error
                return;
            }
            if (clickInteractor != null)
             {
                  // Initialize the ClickInteractor with the main camera and the pill layer from config
                  // Assuming Camera.main is the correct camera for interaction. Adjust if using a different camera.
                  clickInteractor.Initialize(Camera.main, pillConfig.pillLayer); // <--- ADDED Initialization
             }

            _initialCameraTarget = pouringCameraTarget;
            _initialCameraDuration = pillConfig.cameraMoveDuration;

            minigameSuccessStatus = false;

            instantiatedPills.Clear();
            discardedPills.Clear();
            countedPills.Clear();
            currentPillCountOnTable = 0;
            uiHandler.RuntimeInitialize();

            base.SetupAndStart(recipe, batches); // This calls SetMinigameState(Beginning)
        }

         public override void EndMinigame(bool wasAborted)
         {
             Debug.Log($"PillCraftingMinigame ({gameObject.name}): EndMinigame called. Aborted: {wasAborted}.", this);
             // The base class EndMinigame now calls PerformCleanup() for us
             base.EndMinigame(wasAborted); // This calls PerformCleanup() which includes UI cleanup
         }

         /// <summary>
         /// Implements the abstract method from CraftingMinigameBase to perform specific cleanup.
         /// Handles pooling and calls the UI Handler's cleanup.
         /// </summary>
        protected override void PerformCleanup()
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Performing specific cleanup.", this);

            // --- UPDATED: Unsubscribe from UI Handler event and call its cleanup ---
            if (uiHandler != null)
            {
                 uiHandler.OnFinishButtonClicked -= OnFinishCountingClicked; // Unsubscribe from the event
                 uiHandler.PerformCleanup(); // Tell the UI handler to clean up its listeners etc.
            }
            if (clickInteractor != null)
             {
                  clickInteractor.OnObjectClicked -= HandleObjectClicked; // Unsubscribe from the event
                  clickInteractor.PerformCleanup(); // Tell the input handler to disable interaction etc.
             }
            if (movementManager != null)
             {
                 movementManager.PerformCleanup(); // Stops any ongoing coroutines/tweens managed by it
             }
            CleanupMinigameAssets(); // Pool the actual game objects and clear lists
        }


        // --- Base Class State Overrides ---

        protected override void OnEnterBeginningState()
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Entering Beginning (Pouring) State.", this);

            targetPillCount = UnityEngine.Random.Range(pillConfig.minTargetPills, pillConfig.maxTargetPills + 1);
            targetPillCount = Mathf.Max(1, targetPillCount); // Ensure target is at least 1
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Target Pill Count generated: {targetPillCount}.", this);

            currentPillCountOnTable = 0;

            StartCoroutine(PouringSequence());

            if (uiHandler != null) uiHandler.Hide();
            if (clickInteractor != null) clickInteractor.DisableInteraction();
        }

        protected override void OnExitBeginningState()
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Exiting Beginning (Pouring) State.", this);
             // Cleanup handled by PerformCleanup() via base.EndMinigame()
        }

        protected override void OnEnterMiddleState()
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Entering Middle (Counting) State.", this);

            if (countingCameraTarget != null)
            {
                CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.CinematicView, countingCameraTarget, pillConfig.cameraMoveDuration);
            }
            else Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Counting Camera Target not assigned!", this);

            currentPillCountOnTable = instantiatedPills.Count - discardedPills.Count;

            // --- UPDATED: UI handling via MinigameUIHandler ---
            if (uiHandler != null)
            {
                Debug.Log("PillCraftingMinigame : Showing UI");
                UpdateCountUI(); // Call our helper which now uses uiHandler
                 uiHandler.Show(); // Use the handler's show method
                 uiHandler.OnFinishButtonClicked -= OnFinishCountingClicked;
                 uiHandler.OnFinishButtonClicked += OnFinishCountingClicked;
            }
            else Debug.LogError($"PillCraftingMinigame ({gameObject.name}): UI Handler is null! Cannot manage UI.", this);

            if (clickInteractor != null)
            {
                 // Initialize the interactor if not already (e.g., if Awake order was weird)
                 // It's safer to initialize it in SetupAndStart or Awake if it needs scene refs like Camera.main
                 // Let's put initialization in SetupAndStart.
                 clickInteractor.EnableInteraction(); // Enable interaction
                 // Subscribe to the click event
                 clickInteractor.OnObjectClicked -= HandleObjectClicked; // Ensure no double subscription
                 clickInteractor.OnObjectClicked += HandleObjectClicked;
            }
            else Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Click Interactor is null! Cannot enable input.", this);
        }

        protected override void OnExitMiddleState()
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Exiting Middle (Counting) State.", this);
             // --- UPDATED: UI handling via MinigameUIHandler ---
            if (uiHandler != null)
            {
                uiHandler.Hide(); // Use the handler's hide method
                uiHandler.OnFinishButtonClicked -= OnFinishCountingClicked; // Unsubscribe
            }
            if (clickInteractor != null)
             {
                  clickInteractor.DisableInteraction(); // Disable interaction
                  clickInteractor.OnObjectClicked -= HandleObjectClicked; // Unsubscribe
             }
        }

        protected override void OnEnterEndState()
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Entering End (Packaging) State.", this);

            if (packagingCameraTarget != null)
            {
                CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.CinematicView, packagingCameraTarget, pillConfig.cameraMoveDuration);
            }
            else Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Packaging Camera Target not assigned!", this);

            StartCoroutine(PackagingSequence());
        }

        protected override void OnExitEndState()
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Exiting End state.", this);
             // Cleanup handled by PerformCleanup() via base.EndMinigame()
        }

         protected override void OnEnterNoneState()
         {
              Debug.Log($"PillCraftingMinigame ({gameObject.name}): Entering None state.", this);
         }


        protected override void Update()
        {
            base.Update();
        }

        // --- Pill Minigame Specific Logic ---

        private IEnumerator PouringSequence()
        {
            if (pillConfig == null || pillConfig.containerPrefab == null || pillSpawnPoint == null || pillConfig.pillPrefab == null || PoolingManager.Instance == null || pillCraftingAnimator == null || pouringContainerSpawnPoint == null || stockContainerSetdown == null)
            {
                Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Missing essential references for pouring sequence or animator or spawn point!", this);
                 minigameSuccessStatus = false;
                 EndMinigame(false);
                yield break;
            }

            instantiatedContainer = PoolingManager.Instance.GetPooledObject(pillConfig.containerPrefab);
            if (instantiatedContainer != null)
            {
                instantiatedContainer.transform.SetPositionAndRotation(pouringContainerSpawnPoint.position, pouringContainerSpawnPoint.rotation);
                instantiatedContainer.SetActive(true);
            }
            else
            {
                Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Failed to get container from pool! Signalling internal failure.", this);
                 minigameSuccessStatus = false;
                 EndMinigame(false);
                yield break;
            }

            pillCraftingAnimator.SetRuntimeReferences(instantiatedContainer.transform, null, null);
            yield return new WaitForSeconds(0.25f); // Initial brief wait


            int excessPillsToPour = UnityEngine.Random.Range(pillConfig.minExcessPills, pillConfig.maxExcessPills + 1);
            int totalPillsToSpawn = targetPillCount + excessPillsToPour;
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Spawning {totalPillsToSpawn} pills (Target: {targetPillCount}, Excess: {excessPillsToPour}).", this);

            // Start the pouring animation sequence
             Sequence pouringAnimSequence = pillCraftingAnimator.AnimatePouring();

             if (pouringAnimSequence != null)
             {
                 yield return new WaitForSeconds(0.28f);
                 StartCoroutine(SpawnPillsDuringPour(totalPillsToSpawn, pillConfig.pouringDuration));

                 yield return pouringAnimSequence.WaitForCompletion(); // Wait for the whole animation sequence to finish
                 Debug.Log($"PillCraftingMinigame ({gameObject.name}): Pouring animation sequence finished.");
             }
             else
             {
                 Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Pouring animation sequence failed to start.");
             }


            currentPillCountOnTable = instantiatedPills.Count;
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Pouring sequence finished. Initial pills on table: {currentPillCountOnTable}.");

            if (instantiatedContainer != null && stockContainerSetdown != null)
             {
                 Debug.Log($"PillCraftingMinigame ({gameObject.name}): Moving stock container to setdown position ({stockContainerSetdown.position}).", this);
                Tween setdownTween = instantiatedContainer.transform.DOMove(stockContainerSetdown.position, 0.5f);

                 Debug.Log($"PillCraftingMinigame ({gameObject.name}): Stock container setdown animation finished.");
             }
             else
             {
                 Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Stock container or setdown point is null. Skipping setdown animation.", this);
             }

            SetMinigameState(MinigameState.Middle);
        }

        /// <summary>
        /// Coroutine to spawn pills over a given duration, synchronized with the pouring animation.
        /// </summary>
        private IEnumerator SpawnPillsDuringPour(int totalPillsToSpawn, float spawnDuration)
        {
             if (pillConfig == null || pillConfig.pillPrefab == null || PoolingManager.Instance == null || pillSpawnPoint == null || pillCraftingAnimator == null || movementManager == null)
             {
                  Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Missing references (PillConfig, prefab, pool, points, animator) for spawning pills during pour!", this);
                  yield break;
             }

             if (totalPillsToSpawn > 0 && spawnDuration <= 0)
             {
                 Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Spawn duration is zero or negative but pills need spawning. Spawning instantly.", this);
                 spawnDuration = 0.01f;
             }

             float timePerPill = totalPillsToSpawn > 0 ? spawnDuration / totalPillsToSpawn : 0;

             for (int i = 0; i < totalPillsToSpawn; i++)
             {
                 if (currentMinigameState == MinigameState.None || currentMinigameState == MinigameState.End)
                 {
                      Debug.Log($"PillCraftingMinigame ({gameObject.name}): Aborting pill spawn coroutine due to minigame ending.");
                      yield break;
                 }

                 GameObject pillGO = PoolingManager.Instance.GetPooledObject(pillConfig.pillPrefab);

                 if (pillGO != null)
                 {
                     Transform spawnPoint = pillCraftingAnimator?.pillStockPourPoint != null ? pillCraftingAnimator.pillStockPourPoint : pillSpawnPoint;
                     if (spawnPoint == null)
                     {
                         Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Pill spawn point is null after checks!", this);
                         PoolingManager.Instance.ReturnPooledObject(pillGO);
                         yield break;
                     }

                     pillGO.transform.SetPositionAndRotation(spawnPoint.position, Quaternion.identity);
                     pillGO.SetActive(true);
                     instantiatedPills.Add(pillGO);

                     movementManager.SetPhysicsState(pillGO, false);
                    movementManager.StartPhysicsActivationDelay(pillGO, UnityEngine.Random.Range(pillConfig.minPhysicsEnableDelay, pillConfig.maxPhysicsEnableDelay));
                 }
                 else
                 {
                     Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Failed to get pill {i} from pool during pour! Aborting spawning.", this);
                     yield break;
                 }

                 if (timePerPill > 0)
                 {
                     yield return new WaitForSeconds(timePerPill);
                 }
             }
             Debug.Log($"PillCraftingMinigame ({gameObject.name}): Finished starting pill spawns.");
        }

        private void HandleObjectClicked(GameObject clickedObject)
        {
             // This method is ONLY subscribed and enabled during the Middle (Counting) state.
             // No need for a state check here, but doesn't hurt for robustness.
             if (currentMinigameState != MinigameState.Middle)
             {
                  Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Received click from interactor outside Middle state. Ignoring.", clickedObject);
                  return;
             }

            // Check if the clicked object is one of the instantiated pills and has a Rigidbody
            // (Rigidbody check remains for now as Discard/Reclaim use it)
            if (instantiatedPills.Contains(clickedObject) && clickedObject.GetComponent<Rigidbody>() != null)
            {
                if (!discardedPills.Contains(clickedObject))
                {
                    // Pill is currently on the table (not discarded), so discard it
                    DiscardPill(clickedObject);
                }
                else
                {
                    // Pill is currently discarded, so reclaim it
                    ReclaimPill(clickedObject);
                }
            }
             else if (instantiatedPills.Contains(clickedObject) && clickedObject.GetComponent<Rigidbody>() == null)
             {
                 Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Clicked object '{clickedObject.name}' is a pill, but missing Rigidbody. Cannot process click.", clickedObject);
             }
             else
             {
                 // Log if it's on the correct layer but not in our instantiated list.
                 // The ClickInteractor already filtered by layer, so this is a pill layer object we didn't spawn.
                 Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Clicked object '{clickedObject.name}' is on the pill layer but not in instantiatedPills list. Ignoring.", clickedObject);
             }
        }

        private void DiscardPill(GameObject pillGO)
        {
            if (!discardedPills.Contains(pillGO))
            {
                discardedPills.Add(pillGO);
                currentPillCountOnTable--;
                UpdateCountUI();

                Rigidbody rb = pillGO.GetComponent<Rigidbody>();
                Collider col = pillGO.GetComponent<Collider>();

                if (movementManager != null)
                {
                     // Set physics state to disabled (kinematic/collider off) for the move
                     // movementManager.SetPhysicsState(pillGO, false); // This is handled inside StartSmoothMove if disablePhysicsDuringMove is true

                     // Start the smooth move. Physics will remain disabled after the move completes (as per MoveCoroutine logic).
                    movementManager.StartSmoothMove(
                        pillGO,
                        pillDiscardPoint.position,
                        0.5f, // Duration (Could be in config?)
                        useRigidbody: true, // Use RB for movement
                        disablePhysicsDuringMove: true, // Disable physics/collider during the move
                        onComplete: () =>
                        {
                            if (movementManager != null && pillGO != null) // Defensive check
                            {
                                movementManager.SetPhysicsState(pillGO, true); // Set non-kinematic, gravity on, collider on
                                                                               // Add a small force upwards or outwards to make it settle realistically
                                                                               // rb might be null here if the GO was destroyed/pooled mid-move, check again
                                Rigidbody completedRB = pillGO.GetComponent<Rigidbody>();
                                if (completedRB != null)
                                {
                                    completedRB.AddForce(Vector3.up * 0.1f, ForceMode.VelocityChange); // Example nudge
                                                                                                       // Add a small random horizontal force?
                                }
                            }
                        }
                    );
                }
                else
                {
                     Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Movement Manager is null! Cannot discard pill movement.", this);
                     // Fallback: Just snap the position?
                     if(pillGO != null) pillGO.transform.position = pillDiscardPoint.position;
                }
            }
            else
            {
                Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Attempted to discard pill {pillGO.name} but it was already in the discarded list.", pillGO);
            }
        }

        private void ReclaimPill(GameObject pillGO)
        {
            if (discardedPills.Contains(pillGO))
            {
                discardedPills.Remove(pillGO);
                currentPillCountOnTable++;
                UpdateCountUI();

                Rigidbody rb = pillGO.GetComponent<Rigidbody>(); // Still needed for logic, but move handled externally
                Collider col = pillGO.GetComponent<Collider>();

                 // --- UPDATED: Use movementManager for Reclaim move and re-enable physics ---
                 if (movementManager != null)
                 {
                     // Set physics state to disabled (kinematic/collider off) for the move
                     // movementManager.SetPhysicsState(pillGO, false); // This is handled inside StartSmoothMove if disablePhysicsDuringMove is true

                     Vector3 targetReclaimPos = (pillReclaimPoint != null) ? pillReclaimPoint.position : pillSpawnPoint.position + UnityEngine.Random.insideUnitSphere * pillConfig.packagingScatterRadius;

                     // Start the smooth move. Use a callback to re-enable physics after the move completes.
                     movementManager.StartSmoothMove(
                         pillGO,
                         targetReclaimPos,
                         0.5f, // Duration (Could be in config?)
                         useRigidbody: true, // Use RB for movement
                         disablePhysicsDuringMove: true, // Disable physics/collider during the move
                         onComplete: () => { // Callback after move finishes
                             // Re-enable physics using the MovementManager's helper
                              if(movementManager != null && pillGO != null) // Defensive check
                              {
                                 movementManager.SetPhysicsState(pillGO, true); // Set non-kinematic, gravity on, collider on
                                 // Add a small force upwards or outwards to make it settle realistically
                                 // rb might be null here if the GO was destroyed/pooled mid-move, check again
                                 Rigidbody completedRB = pillGO.GetComponent<Rigidbody>();
                                 if (completedRB != null)
                                 {
                                     completedRB.AddForce(Vector3.up * 0.1f, ForceMode.VelocityChange); // Example nudge
                                      // Add a small random horizontal force?
                                 }
                              }
                         }
                     );
                 }
                 else
                 {
                     Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Movement Manager is null! Cannot reclaim pill movement.", this);
                     // Fallback: Just snap and set physics state?
                     if(pillGO != null)
                     {
                         Vector3 targetReclaimPos = (pillReclaimPoint != null) ? pillReclaimPoint.position : pillSpawnPoint.position + UnityEngine.Random.insideUnitSphere * pillConfig.packagingScatterRadius;
                         pillGO.transform.position = targetReclaimPos;
                         // Also attempt to re-enable physics
                         Rigidbody completedRB = pillGO.GetComponent<Rigidbody>();
                         Collider completedCol = pillGO.GetComponent<Collider>();
                         if (completedRB != null) completedRB.isKinematic = false;
                         if (completedRB != null) completedRB.useGravity = true;
                         if (completedCol != null) completedCol.enabled = true;
                     }
                 }
                Debug.Log($"PillCraftingMinigame ({gameObject.name}): Reclaimed pill {pillGO.name}. Current count on table: {currentPillCountOnTable}", pillGO);
            }
            else
            {
                Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Attempted to reclaim pill {pillGO.name} but it was NOT in the discarded list.", pillGO);
            }
        }

        /// <summary>
        /// Updates the count display and finish button interactability using the MinigameUIHandler.
        /// </summary>
        // --- UPDATED: Use MinigameUIHandler for text update AND button interactability ---
        private void UpdateCountUI()
        {
            if (uiHandler != null)
            {
                uiHandler.UpdateText($"Pills: {currentPillCountOnTable}/{targetPillCount}");
                 uiHandler.SetFinishButtonInteractable(currentPillCountOnTable == targetPillCount); 
            }
        }


        /// <summary>
        /// Handles the logic when the Finish Counting button is clicked via the UI Handler event.
        /// </summary>
        public void OnFinishCountingClicked() // This method is now an event handler
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Finish Counting button clicked (via UI Handler event).", this);
            // Only proceed if currently in the Counting state
            if (currentMinigameState == MinigameState.Middle)
            {
                 // Check if count is correct *before* transitioning
                if (currentPillCountOnTable == targetPillCount)
                {
                    Debug.Log($"PillCraftingMinigame ({gameObject.name}): Count is correct ({currentPillCountOnTable}/{targetPillCount}). Transitioning to Packaging.", this);

                    // Collect only the pills NOT in the discarded pile
                    countedPills.Clear();
                    foreach(GameObject pillGO in instantiatedPills)
                    {
                        if (pillGO != null && !discardedPills.Contains(pillGO))
                        {
                             countedPills.Add(pillGO);
                        }
                    }

                    if (countedPills.Count != targetPillCount)
                    {
                        Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Mismatch! Collected {countedPills.Count} pills but target was {targetPillCount}. This should not happen if logic is correct. Signalling internal failure.", this);
                        minigameSuccessStatus = false; // Ensure failure if collection logic was wrong
                    }
                    else
                    {
                         minigameSuccessStatus = true; // Success!
                    }

                    SetMinigameState(MinigameState.End); // Transition to packaging
                }
                else
                {
                    Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Finish Counting button clicked, but count is incorrect. Count: {currentPillCountOnTable}, Target: {targetPillCount}.", this);
                    minigameSuccessStatus = false; // Explicitly set failure
                     SetMinigameState(MinigameState.End); // Still transition to End state (failure)
                }
            }
             else
             {
                  // Button was clicked when not in the Middle state - ignore or log
                  Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Finish Counting button clicked when not in the Counting state ({currentMinigameState}). Ignoring.", this);
             }
        }

        private IEnumerator PackagingSequence()
        {
            if (pillConfig == null || pillConfig.prescriptionContainerPrefab == null || pillConfig.prescriptionContainerLidPrefab == null || prescriptionContainerSpawnPoint == null || prescriptionContainerLidSpawnPoint == null || PoolingManager.Instance == null || pillCraftingAnimator == null)
            {
                Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Missing essential references (PillConfig, prefabs, points, pool, animator) for packaging sequence! Signalling internal failure.", this);
                 minigameSuccessStatus = false;
                 EndMinigame(false);
                yield break;
            }

            instantiatedPrescriptionContainer = PoolingManager.Instance.GetPooledObject(pillConfig.prescriptionContainerPrefab);

            if (instantiatedPrescriptionContainer != null)
            {
                instantiatedPrescriptionContainer.transform.SetPositionAndRotation(prescriptionContainerSpawnPoint.position, prescriptionContainerSpawnPoint.rotation);
                instantiatedPrescriptionContainer.SetActive(true);
                 Debug.Log($"PillCraftingMinigame ({gameObject.name}) PackagingSequence: Prescription container '{instantiatedPrescriptionContainer.name}' active at {instantiatedPrescriptionContainer.transform.position}.", instantiatedPrescriptionContainer);
            }
            else
            {
                Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Failed to get prescription container from pool! Signalling internal failure.", this);
                minigameSuccessStatus = false;
                EndMinigame(false);
                yield break;
            }

            instantiatedPrescriptionContainerLid = PoolingManager.Instance.GetPooledObject(pillConfig.prescriptionContainerLidPrefab);

            if (instantiatedPrescriptionContainerLid != null)
            {
                instantiatedPrescriptionContainerLid.transform.SetPositionAndRotation(prescriptionContainerLidSpawnPoint.position, prescriptionContainerLidSpawnPoint.rotation);
                instantiatedPrescriptionContainerLid.transform.localScale = pillConfig.prescriptionContainerLidScale;
                instantiatedPrescriptionContainerLid.SetActive(true);
                 Debug.Log($"PillCraftingMinigame ({gameObject.name}) PackagingSequence: Prescription container Lid '{instantiatedPrescriptionContainerLid.name}' active at {instantiatedPrescriptionContainerLid.transform.position}.", instantiatedPrescriptionContainerLid);
            }
            else
            {
                 Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Failed to get prescription container LID from pool! Cannot animate packaging visually. Signalling internal failure.", this);
                 if(instantiatedPrescriptionContainer != null) PoolingManager.Instance.ReturnPooledObject(instantiatedPrescriptionContainer);
                 instantiatedPrescriptionContainer = null;

                 minigameSuccessStatus = false;
                 EndMinigame(false);
                 yield break;
            }

            pillCraftingAnimator.SetRuntimeReferences(null, instantiatedPrescriptionContainer.transform, instantiatedPrescriptionContainerLid.transform);
             pillCraftingAnimator.SetCountedPillsCount(countedPills.Count);

             List<Vector3> scatteredPositions = new List<Vector3>();
             Transform pillDropPointSource = pillCraftingAnimator?.prescriptionPillDropPoint != null ? pillCraftingAnimator.prescriptionPillDropPoint : instantiatedPrescriptionContainer.transform;
             Vector3 containerCenter = pillDropPointSource.position;

             foreach (var pillGO in countedPills)
             {
                 if (pillGO != null)
                 {
                      scatteredPositions.Add(containerCenter + UnityEngine.Random.insideUnitSphere * pillConfig.packagingScatterRadius);
                 }
                 else
                 {
                      Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Null pill found in countedPills list during packaging setup.", this);
                 }
             }

            Sequence packagingAnimSequence = pillCraftingAnimator.AnimatePackaging();

             if (packagingAnimSequence != null)
             {
                 yield return new WaitForSeconds(pillConfig.lidAnimateDuration * 0.3f);

                 if (currentMinigameState != MinigameState.None)
                 {
                      StartCoroutine(AnimatePillsIntoContainerCoroutine(countedPills, scatteredPositions, pillConfig.pillPackageAnimateDuration));
                 }

                 yield return packagingAnimSequence.WaitForCompletion();
                 Debug.Log($"PillCraftingMinigame ({gameObject.name}): Packaging animation sequence finished.");
             }
             else
             {
                  Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Packaging animation sequence failed to start. Proceeding without animation sync.");
                  yield return new WaitForSeconds(1.0f);
             }

            yield return new WaitForSeconds(0.2f);

            EndMinigame(false);

            Debug.Log($"PillCraftingMinigame ({gameObject.name}) PackagingSequence: Coroutine finished.");
        }

         private IEnumerator AnimatePillsIntoContainerCoroutine(List<GameObject> pills, List<Vector3> targetPositions, float durationPerPill)
         {
              if (pills == null || targetPositions == null || pills.Count != targetPositions.Count || pillCraftingAnimator == null)
              {
                   Debug.LogError($"PillCraftingMinigame ({gameObject.name}): Invalid data for AnimatePillsIntoContainerCoroutine!", this);
                   yield break;
              }

              float stagger = 0.05f;

              for (int i = 0; i < pills.Count; i++)
              {
                   if (currentMinigameState == MinigameState.None || currentMinigameState == MinigameState.Middle)
                   {
                        Debug.Log($"PillCraftingMinigame ({gameObject.name}): Aborting pill packaging animation coroutine due to minigame ending.");
                        yield break;
                   }

                   GameObject pillGO = pills[i];
                   if (pillGO == null)
                   {
                        Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Null pill found in list at index {i} during packaging animation.", this);
                        continue;
                   }
                   Vector3 targetPos = targetPositions[i];

                   if (pillCraftingAnimator != null)
                   {
                        pillCraftingAnimator.AnimatePillIntoContainer(pillGO, targetPos);
                   }
                   else Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Animator is null during packaging animation for pill {pillGO.name}.", this);

                   yield return new WaitForSeconds(stagger);
              }
              Debug.Log($"PillCraftingMinigame ({gameObject.name}): Finished starting pill packaging animations.");
         }


        private void CleanupMinigameAssets()
        {
            Debug.Log($"PillCraftingMinigame ({gameObject.name}): Cleaning up instantiated (pooled) assets.", this);

            if (instantiatedContainer != null)
            {
                PoolingManager.Instance.ReturnPooledObject(instantiatedContainer);
                instantiatedContainer = null;
            }
             if (instantiatedPrescriptionContainer != null)
             {
                 PoolingManager.Instance.ReturnPooledObject(instantiatedPrescriptionContainer);
                 instantiatedPrescriptionContainer = null;
             }
             if (instantiatedPrescriptionContainerLid != null)
             {
                 PoolingManager.Instance.ReturnPooledObject(instantiatedPrescriptionContainerLid);
                 instantiatedPrescriptionContainerLid = null;
             }

            foreach (GameObject pillGO in instantiatedPills.ToList())
            {
                if (pillGO != null && pillGO.activeInHierarchy)
                {
                     if (movementManager != null)
                     {
                         movementManager.SetPhysicsState(pillGO, false); // Set kinematic=true, gravity=false, collider=false
                     }
                     else
                     {
                         // Fallback if movement manager is missing
                         Rigidbody rb = pillGO.GetComponent<Rigidbody>();
                         if (rb != null)
                         {
                              rb.isKinematic = true;
                              rb.useGravity = true; // Restore default gravity state (assuming pool expects this)
                              rb.linearVelocity = Vector3.zero;
                              rb.angularVelocity = Vector3.zero;
                         }
                         Collider col = pillGO.GetComponent<Collider>();
                         if (col != null) col.enabled = false;
                     }

                    PoolingManager.Instance.ReturnPooledObject(pillGO);
                }
                 else if (pillGO == null)
                 {
                     Debug.LogWarning($"PillCraftingMinigame ({gameObject.name}): Null GameObject found in instantiatedPills during cleanup.", this);
                 }
            }
            instantiatedPills.Clear();
            discardedPills.Clear();
            countedPills.Clear();
            currentPillCountOnTable = 0; // Reset count
        }
    }
}