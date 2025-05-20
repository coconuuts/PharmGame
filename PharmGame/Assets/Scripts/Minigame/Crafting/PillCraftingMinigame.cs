// Systems/CraftingMinigames/PillCraftingMinigame.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Systems.Inventory;
using TMPro;
using UnityEngine.UI;
using Systems.CameraControl;
using Utils.Pooling;
using System.Linq;
using DG.Tweening; // Needed for DOTween Sequence/Tween
using Systems.Minigame.Animation; // Needed for PillCraftingAnimator


namespace Systems.CraftingMinigames
{
    /// <summary>
    /// Specific implementation of the Crafting Minigame for creating pills.
    /// Manages game state (Pouring, Counting, Packaging), handles player input for counting,
    /// and delegates visual animations to PillCraftingAnimator.
    /// </summary>
    public class PillCraftingMinigame : CraftingMinigameBase
    {
        [Header("Pill Minigame References")]
        [Tooltip("Prefab for the large container that pours pills.")]
        [SerializeField] private GameObject containerPrefab;
        [Tooltip("Prefab for a single pill asset.")]
        [SerializeField] private GameObject pillPrefab;
        [Tooltip("Prefab for the finished prescription container.")]
        [SerializeField] private GameObject prescriptionContainerPrefab;

        // --- MODIFIED: Added Lid Prefab ---
        [Tooltip("Prefab for the lid of the finished prescription container.")]
        [SerializeField] private GameObject prescriptionContainerLidPrefab;

        [Tooltip("The desired local scale for the instantiated Prescription Container Lid.")]
         [SerializeField] private Vector3 prescriptionContainerLidScale = Vector3.one;
        // ----------------------------------


        [Tooltip("Transform representing where the camera should be during pouring (Initial).")]
        [SerializeField] private Transform pouringCameraTarget;
        [Tooltip("Transform representing where the camera should be during counting.")]
        [SerializeField] private Transform countingCameraTarget;
        [Tooltip("Transform representing where the camera should be during packaging.")]
        [SerializeField] private Transform packagingCameraTarget;

        [Tooltip("Transform where the Pill Stock Container should be instantiated.")]
        [SerializeField] private Transform pouringContainerSpawnPoint;
        [Tooltip("Transform where the Prescription Container should be instantiated.")]
        [SerializeField] private Transform prescriptionContainerSpawnPoint;
        [Tooltip("Transform where the Prescription Container Lid should be instantiated.")]
        [SerializeField] private Transform prescriptionContainerLidSpawnPoint;
        [Tooltip("Transform where the Pill Stock Container should move after pouring.")]
        [SerializeField] private Transform stockContainerSetdown;


        [Tooltip("Transform representing the point where pills are logically poured from (Fallback).")]
        [SerializeField] private Transform pillSpawnPoint; // Fallback if animator point is missing

        [Tooltip("Transform representing the point where discarded pills are moved.")]
        [SerializeField] private Transform pillDiscardPoint;
        [Tooltip("Transform representing the point where reclaimed pills are moved back to.")]
        [SerializeField] private Transform pillReclaimPoint;
        // --- MODIFIED: Pill Repackage Point might not be needed anymore as animation goes to container ---
        // Let's keep it for now as a fallback or potential additional reference if needed.
        // [Tooltip("Transform representing the point where the prescription container appears (Fallback).")]
        // [SerializeField] private Transform pillRepackagePoint;
        // ------------------------------------------------------------------------------------------------


        [Tooltip("Root GameObject for the Pill Counting UI.")]
        [SerializeField] private GameObject countingUIRoot;
        [Tooltip("UI Text element to display the current pill count.")]
        [SerializeField] private TextMeshProUGUI countText;
        [Tooltip("UI Button to confirm the pill count.")]
        [SerializeField] private Button finishCountingButton;

        [Header("Pill Minigame Settings")]
        [Tooltip("Duration of the pouring animation/sequence.")]
        [SerializeField] private float pouringDuration = 5.0f; // Used for fallback spawn timing
        [Tooltip("Minimum random number of pills to require packaging.")]
        [SerializeField] private int minTargetPills = 10;
        [Tooltip("Maximum random number of pills to require packaging.")]
        [SerializeField] private int maxTargetPills = 20;
        [Tooltip("Duration of the camera movement between states.")]
        [SerializeField] private float cameraMoveDuration = 1.0f;

