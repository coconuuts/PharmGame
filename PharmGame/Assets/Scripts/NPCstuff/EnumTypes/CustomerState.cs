namespace Game.NPC
{
    /// <summary>
    /// Defines the possible states for a customer NPC (primarily for CustomerManager/Event compatibility during migration).
    /// 
    public enum CustomerState
    {
        Inactive,
        LookingToShop,
        Entering,
        Browse,
        MovingToRegister,
        WaitingAtRegister,
        Queue,
        SecondaryQueue,
        TransactionActive,
        Exiting,
        ReturningToPool,
    }
}
