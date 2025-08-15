using UnityEngine;

[DisallowMultipleComponent]
public class CopyWeaponScript : MonoBehaviour
{
    public enum WeaponType { Knife, SimpleShooter }
    public WeaponType weaponToCopy = WeaponType.Knife;

    [Tooltip("Prefab or GameObject containing the script to copy")]
    public GameObject sourceObject;

    private void Awake()
    {
        if (sourceObject == null)
        {
            Debug.LogWarning($"{nameof(CopyWeaponScript)}: No source object assigned.");
            return;
        }

        switch (weaponToCopy)
        {
            case WeaponType.Knife:
                CopyComponent<Knife>(sourceObject, gameObject);
                break;
            case WeaponType.SimpleShooter:
                CopyComponent<SimpleShooter>(sourceObject, gameObject);
                break;
        }
    }

    private void FixedUpdate()
    {
        switch (weaponToCopy)
        {
            case WeaponType.Knife:
                CopyComponent<Knife>(sourceObject, gameObject);
                break;
            case WeaponType.SimpleShooter:
                CopyComponent<SimpleShooter>(sourceObject, gameObject);
                break;
        }
    }

    private T CopyComponent<T>(GameObject source, GameObject destination) where T : Component
    {
        T sourceComp = source.GetComponent<T>();
        if (sourceComp == null)
        {
            Debug.LogWarning($"{nameof(CopyWeaponScript)}: Source object has no {typeof(T).Name} component.");
            return null;
        }

        T destComp = destination.AddComponent<T>();
        System.Type type = typeof(T);
        var fields = type.GetFields(System.Reflection.BindingFlags.Public |
                                    System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance);

        foreach (var field in fields)
        {
            field.SetValue(destComp, field.GetValue(sourceComp));
        }

        return destComp;
    }
}