        [Tooltip("Minimum excess pills to pour (beyond target).")]
        [SerializeField] private int minExcessPills = 5;
        [Tooltip("Maximum excess pills to pour (beyond target).")]
        [SerializeField] private int maxExcessPills = 15;

        [Tooltip("Radius for scattering pills as they enter the container (Used by Animator).")]
        [SerializeField] private float packagingScatterRadius = 0.1f; // Pass this to the animator or let animator have its own setting
        [Tooltip("The LayerMask for detecting clickable pills.")]
        [SerializeField] private LayerMask pillLayer;

         [Tooltip("Minimum delay after spawning a pill before enabling physics to prevent phasing.")]
         [SerializeField] private float minPhysicsEnableDelay = 0.02f;
         [Tooltip("Maximum delay after spawning a pill before enabling physics to prevent phasing.")]
         [SerializeField] private float maxPhysicsEnableDelay = 0.05f;

        // Reference to the specific Animator component
        [Header("Animator")]
        [Tooltip("The PillCraftingAnimator component on this GameObject.")]
        private PillCraftingAnimator pillCraftingAnimator; // Get this in Awake

        private GameObject instantiatedContainer;
        private GameObject instantiatedPrescriptionContainer;
        // --- ADDED: Field for instantiated Lid ---
        private GameObject instantiatedPrescriptionContainerLid;
        // -----------------------------------------

        private HashSet<GameObject> instantiatedPills = new HashSet<GameObject>();
        private HashSet<GameObject> discardedPills = new HashSet<GameObject>();
        private List<GameObject> countedPills = new List<GameObject>();


        private int currentPillCountOnTable = 0;
        private int targetPillCount = 0;

        private object finalMinigameData = null;


        // Get animator reference in Awake
         private void Awake()
         {
             pillCraftingAnimator = GetComponent<PillCraftingAnimator>();
             if (pillCraftingAnimator == null)
             {
                 Debug.LogError($"PillCraftingMinigame ({gameObject.name}): PillCraftingAnimator component is missing on this GameObject!", this);
                 enabled = false;
                 return;
             }
         }

        public override void SetupAndStart(CraftingRecipe recipe, int batches)
        {
            _initialCameraTarget = pouringCameraTarget;
            _initialCameraDuration = cameraMoveDuration;

            minigameSuccessStatus = false;
            finalMinigameData = null;

            instantiatedPills.Clear();
            discardedPills.Clear();
            countedPills.Clear();
            currentPillCountOnTable = 0;

            base.SetupAndStart(recipe, batches);
        }

         public override void EndMinigame(bool wasAborted)
         {
             Debug.Log($"PillCraftingMinigame: EndMinigame called. Aborted: {wasAborted}.", this);
             base.EndMinigame(wasAborted); // This calls PerformCleanup()
         }

         /// <summary>
         /// Implements the abstract method from CraftingMinigameBase to perform specific cleanup.
         /// Calls the animator cleanup and handles pooling.
         /// </summary>
        protected override void PerformCleanup()
        {
            Debug.Log("PillCraftingMinigame: Performing specific cleanup.", this);

            if (pillCraftingAnimator != null)
            {
                // --- MODIFIED: Call cleanup without passing runtime refs, animator should null them ---
                pillCraftingAnimator.PerformAnimatorCleanup(); // This stops all tweens and clears animator refs
                // --------------------------------------------------------------------------------------
            }
            else Debug.LogWarning("PillCraftingMinigame: Animator reference is null during cleanup.", this);

            CleanupMinigameAssets(); // Pool the actual game objects and clear lists
        }


        // --- Base Class State Overrides ---

        protected override void OnEnterBeginningState()
        {
            Debug.Log("PillCraftingMinigame: Entering Beginning (Pouring) State.", this);

            targetPillCount = Random.Range(minTargetPills, maxTargetPills + 1);
            targetPillCount = Mathf.Max(1, targetPillCount);
            Debug.Log($"PillCraftingMinigame: Target Pill Count generated: {targetPillCount}.", this);

            currentPillCountOnTable = 0;

            StartCoroutine(PouringSequence());

            if (countingUIRoot != null) countingUIRoot.SetActive(false);
            if (finishCountingButton != null) finishCountingButton.onClick.RemoveListener(OnFinishCountingClicked);
        }

        protected override void OnExitBeginningState()
        {
            Debug.Log("PillCraftingMinigame: Exiting Beginning (Pouring) State.", this);
             // Ensure the container is cleaned up if we exit this state for any reason other than completing the sequence
             // (e.g. if the minigame is aborted during pouring). PerformCleanup handles this now.
        }

