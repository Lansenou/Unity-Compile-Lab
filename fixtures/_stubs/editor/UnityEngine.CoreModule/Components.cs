// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

using System.Collections;

namespace UnityEngine
{
    /// <summary>Base class for everything attached to a GameObject.</summary>
    public class Component : Object
    {
        public Transform transform => throw Native.Unavailable();

        public GameObject gameObject => throw Native.Unavailable();

        public string tag { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public T GetComponent<T>() => throw Native.Unavailable();

        public bool TryGetComponent<T>(out T component) => throw Native.Unavailable();

        public T GetComponentInChildren<T>() => throw Native.Unavailable();

        public T[] GetComponents<T>() => throw Native.Unavailable();

        public bool CompareTag(string tag) => throw Native.Unavailable();
    }

    /// <summary>A component that can be enabled or disabled.</summary>
    public class Behaviour : Component
    {
        public bool enabled { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public bool isActiveAndEnabled => throw Native.Unavailable();
    }

    /// <summary>Base class for scripts attached to GameObjects.</summary>
    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => throw Native.Unavailable();

        public void StopCoroutine(Coroutine routine) => throw Native.Unavailable();

        public void StopAllCoroutines() => throw Native.Unavailable();

        public void Invoke(string methodName, float time) => throw Native.Unavailable();

        public void CancelInvoke() => throw Native.Unavailable();

        public static void print(object message) => throw Native.Unavailable();
    }

    /// <summary>Handle to a running coroutine.</summary>
    public sealed class Coroutine
    {
        private Coroutine()
        {
        }
    }

    /// <summary>Suspends a coroutine for the given number of seconds.</summary>
    public sealed class WaitForSeconds
    {
        public WaitForSeconds(float seconds) => throw Native.Unavailable();
    }

    /// <summary>Position, rotation and scale of an object.</summary>
    public class Transform : Component, IEnumerable
    {
        protected Transform()
        {
        }

        public Vector3 position { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Vector3 localPosition { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Quaternion rotation { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Vector3 localScale { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public Vector3 forward => throw Native.Unavailable();

        public Vector3 right => throw Native.Unavailable();

        public Vector3 up => throw Native.Unavailable();

        public Transform parent { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public int childCount => throw Native.Unavailable();

        public void Translate(Vector3 translation) => throw Native.Unavailable();

        public void Rotate(Vector3 eulers) => throw Native.Unavailable();

        public void LookAt(Transform target) => throw Native.Unavailable();

        public void SetParent(Transform parent) => throw Native.Unavailable();

        public Transform GetChild(int index) => throw Native.Unavailable();

        public IEnumerator GetEnumerator() => throw Native.Unavailable();
    }

    /// <summary>An entity in a Unity scene.</summary>
    public sealed class GameObject : Object
    {
        public GameObject() => throw Native.Unavailable();

        public GameObject(string name) => throw Native.Unavailable();

        public Transform transform => throw Native.Unavailable();

        public bool activeSelf => throw Native.Unavailable();

        public bool activeInHierarchy => throw Native.Unavailable();

        public string tag { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public int layer { get => throw Native.Unavailable(); set => throw Native.Unavailable(); }

        public void SetActive(bool value) => throw Native.Unavailable();

        public T AddComponent<T>() where T : Component => throw Native.Unavailable();

        public T GetComponent<T>() => throw Native.Unavailable();

        public bool TryGetComponent<T>(out T component) => throw Native.Unavailable();

        public bool CompareTag(string tag) => throw Native.Unavailable();

        public static GameObject Find(string name) => throw Native.Unavailable();

        public static GameObject FindWithTag(string tag) => throw Native.Unavailable();
    }

    /// <summary>A data container asset.</summary>
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => throw Native.Unavailable();
    }
}
