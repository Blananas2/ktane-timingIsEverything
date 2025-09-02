using KModkit;

public static class TimeRemaining
{
    public static void FromModule(KMBombModule module, float time)
    {
        module.GetComponent("BombComponent").GetValue<object>("Bomb").CallMethod<object>("GetTimer").SetValue("TimeRemaining", time);
    }

    public static void FromModule(KMNeedyModule module, float time)
    {
        module.GetComponent("BombComponent").GetValue<object>("Bomb").CallMethod<object>("GetTimer").SetValue("TimeRemaining", time);
    }
}
