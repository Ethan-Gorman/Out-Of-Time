using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Hooks the small particle effects onto things when a scene loads, without touching their scripts or prefabs
// (the same pattern as InteractOutline):
//  - FlameEmbers on every flame (bonfire, campfires, clay oven, furnace, torches, lanterns), each with its own
//    amount, size and colour; not on the electric factory lamps
//  - FxSparkle on crystals (cyan) and Time Coins (gold)
//  - DeathPuff on skeletons, clockwork guards and the boar
//  - a green and gold burst of sparks around the player whenever the Clock gains time
public static class FxBootstrap
{
    static readonly Color GainGreen = new Color(0.45f, 1f, 0.5f, 1f);
    static readonly Color GainGold = new Color(1f, 0.86f, 0.3f, 1f);

    static Transform player;

    // Statics survive between play sessions when domain reload is off, so reset on play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        player = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Clock clears its event in SubsystemRegistration, which runs before this
        Clock.TimeChanged -= OnTimeChanged;
        Clock.TimeChanged += OnTimeChanged;
        AddToAll();
        FxParticles.Ensure().StartCoroutine(RescanSoon());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;
        AddToAll();
        FxParticles.Ensure().StartCoroutine(RescanSoon());
    }

    // Level scripts sometimes build props in Start; one more look a moment later catches those too
    static IEnumerator RescanSoon()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        AddToAll();
    }

    static void AddToAll()
    {
        const FindObjectsInactive all = FindObjectsInactive.Include;
        const FindObjectsSortMode any = FindObjectsSortMode.None;
        foreach (var c in Object.FindObjectsByType<TorchFlicker>(all, any))
            if (FireKind(c, out var kind) && c.GetComponent<FlameEmbers>() == null)
                c.gameObject.AddComponent<FlameEmbers>().kind = kind;
        foreach (var c in Object.FindObjectsByType<MagneticStone>(all, any)) FxSparkle.Add(c, FxSparkle.CrystalColor);
        foreach (var c in Object.FindObjectsByType<TimeCrystal>(all, any)) FxSparkle.Add(c, FxSparkle.CrystalColor);
        foreach (var c in Object.FindObjectsByType<TimeCoin>(all, any)) FxSparkle.Add(c, FxSparkle.CoinColor);
        foreach (var c in Object.FindObjectsByType<SkeletonEnemy>(all, any)) DeathPuff.Add(c);
        foreach (var c in Object.FindObjectsByType<ClockworkGuard>(all, any)) DeathPuff.Add(c);
        foreach (var c in Object.FindObjectsByType<WildBoar>(all, any)) DeathPuff.Add(c);
    }

    // Every flame that shares TorchFlicker gets embers with its own look (bonfire, campfire, oven, furnace, torch,
    // lantern), told apart by name. The Steampunk factory lamps also use TorchFlicker for a buzzing bulb, so electric
    // lights (FactoryLamp prefab, or a "Bulb" child from SteampunkSceneBuilder) get none
    public static bool FireKind(TorchFlicker torch, out FlameEmbers.Kind kind)
    {
        string name = torch.name;
        kind = FlameEmbers.Kind.Torch;
        if (Has(name, "FactoryLamp") || torch.transform.Find("Bulb") != null) return false;
        if (Has(name, "Bonfire")) kind = FlameEmbers.Kind.Bonfire;
        else if (Has(name, "Campfire")) kind = FlameEmbers.Kind.Campfire;
        else if (Has(name, "Oven")) kind = FlameEmbers.Kind.Oven;
        else if (Has(name, "Furnace")) kind = FlameEmbers.Kind.Furnace;
        else if (Has(name, "Lantern")) kind = FlameEmbers.Kind.Lantern;
        return true;
    }

    // True for the big camp fires, which get the extra flame and smoke effects too
    public static bool IsBonfire(TorchFlicker torch) =>
        FireKind(torch, out var kind) && (kind == FlameEmbers.Kind.Bonfire || kind == FlameEmbers.Kind.Campfire);

    static bool Has(string name, string part) => name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;

    static Transform Player()
    {
        if (MedievalInteractor.Current != null)
            return MedievalInteractor.Current.transform;
        if (player == null)
        {
            var movement = Object.FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
                player = movement.transform;
        }
        return player;
    }

    // Only gains (pickups, kills): damage already has its own red flash. Real time, since it is feedback
    // and has to read even while the player stands still
    static void OnTimeChanged(float seconds)
    {
        if (seconds <= 0f)
            return;
        Transform target = Player();
        if (target == null)
            return;

        Vector3 centre = target.position + Vector3.up * 0.5f;
        int count = Mathf.Clamp(8 + Mathf.RoundToInt(seconds), 8, 22);
        for (int i = 0; i < count; i++)
        {
            Vector2 flat = Random.insideUnitCircle;
            Color color = Random.value < 0.55f ? GainGreen : GainGold;
            Color fade = color;
            fade.a = 0f;
            float roll = Random.value;
            FxParticles.Spawn(new FxParticles.Spec
            {
                shape = roll < 0.25f ? FxParticles.Shape.Star : roll < 0.6f ? FxParticles.Shape.BigDot : FxParticles.Shape.Dot,
                position = centre + new Vector3(flat.x, 0f, flat.y) * 0.35f,
                velocity = new Vector3(flat.x * 0.4f, Random.Range(1.4f, 2.4f), flat.y * 0.4f),
                life = Random.Range(0.6f, 0.9f),
                startColor = color,
                endColor = fade,
                startSize = 1f,
                endSize = 1f,
                gravity = 2.5f,
                drag = 1f,
                unscaled = true,
            });
        }
    }
}
