using UnityEngine;

public enum Rarity { Common, Uncommon, Rare, Legendary }

[System.Serializable]
public struct RarityWeights
{
    [Min(0f)] public float common;
    [Min(0f)] public float uncommon;
    [Min(0f)] public float rare;
    [Min(0f)] public float legendary;

    public Rarity Roll(System.Random rng)
    {
        float c = Mathf.Max(0f, common);
        float u = Mathf.Max(0f, uncommon);
        float r = Mathf.Max(0f, rare);
        float l = Mathf.Max(0f, legendary);
        float total = c + u + r + l;
        if (total <= 0f) return Rarity.Common;

        double roll = rng.NextDouble() * total;
        if (roll < c) return Rarity.Common; roll -= c;
        if (roll < u) return Rarity.Uncommon; roll -= u;
        if (roll < r) return Rarity.Rare;
        return Rarity.Legendary;
    }
}
