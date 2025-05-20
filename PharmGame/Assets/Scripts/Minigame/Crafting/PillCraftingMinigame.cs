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

namespace Systems.CraftingMinigames
{
    public class PillCraftingMinigame : CraftingMinigameBase
    {
        [Header("Pill Minigame References")]
        [Tooltip("Prefab for the large container that pours pills.")]
        [SerializeField] private GameObject containerPrefab;
        [Tooltip("Prefab for a single pill asset.")]
        [SerializeField] private GameObject pillPrefab;
        [Tooltip("Prefab for the finished prescription container.")]
        [SerializeField] private GameObject prescriptionContainerPrefab;

        [Tooltip("Transform representing where the camera should be during pouring (Initial).")]
        [SerializeField] private Transform pouringCameraTarget;
        [Tooltip("Transform representing where the camera should be during counting.")]
        [SerializeField] private Transform countingCameraTarget;
        [Tooltip("Transform representing where the camera should be during packaging.")]
        [SerializeField] private Transform packagingCameraTarget;

        [Tooltip("Transform representing the point where pills spawn.")]
        [SerializeField] private Transform pillSpawnPoint;
        [Tooltip("Transform representing the point where discarded pills are moved.")]
        [SerializeField] private Transform pillDiscardPoint;
        [Tooltip("Transform representing the point where reclaimed pills are moved back to.")]
        [SerializeField] private Transform pillReclaimPoint;
        [Tooltip("Transform representing the point where the prescription container appears, and pills are moved into it.")]
        [SerializeField] private Transform pillRepackagePoint; // Changed tooltip

        [Tooltip("Root GameObject for the Pill Counting UI.")]
        [SerializeField] private GameObject countingUIRoot;
        [Tooltip("UI Text element to display the current pill count.")]
        [SerializeField] private TextMeshProUGUI countText;
        [Tooltip("UI Button to confirm the pill count.")]
        [SerializeField] private Button finishCountingButton;

        [Header("Pill Minigame Settings")]
        [Tooltip("Duration of the pouring animation/sequence.")]
        [SerializeField] private float pouringDuration = 5.0f;
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

        [Tooltip("Radius for scattering pills as they enter the container.")]
        [SerializeField] private float packagingScatterRadius = 0.1f;
        [Tooltip("The LayerMask for detecting clickable pills.")]
        [SerializeField] private LayerMask pillLayer;


        private GameObject instantiatedContainer;
        private GameObject instantiatedPrescriptionContainer;
        private List<GameObject> instantiatedPills = new List<GameObject>();
        private List<GameObject> discardedPills = new List<GameObject>();
        private List<GameObject> countedPills = new List<GameObject>();

        private int currentPillCountOnTable = 0;
        private int targetPillCount = 0;

        // The base class CraftingMinigameBase now has minigameSuccessStatus.
        // We don't need a separate finalMinigameResult here unless we pass other data.
        private object finalMinigameData = null; // Stored optional data (not used by this minigame but kept for pattern)


        // Marked as virtual in base class
        public override void SetupAndStart(CraftingRecipe recipe, int batches)
        {
            // Assign the initial camera target and duration from *this specific minigame*
            _initialCameraTarget = pouringCameraTarget;
            _initialCameraDuration = cameraMoveDuration;

            // Initialize result data
            minigameSuccessStatus = false; // Inherited from base class, reset
            finalMinigameData = null;

            // Call the base setup to initialize recipe/batches and transition to Beginning state
            base.SetupAndStart(recipe, batches);
        }

         // Override EndMinigame with the correct signature
         public override void EndMinigame(bool wasAborted)
         {
             Debug.Log($"PillCraftingMinigame: EndMinigame called. Aborted: {wasAborted}.", this);
             // CleanupAssets is now called by base.EndMinigame via PerformCleanup().
             // Call base class EndMinigame. It will handle state transition to None
             // and trigger the completion event with the correct success/abort status.
             base.EndMinigame(wasAborted);
         }

         /// <summary>
         /// Implements the abstract method from CraftingMinigameBase to perform specific cleanup.
         /// --- MODIFIED: Implement the new abstract method ---
         /// </summary>
        protected override void PerformCleanup()
        {
            Debug.Log("PillCraftingMinigame: Performing specific cleanup (returning pooled assets).", this);
            CleanupMinigameAssets(); // Move the asset cleanup logic here
        }


        // --- Base Class State Overrides ---