        protected override void OnEnterMiddleState()
        {
            Debug.Log("PillCraftingMinigame: Entering Middle (Counting) State.", this);

            if (countingCameraTarget != null)
            {
                CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.CinematicView, countingCameraTarget, cameraMoveDuration);
            }
            else Debug.LogWarning("PillCraftingMinigame: Counting Camera Target not assigned!", this);

            // --- MODIFIED: Ensure current count reflects actual pills *not* discarded ---
            currentPillCountOnTable = instantiatedPills.Count - discardedPills.Count;
            // -------------------------------------------------------------------------


            UpdateCountUI();
            if (countingUIRoot != null) countingUIRoot.SetActive(true);

            if (finishCountingButton != null)
            {
                finishCountingButton.onClick.AddListener(OnFinishCountingClicked);
                finishCountingButton.interactable = (currentPillCountOnTable == targetPillCount);
            }
            else Debug.LogError("PillCraftingMinigame: Finish Counting Button not assigned!", this);

            EnablePillClicking(); // TODO
        }

        protected override void OnExitMiddleState()
        {
            Debug.Log("PillCraftingMinigame: Exiting Middle (Counting) State.", this);
            if (countingUIRoot != null) countingUIRoot.SetActive(false);
            if (finishCountingButton != null) finishCountingButton.onClick.RemoveListener(OnFinishCountingClicked);
            DisablePillClicking(); // TODO
        }

        protected override void OnEnterEndState()
        {
            Debug.Log("PillCraftingMinigame: Entering End (Packaging) State.", this);

            if (packagingCameraTarget != null)
            {
                CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.CinematicView, packagingCameraTarget, cameraMoveDuration);
            }
            else Debug.LogWarning("PillCraftingMinigame: Packaging Camera Target not assigned!", this);

