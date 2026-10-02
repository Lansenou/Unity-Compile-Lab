using NUnit.Framework;
using UnityEngine;

public class MyBehaviour : MonoBehaviour { public int Number = 42; }
public class MySo : ScriptableObject { public int Number = 42; }

[TestFixture]
public class EngineTests
{
    GameObject instance;
    Object asset;

    [SetUp] public void SetUp() { instance = null; asset = null; }
    [TearDown] public void TearDown()
    {
        if (instance != null) Object.DestroyImmediate(instance);
        if (asset != null) Object.DestroyImmediate(asset);
    }

    [Test] public void T01_BuiltinComponent()
    {
        instance = new GameObject("a");
        Assert.That(instance.AddComponent<BoxCollider>(), Is.Not.Null);
    }
    [Test] public void T02_DynamicBehaviour()
    {
        instance = new GameObject("a");
        var component = instance.AddComponent<MyBehaviour>();
        Assert.That(component, Is.Not.Null);
        Assert.That(component.Number, Is.EqualTo(42));
    }
    [Test] public void T03_Logging()
    {
        Debug.Log("SPIKE_LOG");
        Debug.LogError("SPIKE_LOG_ERROR");
        Assert.That(true, Is.True); // Captured player log must also contain both markers.
    }
    [Test] public void T04_Quaternion()
    {
        var actual = Quaternion.Euler(30, 45, 60) * Vector3.forward;
        Assert.That(actual.x, Is.EqualTo(Oracle.X).Within(0.00001f));
        Assert.That(actual.y, Is.EqualTo(Oracle.Y).Within(0.00001f));
        Assert.That(actual.z, Is.EqualTo(Oracle.Z).Within(0.00001f));
    }
    [Test] public void T05_PhysicsRaycast()
    {
        instance = new GameObject("a");
        instance.AddComponent<BoxCollider>();
        Physics.SyncTransforms();
        Assert.That(Physics.Raycast(new Vector3(0, 0, -5), Vector3.forward, out var hit, 10), Is.True);
        Assert.That(hit.collider.gameObject, Is.SameAs(instance));
    }
    [Test] public void T06_DynamicScriptableObject()
    {
        var created = ScriptableObject.CreateInstance<MySo>();
        asset = created;
        Assert.That(created, Is.Not.Null);
        Assert.That(created.Number, Is.EqualTo(42));
    }
    [Test] public void T07_TexturePixels()
    {
        var texture = new Texture2D(4, 4);
        asset = texture;
        texture.SetPixel(1, 2, Color.red);
        Assert.That(texture.GetPixel(1, 2), Is.EqualTo(Color.red));
    }
    [Test] public void T08_DataPath()
    {
        Assert.That(Application.dataPath, Is.Not.Empty);
        Debug.Log("SPIKE_DATA_PATH " + Application.dataPath);
    }
}
