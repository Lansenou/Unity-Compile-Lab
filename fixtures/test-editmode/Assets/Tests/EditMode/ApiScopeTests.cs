using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Original cases for the README scope table; see docs/test.md, API source audit.
    public class ApiScopeTests
    {
        [SerializeField, UnityEngine.Range(0, 1)] private float value = 0.5f;
        public class ScopeAsset : ScriptableObject { }

        [Test] public void Managed_vectors() => Assert.AreEqual(5f, Vector3.Distance(Vector3.zero, new Vector3(3, 4, 0)));
        [Test] public void Native_scriptable_creation() => ScriptableObject.CreateInstance<ScopeAsset>();
        [Test] public void Managed_math()
        {
            Assert.AreEqual(1f, Mathf.Clamp(2f, 0f, 1f));
            Assert.AreEqual(2f, Mathf.Lerp(0f, 4f, 0.5f));
            Assert.IsTrue(Mathf.Approximately(1f, 1f));
            Assert.AreEqual(0f, Mathf.Sin(0f));
            Assert.AreEqual(8, Mathf.ClosestPowerOfTwo(7));
        }
        [Test] public void Native_perlin() => Mathf.PerlinNoise(0f, 0f);
        [Test] public void Managed_values()
        {
            Assert.AreEqual(1f, new Color(1f, 0f, 0f).r);
            Assert.AreEqual((byte)255, new Color32(255, 0, 0, 255).r);
            Assert.AreEqual(3f, new Rect(1, 2, 3, 4).width);
            Assert.AreEqual(3, new RectInt(1, 2, 3, 4).width);
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            Assert.IsTrue(bounds.Intersects(bounds));
        }
        [Test] public void Native_texture() => new Texture2D(1, 1);
        [Test] public void Managed_quaternion()
        {
            Assert.AreEqual(Vector3.right, Quaternion.identity * Vector3.right);
            Assert.AreEqual(Vector3.left, new Quaternion { z = 1f } * Vector3.right);
            Assert.AreEqual(Quaternion.identity, Quaternion.identity * Quaternion.identity);
        }
        [Test] public void Native_quaternion() => Quaternion.Euler(0f, 90f, 0f);
        [Test] public void Managed_matrix()
        {
            var translated = Matrix4x4.identity;
            translated.m03 = 2f;
            Assert.AreEqual(new Vector3(3, 0, 0), (translated * Matrix4x4.identity).MultiplyPoint3x4(Vector3.right));
        }
        [Test] public void Native_matrix() => Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
        [Test] public void Managed_attributes_and_enums()
        {
            Assert.AreEqual(0.5f, value);
            Assert.AreEqual(0, (int)HideFlags.None);
        }
        [Test] public void Native_logging() => Debug.Log("original scope fixture");
        [Test] public void Native_editor_api() => UnityEditor.AssetDatabase.Refresh();
        [Test] public void Native_physics() => Physics.Raycast(Vector3.zero, Vector3.right);
    }
}