            StartCoroutine(PackagingSequence());
        }

        protected override void OnExitEndState()
        {
            Debug.Log("PillCraftingMinigame: Exiting End state.", this);
             // Ensure packaging assets are cleaned up if we exit this state for any reason
             // (e.g. if the minigame is aborted during packaging). PerformCleanup handles this now.
        }

         protected override void OnEnterNoneState()
         {
              Debug.Log("PillCraftingMinigame: Entering None state.", this);
         }


        protected override void Update()
        {
            base.Update();

            if (currentMinigameState == MinigameState.Middle)
            {
                HandlePillClickInput();
            }
        }

        // --- Pill Minigame Specific Logic ---

        private IEnumerator PouringSequence()
        {
            if (containerPrefab == null || pillSpawnPoint == null || pillPrefab == null || PoolingManager.Instance == null || pillCraftingAnimator == null || pouringContainerSpawnPoint == null || stockContainerSetdown == null)
            {
                Debug.LogError("PillCraftingMinigame: Missing essential references for pouring sequence or animator or spawn point!", this);
                 minigameSuccessStatus = false;
                 EndMinigame(false);
                yield break;
            }

            // Instantiate container at the designated spawn point
            instantiatedContainer = PoolingManager.Instance.GetPooledObject(containerPrefab);
            if (instantiatedContainer != null)
            {
                // --- MODIFIED: Use the pouringContainerSpawnPoint Transform ---
                instantiatedContainer.transform.SetPositionAndRotation(pouringContainerSpawnPoint.position, pouringContainerSpawnPoint.rotation);
                // -------------------------------------------------------------
                instantiatedContainer.SetActive(true);
            }
            else
            {
                Debug.LogError("PillCraftingMinigame: Failed to get container from pool! Signalling internal failure.", this);
                 minigameSuccessStatus = false;
                 EndMinigame(false);
                yield break;
            }

            pillCraftingAnimator.SetRuntimeReferences(instantiatedContainer.transform, null, null);
            yield return new WaitForSeconds(0.25f); // Initial brief wait


            int excessPillsToPour = Random.Range(minExcessPills, maxExcessPills + 1);
            int totalPillsToSpawn = targetPillCount + excessPillsToPour;
            Debug.Log($"PillCraftingMinigame: Spawning {totalPillsToSpawn} pills (Target: {targetPillCount}, Excess: {excessPillsToPour}).", this);

            // Start the pouring animation sequence
             Sequence pouringAnimSequence = pillCraftingAnimator.AnimatePouring();

             if (pouringAnimSequence != null)
             {
                 yield return new WaitForSeconds(0.28f);
                 StartCoroutine(SpawnPillsDuringPour(totalPillsToSpawn, pouringDuration));

                 yield return pouringAnimSequence.WaitForCompletion(); // Wait for the whole animation sequence to finish
                 Debug.Log("PillCraftingMinigame: Pouring animation sequence finished.");
             }
             else
             {
                 Debug.LogError("PillCraftingMinigame: Pouring animation sequence failed to start.");
             }


            currentPillCountOnTable = instantiatedPills.Count;
            Debug.Log($"PillCraftingMinigame: Pouring sequence finished. Initial pills on table: {currentPillCountOnTable}.");

            if (instantiatedContainer != null && stockContainerSetdown != null)
             {
                 Debug.Log($"PillCraftingMinigame: Moving stock container to setdown position ({stockContainerSetdown.position}).", this);
                // Use DOMove for smooth position animation without Rigidbody
                Tween setdownTween = instantiatedContainer.transform.DOMove(stockContainerSetdown.position, 0.5f);
                                                                     // Example ease

                 Debug.Log("PillCraftingMinigame: Stock container setdown animation finished.");
             }
             else
             {
                 // This should be caught by the initial null check, but defensive logging.
                 Debug.LogWarning("PillCraftingMinigame: Stock container or setdown point is null. Skipping setdown animation.", this);
             }


            SetMinigameState(MinigameState.Middle);
        }

        /// <summary>
        /// Coroutine to spawn pills over a given duration, synchronized with the pouring animation.
        /// </summary>
        private IEnumerator SpawnPillsDuringPour(int totalPillsToSpawn, float spawnDuration)
        {
             if (pillPrefab == null || PoolingManager.Instance == null || pillSpawnPoint == null || pillCraftingAnimator == null)
             {
                  Debug.LogError("PillCraftingMinigame: Missing references for spawning pills during pour!", this);
                  yield break;
             }

             // Ensure spawnDuration is positive if totalPillsToSpawn > 0 to avoid instantaneous loop
             if (totalPillsToSpawn > 0 && spawnDuration <= 0)
             {
                 Debug.LogWarning("PillCraftingMinigame: Spawn duration is zero or negative but pills need spawning. Spawning instantly.", this);
                 spawnDuration = 0.01f; // Use a tiny duration
             }

             float timePerPill = totalPillsToSpawn > 0 ? spawnDuration / totalPillsToSpawn : 0;

             for (int i = 0; i < totalPillsToSpawn; i++)
             {
                 // Check if the minigame has ended externally during the spawn loop
                 if (currentMinigameState == MinigameState.None || currentMinigameState == MinigameState.End)
                 {
                      Debug.Log("PillCraftingMinigame: Aborting pill spawn coroutine due to minigame ending.");
                      yield break; // Exit coroutine if state changes
                 }


                 GameObject pillGO = PoolingManager.Instance.GetPooledObject(pillPrefab);
                 if (pillGO != null)
                 {
                     // Use the Animator's pour point if available, otherwise fall back
                     Transform spawnPoint = pillCraftingAnimator?.pillStockPourPoint != null ? pillCraftingAnimator.pillStockPourPoint : pillSpawnPoint;
                     if (spawnPoint == null)
                     {
                         Debug.LogError("PillCraftingMinigame: Pill spawn point is null after checks!", this);
                         PoolingManager.Instance.ReturnPooledObject(pillGO); // Return the acquired object
                         yield break; // Cannot proceed
                     }

                     pillGO.transform.SetPositionAndRotation(spawnPoint.position, Quaternion.identity);
                     pillGO.SetActive(true);
                     instantiatedPills.Add(pillGO);

                     // Physics and collider are enabled after a delay by ActivatePhysicsAfterDelay coroutine
                     // Ensure they are initially off/kinematic
                     Rigidbody pillRB = pillGO.GetComponent<Rigidbody>();
                     Collider pillCollider = pillGO.GetComponent<Collider>();
                     if (pillRB != null) pillRB.isKinematic = true;
                     if (pillCollider != null) pillCollider.enabled = false;

                     StartCoroutine(ActivatePhysicsAfterDelay(pillRB, pillCollider, UnityEngine.Random.Range(minPhysicsEnableDelay, maxPhysicsEnableDelay)));
                 }
                 else
                 {
                     Debug.LogError($"PillCraftingMinigame: Failed to get pill {i} from pool during pour! Aborting spawning.", this);
                     yield break;
                 }

                 if (timePerPill > 0) // Only yield if there's time between pills
                 {
                     yield return new WaitForSeconds(timePerPill);
                 }
                 // If timePerPill is 0, pills spawn instantly within one frame iteration (or as fast as Unity allows)
             }
             Debug.Log("PillCraftingMinigame: Finished starting pill spawns.");
        }


         private IEnumerator ActivatePhysicsAfterDelay(Rigidbody rb, Collider col, float delay)
         {
             if (rb == null) yield break; // Cannot enable physics without Rigidbody

             // Ensure they are off before the delay
             rb.isKinematic = true;
             if (col != null) col.enabled = false;

             yield return new WaitForSeconds(delay);

             // Re-enable if the GameObject is still active and hasn't been discarded/reclaimed/pooled
             if (rb != null && rb.gameObject.activeInHierarchy)
             {
                rb.isKinematic = false;
                if (col != null) col.enabled = true;
             }
         }


        private void HandlePillClickInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100f, pillLayer))
                {
                    GameObject clickedObject = hit.collider.gameObject;
                    if (instantiatedPills.Contains(clickedObject) && clickedObject.GetComponent<Rigidbody>() != null)
                    {
                        if (!discardedPills.Contains(clickedObject))
                        {
                            DiscardPill(clickedObject);
                        }
                        else
                        {
                            ReclaimPill(clickedObject);
                        }
                        if (finishCountingButton != null)
                        {
                            finishCountingButton.interactable = (currentPillCountOnTable == targetPillCount);
                        }
                    }
                     else if (instantiatedPills.Contains(clickedObject) && clickedObject.GetComponent<Rigidbody>() == null)
                    {
                         Debug.LogWarning($"PillCraftingMinigame: Raycast hit pill '{clickedObject.name}' but it's missing a Rigidbody component. Cannot move via physics.", clickedObject);
                    }
                    else
                    {
                        Debug.LogWarning($"PillCraftingMinigame: Raycast hit an object on the Pill layer ('{clickedObject.name}') but it was not in the instantiatedPills list. Check LayerMask setup.", clickedObject);
                    }
                }
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

                if (rb != null) rb.isKinematic = true;
                if (col != null) col.enabled = false;

                StartCoroutine(MoveGameObjectSmoothly(pillGO, pillDiscardPoint.position, 0.5f, true));

                Debug.Log($"PillCraftingMinigame: Discarded pill {pillGO.name}. Current count on table: {currentPillCountOnTable}", pillGO);
            }
            else
            {
                Debug.LogWarning($"PillCraftingMinigame: Attempted to discard pill {pillGO.name} but it was already in the discarded list.", pillGO);
            }
        }

        private void ReclaimPill(GameObject pillGO)
        {
            if (discardedPills.Contains(pillGO))
            {
                discardedPills.Remove(pillGO);
                currentPillCountOnTable++;
                UpdateCountUI();

                Rigidbody rb = pillGO.GetComponent<Rigidbody>();
                Collider col = pillGO.GetComponent<Collider>();

                 if (rb != null) rb.isKinematic = true;
                 if (col != null) col.enabled = false;

                Vector3 targetReclaimPos = (pillReclaimPoint != null) ? pillReclaimPoint.position : pillSpawnPoint.position + UnityEngine.Random.insideUnitSphere * 0.5f;
                StartCoroutine(MoveGameObjectSmoothly(pillGO, targetReclaimPos, 0.5f, true));

                Debug.Log($"PillCraftingMinigame: Reclaimed pill {pillGO.name}. Current count on table: {currentPillCountOnTable}", pillGO);
            }
            else
            {
                Debug.LogWarning($"PillCraftingMinigame: Attempted to reclaim pill {pillGO.name} but it was NOT in the discarded list.", pillGO);
            }
        }

        private void EnablePillClicking() { /* TODO: Potentially activate a click handler component */ }
        private void DisablePillClicking() { /* TODO: Potentially deactivate a click handler component */ }

        private void UpdateCountUI()
        {
            if (countText != null)
            {
                countText.text = $"Pills: {currentPillCountOnTable}/{targetPillCount}";
            }
        }

        public void OnFinishCountingClicked()
        {
            Debug.Log("PillCraftingMinigame: Finish Counting button clicked.", this);
            if (currentMinigameState == MinigameState.Middle && currentPillCountOnTable == targetPillCount)
            {
                Debug.Log("PillCraftingMinigame: Count is correct. Transitioning to Packaging.", this);

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
                    Debug.LogError($"PillCraftingMinigame: Mismatch! Collected {countedPills.Count} pills but target was {targetPillCount}. This should not happen if logic is correct. Signalling internal failure.", this);
                    // Set success status based on the final check before packaging
                     minigameSuccessStatus = false;
                }
                else
                {
                     minigameSuccessStatus = true;
                }

                SetMinigameState(MinigameState.End);
            }
            else
            {
                Debug.LogWarning("PillCraftingMinigame: Finish Counting button clicked, but count is incorrect or not in Counting state. Count: " + currentPillCountOnTable + ", Target: " + targetPillCount, this);
                // If count is incorrect when button clicked, the minigame fails.
                minigameSuccessStatus = false; // Explicitly set failure
                // Optionally transition to End state anyway to show failure packaging or just EndMinigame(false) directly?
                // The current flow transitions to End state, then EndMinigame is called after the sequence (which might still run visually)
                // Let's stick to the flow: go to End, packaging happens (maybe differently for failure?), then EndMinigame(false) is called.
                 SetMinigameState(MinigameState.End); // Still transition to End state
            }
        }

        private IEnumerator PackagingSequence()
        {
            if (prescriptionContainerPrefab == null || prescriptionContainerSpawnPoint == null || prescriptionContainerLidPrefab == null || prescriptionContainerLidSpawnPoint == null || PoolingManager.Instance == null || pillCraftingAnimator == null)
            {
                Debug.LogError("PillCraftingMinigame: Missing essential references for packaging sequence or animator or spawn points!", this);
                 minigameSuccessStatus = false; // Ensure failure if references are missing
                 EndMinigame(false);
                yield break;
            }

            // Instantiate prescription container at the designated spawn point
            instantiatedPrescriptionContainer = PoolingManager.Instance.GetPooledObject(prescriptionContainerPrefab);
            if (instantiatedPrescriptionContainer != null)
            {
                instantiatedPrescriptionContainer.transform.SetPositionAndRotation(prescriptionContainerSpawnPoint.position, prescriptionContainerSpawnPoint.rotation);
                instantiatedPrescriptionContainer.SetActive(true);
                 Debug.Log($"PackagingSequence: Prescription container '{instantiatedPrescriptionContainer.name}' active at {instantiatedPrescriptionContainer.transform.position}.", instantiatedPrescriptionContainer);
            }
            else
            {
                Debug.LogError("PillCraftingMinigame: Failed to get prescription container from pool! Signalling internal failure.", this);
                minigameSuccessStatus = false;
                EndMinigame(false);
                yield break;
            }

            // --- ADDED: Instantiate the Lid Prefab ---
            instantiatedPrescriptionContainerLid = PoolingManager.Instance.GetPooledObject(prescriptionContainerLidPrefab);
            if (instantiatedPrescriptionContainerLid != null)
            {
                instantiatedPrescriptionContainerLid.transform.SetPositionAndRotation(prescriptionContainerLidSpawnPoint.position, prescriptionContainerLidSpawnPoint.rotation);
                instantiatedPrescriptionContainerLid.transform.localScale = prescriptionContainerLidScale;
                instantiatedPrescriptionContainerLid.SetActive(true);
                 Debug.Log($"PackagingSequence: Prescription container Lid '{instantiatedPrescriptionContainerLid.name}' active at {instantiatedPrescriptionContainerLid.transform.position}.", instantiatedPrescriptionContainerLid);
            }
            else
            {
                 Debug.LogError("PillCraftingMinigame: Failed to get prescription container LID from pool! Cannot animate packaging visually. Signalling internal failure.", this);
                 // Clean up the main container if we failed to get the lid
                 if(instantiatedPrescriptionContainer != null) PoolingManager.Instance.ReturnPooledObject(instantiatedPrescriptionContainer);
                 instantiatedPrescriptionContainer = null;

                 minigameSuccessStatus = false;
                 EndMinigame(false);
                 yield break;
            }
            // ----------------------------------------

            // --- MODIFIED: Pass the instantiated lid transform to the animator ---
            pillCraftingAnimator.SetRuntimeReferences(null, instantiatedPrescriptionContainer.transform, instantiatedPrescriptionContainerLid.transform);
            // -------------------------------------------------------------------

            // Also tell the animator how many pills are being packaged to help sync animation intervals
             pillCraftingAnimator.SetCountedPillsCount(countedPills.Count);

            // Calculate scatter positions for pills BEFORE starting animations
             List<Vector3> scatteredPositions = new List<Vector3>();
             // Use the Animator's pill drop point if available, otherwise fall back to the container transform (with offset)
             Transform pillDropPointSource = pillCraftingAnimator?.prescriptionPillDropPoint != null ? pillCraftingAnimator.prescriptionPillDropPoint : instantiatedPrescriptionContainer.transform;
             Vector3 containerCenter = pillDropPointSource.position; // Use the actual drop point position

             foreach (var pillGO in countedPills)
             {
                 if (pillGO != null)
                 {
                      scatteredPositions.Add(containerCenter + UnityEngine.Random.insideUnitSphere * packagingScatterRadius);
                 }
                 else
                 {
                      // This shouldn't happen if countedPills is populated correctly
                      Debug.LogWarning("PillCraftingMinigame: Null pill found in countedPills list during packaging setup.", this);
                 }
             }


            // Start the packaging animation sequence on the animator
            // This sequence includes opening/closing the lid and has an interval for pills
            Sequence packagingAnimSequence = pillCraftingAnimator.AnimatePackaging();

             if (packagingAnimSequence != null)
             {
                 // Start the coroutine to animate pills into the container in parallel
                 // This happens when the packaging sequence begins (specifically, the interval part).
                 // We need to yield until the lid is open or the sequence reaches the interval.
                 // Let's wait for the lid open duration before starting pill animations.
                 yield return new WaitForSeconds(pillCraftingAnimator.lidAnimateDuration * 0.3f); // Adjust delay if needed

                 // Only animate pills if the minigame hasn't been aborted
                 if (currentMinigameState != MinigameState.None)
                 {
                     StartCoroutine(AnimatePillsIntoContainerCoroutine(countedPills, scatteredPositions, pillCraftingAnimator.pillPackageAnimateDuration));
                 }


                 yield return packagingAnimSequence.WaitForCompletion(); // Wait for the whole packaging sequence to finish
                 Debug.Log("PillCraftingMinigame: Packaging animation sequence finished.");
             }
             else
             {
                  Debug.LogWarning("PillCraftingMinigame: Packaging animation sequence failed to start. Proceeding without animation sync.");
                   // If animation fails, maybe just instantly move pills?
                  // For now, just wait a bit
                  yield return new WaitForSeconds(1.0f);
             }
            // -----------------------------------------------------
            yield return new WaitForSeconds(0.2f);
            // The success status was set in OnFinishCountingClicked().
            // Now call EndMinigame to finalize and trigger the completion event.
            EndMinigame(false); // false because it reached its natural end state

            Debug.Log("PackagingSequence: Coroutine finished.");
        }

         /// <summary>
         /// Coroutine to animate multiple pills into the packaging container using the Animator.
         /// This replaces the physics-based movement for packaging.
         /// </summary>
         private IEnumerator AnimatePillsIntoContainerCoroutine(List<GameObject> pills, List<Vector3> targetPositions, float durationPerPill)
         {
              if (pills == null || targetPositions == null || pills.Count != targetPositions.Count || pillCraftingAnimator == null)
              {
                   Debug.LogError("PillCraftingMinigame: Invalid data for AnimatePillsIntoContainerCoroutine!", this);
                   yield break;
              }

              // Animate pills with a slight stagger
              float stagger = 0.05f; // Small delay between starting each pill animation

              for (int i = 0; i < pills.Count; i++)
              {
                   // Check if the minigame has ended externally during the animation loop
                   if (currentMinigameState == MinigameState.None || currentMinigameState == MinigameState.Middle)
                   {
                        Debug.Log("PillCraftingMinigame: Aborting pill packaging animation coroutine due to minigame ending.");
                        yield break; // Exit coroutine if state changes
                   }

                   GameObject pillGO = pills[i];
                   if (pillGO == null)
                   {
                        Debug.LogWarning($"PillCraftingMinigame: Null pill found in list at index {i} during packaging animation.", this);
                        continue; // Skip null entries
                   }
                   Vector3 targetPos = targetPositions[i]; // Should match index

                   if (pillCraftingAnimator != null)
                   {
                        // Call the animator method for a single pill's movement using DOTween
                        // AnimatePillIntoContainer handles setting kinematic/collider etc.
                        pillCraftingAnimator.AnimatePillIntoContainer(pillGO, targetPos); // Start the tween
                   }
                   else Debug.LogWarning($"PillCraftingMinigame: Animator is null during packaging animation for pill {pillGO.name}.", this);

                   yield return new WaitForSeconds(stagger); // Stagger the start of each pill's animation
              }
              Debug.Log("PillCraftingMinigame: Finished starting pill packaging animations.");

               // This coroutine finishes once all animations are started.
               // The main PackagingSequence coroutine is yielding on the animator's *Sequence* completion.
         }


        /// <summary>
        /// Coroutine to smoothly move a GameObject using Rigidbody.MovePosition.
        /// Makes object kinematic and disables collider during the move.
        /// Re-enables physics and collider after move if requested.
        /// Note: This is used for Discard/Reclaim, NOT for packaging into the container now.
        /// </summary>
        private IEnumerator MoveGameObjectSmoothly(GameObject go, Vector3 targetPos, float duration, bool reEnablePhysicsAfter = false)
        {
            if (go == null)
            {
                Debug.LogWarning("MoveGameObjectSmoothly: GameObject is null, aborting movement.", this);
                yield break;
            }

            Rigidbody rb = go.GetComponent<Rigidbody>();
            Collider col = go.GetComponent<Collider>();

            if (rb == null)
            {
                Debug.LogError($"MoveGameObjectSmoothly: GameObject '{go.name}' is missing a Rigidbody component! Cannot use physics movement.", go);
                yield break;
            }

            Vector3 startPos = rb.position;
            float timer = 0f;

            rb.isKinematic = true;
            if (col != null) col.enabled = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Transform originalParent = go.transform.parent;
            go.transform.SetParent(null); // Unparent during physics move


            while (timer < duration)
            {
                if (go == null) { yield break; }

                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                rb.MovePosition(Vector3.Lerp(startPos, targetPos, t));
                yield return null;
            }

            if (go != null)
            {
                 rb.MovePosition(targetPos);
                 go.transform.SetParent(originalParent); // Restore original parent
            }
             else yield break;


            if (reEnablePhysicsAfter)
            {
                 if (rb != null) rb.isKinematic = false;
                 if (col != null) col.enabled = true;
            }
             else
             {
                 if (rb != null) rb.isKinematic = true;
                 if (col != null) col.enabled = false;
             }
        }

         /// <summary>
         /// This contains the logic that cleans up the specific assets for THIS minigame.
         /// It is called by the base class's EndMinigame(bool) method via PerformCleanup().
         /// Contains pooling logic and list clearing.
         /// </summary>
        private void CleanupMinigameAssets()
        {
            Debug.Log("PillCraftingMinigame: Cleaning up instantiated (pooled) assets.", this);

            // --- Pool the main container and the separate lid ---
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
            // --------------------------------------------------


            // Pool all pills that were instantiated
            foreach (GameObject pillGO in instantiatedPills.ToList()) // Use ToList() to avoid modifying collection while iterating
            {
                if (pillGO != null && pillGO.activeInHierarchy) // Check if not null and still active
                {
                     // Ensure kinematic/collider off before returning to pool
                     Rigidbody rb = pillGO.GetComponent<Rigidbody>();
                     if (rb != null)
                     {
                          rb.isKinematic = true;
                          rb.useGravity = true; // Restore default gravity state
                     }
                     Collider col = pillGO.GetComponent<Collider>();
                     if (col != null) col.enabled = false;

                    PoolingManager.Instance.ReturnPooledObject(pillGO);
                }
                 else if (pillGO == null)
                 {
                     // Should not happen if list is managed correctly, but defensive check
                     Debug.LogWarning("PillCraftingMinigame: Null GameObject found in instantiatedPills during cleanup.", this);
                 }
                 // If not activeInHierarchy, it might have already been returned by something else (less likely with pooling)
            }
            instantiatedPills.Clear();
            discardedPills.Clear();
            countedPills.Clear();
            currentPillCountOnTable = 0; // Reset count

            if (countingUIRoot != null) countingUIRoot.SetActive(false);
            if (finishCountingButton != null) finishCountingButton.onClick.RemoveListener(OnFinishCountingClicked);
             // Ensure no ongoing coroutines like MoveGameObjectSmoothly are left dangling
             // This might require tracking coroutines or having a mechanism in MoveGameObjectSmoothly
             // to check if the target GO is still valid. (Added checks in MoveGameObjectSmoothly).
        }
    }
}