        protected override void OnEnterBeginningState()
        {
            Debug.Log("PillCraftingMinigame: Entering Beginning (Pouring) State.", this);

            targetPillCount = Random.Range(minTargetPills, maxTargetPills + 1);
            targetPillCount = Mathf.Max(1, targetPillCount);
            Debug.Log($"PillCraftingMinigame: Target Pill Count generated: {targetPillCount}.", this);

            instantiatedPills.Clear();
            discardedPills.Clear();
            countedPills.Clear();
            currentPillCountOnTable = 0;

            StartCoroutine(PouringSequence());

            if (countingUIRoot != null) countingUIRoot.SetActive(false);
            if (finishCountingButton != null) finishCountingButton.onClick.RemoveListener(OnFinishCountingClicked);
        }

        protected override void OnExitBeginningState()
        {
            Debug.Log("PillCraftingMinigame: Exiting Beginning (Pouring) State.", this);
            // Asset cleanup moved to PerformCleanup()
            if (instantiatedContainer != null)
            {
                // Only return the container here if it's specific to the pouring phase's exit
                // If it persists into other states, return it in PerformCleanup().
                // Assuming the pouring container is only used in the Beginning state visual.
                PoolingManager.Instance.ReturnPooledObject(instantiatedContainer);
                instantiatedContainer = null;
            }
        }

        protected override void OnEnterMiddleState()
        {
            Debug.Log("PillCraftingMinigame: Entering Middle (Counting) State.", this);

            // Camera setting for state transition *within* the minigame
            if (countingCameraTarget != null)
            {
                CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.CinematicView, countingCameraTarget, cameraMoveDuration);
            }
            else Debug.LogWarning("PillCraftingMinigame: Counting Camera Target not assigned!", this);

            currentPillCountOnTable = instantiatedPills.Count; // Initial count is all pills poured

            UpdateCountUI();
            if (countingUIRoot != null) countingUIRoot.SetActive(true);

            if (finishCountingButton != null)
            {
                finishCountingButton.onClick.AddListener(OnFinishCountingClicked);
                finishCountingButton.interactable = (currentPillCountOnTable == targetPillCount);
            }
            else Debug.LogError("PillCraftingMinigame: Finish Counting Button not assigned!", this);

            EnablePillClicking();
        }

        protected override void OnExitMiddleState()
        {
            Debug.Log("PillCraftingMinigame: Exiting Middle (Counting) State.", this);
            if (countingUIRoot != null) countingUIRoot.SetActive(false);
            if (finishCountingButton != null) finishCountingButton.onClick.RemoveListener(OnFinishCountingClicked);
            DisablePillClicking();
        }

        protected override void OnEnterEndState()
        {
            Debug.Log("PillCraftingMinigame: Entering End (Packaging) State.", this);

            // Camera setting for state transition *within* the minigame
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
            // Asset cleanup moved to PerformCleanup()
            // if (instantiatedPrescriptionContainer != null) { ... } // This should be pooled in PerformCleanup
        }

         protected override void OnEnterNoneState() // Implement base class virtual method
         {
              Debug.Log("PillCraftingMinigame: Entering None state.", this);
         }

         /// <summary>
         /// This contains the logic that cleans up the specific assets for THIS minigame.
         /// It is called by the base class's EndMinigame(bool) method via PerformCleanup().
         /// --- MOVED logic from OnExitEndState and CleanupMinigameAssets method here ---
         /// </summary>
        private void CleanupMinigameAssets()
        {
            Debug.Log("PillCraftingMinigame: Cleaning up instantiated (pooled) assets.", this);

            // Clean up assets that persist across states (pills, prescription container)
            if (instantiatedContainer != null) // Pouring container cleanup kept in OnExitBeginningState
            {
                PoolingManager.Instance.ReturnPooledObject(instantiatedContainer);
                instantiatedContainer = null;
            }
             if (instantiatedPrescriptionContainer != null)
             {
                 PoolingManager.Instance.ReturnPooledObject(instantiatedPrescriptionContainer);
                 instantiatedPrescriptionContainer = null;
             }


            foreach (GameObject pillGO in instantiatedPills.ToList()) // Use ToList() because we modify the list inside loop
            {
                if (pillGO != null)
                {
                     Rigidbody rb = pillGO.GetComponent<Rigidbody>();
                     if (rb != null) rb.isKinematic = true;
                     Collider col = pillGO.GetComponent<Collider>();
                     if (col != null) col.enabled = false;

                    PoolingManager.Instance.ReturnPooledObject(pillGO);
                }
            }
            instantiatedPills.Clear();
            discardedPills.Clear();
            countedPills.Clear();

            // Clean up UI state
            if (countingUIRoot != null) countingUIRoot.SetActive(false);
            if (finishCountingButton != null) finishCountingButton.onClick.RemoveListener(OnFinishCountingClicked); // Ensure listener is removed
        }


