// // Assuming this file is located within your Systems.Interaction namespace folder
// // e.g., Systems/Interaction/OpenCraftingResponse.cs
// using Systems.Inventory; // Needed for CraftingStation

// namespace Systems.Interaction
// {
//     /// <summary>
//     /// Response indicating that the player is interacting with a crafting station.
//     /// Contains a reference to the CraftingStation component.
//     /// </summary>
//     public class OpenCraftingResponse : InteractionResponse
//     {
//         public CraftingStation CraftingStationComponent { get; }

//         /// <summary>
//         /// Constructor for OpenCraftingResponse.
//         /// </summary>
//         /// <param name="craftingStation">The CraftingStation component that was interacted with.</param>
//         public OpenCraftingResponse(CraftingStation craftingStation) // : base(...) // Add if using a base constructor in InteractionResponse
//         {
//             CraftingStationComponent = craftingStation;
//         }
//     }
// }