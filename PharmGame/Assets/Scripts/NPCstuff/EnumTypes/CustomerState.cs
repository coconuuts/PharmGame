namespace Game.NPC
{
    /// <summary>s
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
        WaitingInLine,
        TransactionActive,
        Exiting,
        ReturningToPool,

        LookToPrescription,
        PrescriptionEntering, 
        WaitingForPrescription, 
        WaitingForDelivery,
        PrescriptionQueue,
    }
}