        protected override void Update()
        {
            base.Update();

            if (currentMinigameState == MinigameState.Middle)
            {
                HandlePillClickInput();
            }
        }

        // --- Pill Minigame Specific Logic --- (Coroutines now call MarkMinigameCompleted or base.EndMinigame)

        private IEnumerator PouringSequence()
        {
            if (containerPrefab == null || pillSpawnPoint == null || pillPrefab == null || PoolingManager.Instance == null)
            {
                Debug.LogError("PillCraftingMinigame: Missing essential references for pouring sequence! Signalling internal failure.", this);
                 minigameSuccessStatus = false; // Inherited from base
                 EndMinigame(false); // Call base EndMinigame (it wasn't aborted externally)
                yield break;
            }

            instantiatedContainer = PoolingManager.Instance.GetPooledObject(containerPrefab);
            if (instantiatedContainer != null)
            {
                instantiatedContainer.transform.SetPositionAndRotation(transform.position, transform.rotation);
                instantiatedContainer.SetActive(true);
            }
            else
            {
                Debug.LogError("PillCraftingMinigame: Failed to get container from pool! Signalling internal failure.", this);
                 minigameSuccessStatus = false; // Inherited from base
                 EndMinigame(false); // Call base EndMinigame (it wasn't aborted externally)
                yield break;
            }

            yield return new WaitForSeconds(0.5f);

            int excessPillsToPour = Random.Range(minExcessPills, maxExcessPills + 1);
            int totalPillsToSpawn = targetPillCount + excessPillsToPour;
            Debug.Log($"PillCraftingMinigame: Spawning {totalPillsToSpawn} pills (Target: {targetPillCount}, Excess: {excessPillsToPour}).", this);

            for (int i = 0; i < totalPillsToSpawn; i++)
            {
                GameObject pillGO = PoolingManager.Instance.GetPooledObject(pillPrefab);
                if (pillGO != null)
                {
                    pillGO.transform.SetPositionAndRotation(pillSpawnPoint.position, Quaternion.identity);
                    pillGO.SetActive(true);
                    instantiatedPills.Add(pillGO);

                    Collider pillCollider = pillGO.GetComponent<Collider>();
                    if (pillCollider != null) pillCollider.enabled = true;
                    Rigidbody rb = pillGO.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = false;

                }
                else
                {
                    Debug.LogError($"PillCraftingMinigame: Failed to get pill {i} from pool! Aborting minigame.", this);
                     minigameSuccessStatus = false; // Inherited from base
                     EndMinigame(false); // Call base EndMinigame (it wasn't aborted externally)
                    yield break;
                }

                yield return new WaitForSeconds(pouringDuration / totalPillsToSpawn);
            }

            yield return new WaitForSeconds(pouringDuration * 0.5f);

            SetMinigameState(MinigameState.Middle);
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

        private void EnablePillClicking() { /* TODO */ }
        private void DisablePillClicking() { /* TODO */ }

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

                countedPills = instantiatedPills.Where(pill => !discardedPills.Contains(pill)).ToList();

                if (countedPills.Count != targetPillCount)
                {
                    Debug.LogError($"PillCraftingMinigame: Mismatch! Collected {countedPills.Count} pills but target was {targetPillCount}. Signalling internal failure.", this);
                     minigameSuccessStatus = false; // Inherited from base
                }
                else
                {
                     minigameSuccessStatus = true; // Inherited from base
                }

                SetMinigameState(MinigameState.End); // Transition to End state
            }
            else
            {
                Debug.LogWarning("PillCraftingMinigame: Finish Counting button clicked, but count is incorrect or not in Counting state. Aborting transition.", this);
            }
        }

        private IEnumerator PackagingSequence()
        {
            Debug.Log("PackagingSequence: Starting.", this);
            if (prescriptionContainerPrefab == null || pillRepackagePoint == null || PoolingManager.Instance == null)
            {
                Debug.LogError("PillCraftingMinigame: Missing references for packaging sequence! Signalling internal failure.", this);
                 minigameSuccessStatus = false; // Inherited from base
                 EndMinigame(false); // Call base EndMinigame (it wasn't aborted externally)
                yield break;
            }

            Debug.Log("PackagingSequence: Getting prescription container from pool.", this);
            instantiatedPrescriptionContainer = PoolingManager.Instance.GetPooledObject(prescriptionContainerPrefab);
            if (instantiatedPrescriptionContainer != null)
            {
                instantiatedPrescriptionContainer.transform.SetPositionAndRotation(pillRepackagePoint.position, Quaternion.identity);
                instantiatedPrescriptionContainer.SetActive(true);
                Debug.Log($"PackagingSequence: Prescription container active at {instantiatedPrescriptionContainer.transform.position}.", this);

                Vector3 finalContainerCenter = instantiatedPrescriptionContainer.transform.position;
                // Example: finalContainerCenter += Vector3.up * 0.05f; // Small offset up

                Debug.Log($"PackagingSequence: Moving {countedPills.Count} pills directly into the prescription container.", this);
                foreach (GameObject pillGO in countedPills)
                {
                     Rigidbody rb = pillGO.GetComponent<Rigidbody>();
                     Collider pillCollider = pillGO.GetComponent<Collider>();

                     if (rb != null) rb.isKinematic = true;
                     if (pillCollider != null) pillCollider.enabled = false;

                    StartCoroutine(MoveGameObjectSmoothly(pillGO, finalContainerCenter + UnityEngine.Random.insideUnitSphere * packagingScatterRadius, 1.0f, false)); // Pass false
                }
            }
            else
            {
                Debug.LogError("PillCraftingMinigame: Failed to get prescription container from pool! Cannot complete packaging visually. Signalling internal failure.", this);
                minigameSuccessStatus = false; // Inherited from base
            }

            Debug.Log("PackagingSequence: Waiting for packaging animation duration...", this);
            yield return new WaitForSeconds(1.0f); // Wait for packaging animation duration

            // --- MODIFIED: Call base class's EndMinigame to signal completion ---
            // Minigame is considered naturally ended (not aborted) when this sequence finishes.
            // The minigameSuccessStatus was set in OnFinishCountingClicked or at the start of this sequence.
            EndMinigame(false); // Call base EndMinigame (it wasn't aborted externally)
            // --------------------------------------------------------------------

            Debug.Log("PackagingSequence: Coroutine finished.", this);
        }

        /// <summary>
        /// Coroutine to smoothly move a GameObject using Rigidbody.MovePosition.
        /// Makes object kinematic and disables collider during the move.
        /// Re-enables physics and collider after move if requested.
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

            rb.isKinematic = true; // Ensure kinematic during the move
            if (col != null) col.enabled = false; // Disable collider during the move
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;


            while (timer < duration)
            {
                // Check if the game object has been destroyed or pooled during the move
                if (go == null) { yield break; }

                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                rb.MovePosition(Vector3.Lerp(startPos, targetPos, t));
                yield return null;
            }

            // Ensure it reaches the exact target position after the loop
             if (go != null) rb.MovePosition(targetPos);
             else yield break; // Check again if destroyed


            // Re-enable physics/collider if requested
            if (reEnablePhysicsAfter)
            {
                 if (rb != null) rb.isKinematic = false;
                 if (col != null) col.enabled = true;
            }
             else
             {
                 // If not re-enabling physics, ensure they stay kinematic/collider off
                 if (rb != null) rb.isKinematic = true;
                 if (col != null) col.enabled = false;
             }
        }

         // The cleanup logic has been moved into this method, which is called by base.EndMinigame(bool).
        /* // REMOVED: CleanupMinigameAssets method
        private void CleanupMinigameAssets()
        {
            Debug.Log("PillCraftingMinigame: Cleaning up instantiated (pooled) assets.", this);

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

            foreach (GameObject pillGO in instantiatedPills.ToList())
            {
                if (pillGO != null)
                {
                     Rigidbody rb = pillGO.GetComponent<Rigidbody>();
                     if (rb != null) rb.isKinematic = true;
                     Collider col = pillGO.GetComponent<Collider>();
                     if (col != null) col.enabled = false;

                    PoolingManager.Instance.ReturnPooledObject(pillGO);
                }
            }
            instantiatedPills.Clear();
            discardedPills.Clear();
            countedPills.Clear();

            if (countingUIRoot != null) countingUIRoot.SetActive(false);
        }
        */
    }
}