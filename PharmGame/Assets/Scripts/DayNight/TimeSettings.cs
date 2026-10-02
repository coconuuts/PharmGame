using UnityEngine;

[CreateAssetMenu(fileName = "TimeSettings", menuName = "TimeSettings")]
public class TimeSettings : ScriptableObject {
    public float timeMultiplier = 60;
    public float startHour = 12;
    public float sunriseHour = 6;
    public float sunsetHour = 18;
}