namespace NavPathfinder.Sdk.Helpers;

public static class AgentBudget
{
    /// <summary>
    /// Returns target unit count given pipeline pressure, current count, max cap, and morale.
    /// </summary>
    public static int Allocate(float pressure, int currentCount, int maxCount, float morale)
    {
        if (currentCount <= 0) return 0;
        if (pressure <= 0f) return Math.Min(currentCount, maxCount);

        float keepFraction = (1f - pressure) + morale * pressure * 0.3f;
        keepFraction = Math.Clamp(keepFraction, 0.1f, 1f);
        return Math.Clamp((int)(maxCount * keepFraction), 1, maxCount);
    }
}
