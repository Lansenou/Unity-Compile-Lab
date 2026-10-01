// Original stand-in for part of the Unity API, written for the ucl fixtures. Not Unity code. Apache-2.0.

using System.Collections;

namespace UnityEngine
{
    /// <summary>Base class for everything attached to a GameObject.</summary>
    public class Component : Object
    {
        public Transform transform => throw null;

        public GameObject gameObject => throw null;

        public string tag { get => throw null; set => throw null; }

        public T GetComponent<T>() => throw null;

        public bool TryGetComponent<T>(out T component) => throw null;

        public T GetComponentInChildren<T>() => throw null;

        public T[] GetComponents<T>() => throw null;

        public bool CompareTag(string tag) => throw null;
    }

    /// <summary>A component that can be enabled or disabled.</summary>
    public class Behaviour : Component
    {
        public bool enabled { get => throw null; set => throw null; }

        public bool isActiveAndEnabled => throw null;
    }

    /// <summary>Base class for scripts attached to GameObjects.</summary>
    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => throw null;

        public void StopCoroutine(Coroutine routine) => throw null;

        public void StopAllCoroutines() => throw null;

        public void Invoke(string methodName, float time) => throw null;

        public void CancelInvoke() => throw null;

        public static void print(object message) => throw null;
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
        public WaitForSeconds(float seconds) => throw null;
    }

    /// <summary>Position, rotation and scale of an object.</summary>
    public class Transform : Component, IEnumerable
    {
        protected Transform()
        {
        }

        public Vector3 position { get => throw null; set => throw null; }

        public Vector3 localPosition { get => throw null; set => throw null; }

        public Quaternion rotation { get => throw null; set => throw null; }

        public Vector3 localScale { get => throw null; set => throw null; }

        public Vector3 forward => throw null;

        public Vector3 right => throw null;

        public Vector3 up => throw null;

        public Transform parent { get => throw null; set => throw null; }

        public int childCount => throw null;

        public void Translate(Vector3 translation) => throw null;

        public void Rotate(Vector3 eulers) => throw null;

        public void LookAt(Transform target) => throw null;

        public void SetParent(Transform parent) => throw null;

        public Transform GetChild(int index) => throw null;

        public IEnumerator GetEnumerator() => throw null;
    }

    /// <summary>An entity in a Unity scene.</summary>
    public sealed class GameObject : Object
    {
        public GameObject() => throw null;

        public GameObject(string name) => throw null;

        public Transform transform => throw null;

        public bool activeSelf => throw null;

        public bool activeInHierarchy => throw null;

        public string tag { get => throw null; set => throw null; }

        public int layer { get => throw null; set => throw null; }

        public void SetActive(bool value) => throw null;

        public T AddComponent<T>() where T : Component => throw null;

        public T GetComponent<T>() => throw null;

        public bool TryGetComponent<T>(out T component) => throw null;

        public bool CompareTag(string tag) => throw null;

        public static GameObject Find(string name) => throw null;

        public static GameObject FindWithTag(string tag) => throw null;
    }

    /// <summary>A data container asset.</summary>
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => throw null;
    }
}
