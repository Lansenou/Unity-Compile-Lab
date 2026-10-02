# `ucl test` scope: public source audit

The README and docs/test.md scope table was checked against UnityCsReference **6000.3.25f1**,
commit `2f6cef60096cf50741d933becf101bf3186719dd`. The links pin that revision. A managed wrapper
still needs Unity if it calls an extern/native binding. These are member-specific examples, not
claims that every operation on a type runs here.

The original [ApiScopeTests](../fixtures/test-editmode/Assets/Tests/EditMode/ApiScopeTests.cs),
WalletTests and EngineTests back every row with stub-editor cases. The stand-ins encode managed
versus native calls; they are not an accuracy oracle for closed native math. ucl uses the user's
actual editor DLLs and does not substitute these fixture stand-ins or add production API shims.

1. **Plain C# / engine objects.** WalletTests exercise ordinary classes without Unity calls.
   `System.*` is the framework, outside UnityCsReference; the code path and referenced API still matter.
   [GameObject](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/Scripting/GameObject.bindings.cs)
   construction calls Internal_CreateGameObject; Component/Transform operations and MonoBehaviour's
   constructor check have native bindings. Existing EngineTests reach construction through a helper.
2. **Vectors / object creation.** Vector2.cs, Vector3.cs and Vector4.cs in
   [Math](https://github.com/Unity-Technologies/UnityCsReference/tree/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/Math)
   implement arithmetic and the listed Vector3 methods in C#.
   [ScriptableObject](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/Scripting/ScriptableObject.bindings.cs)
   creation and UnityEngineObject.bindings.cs Instantiate/Destroy reach native functions.
   Cases: Managed_vectors / Native_scriptable_creation.
3. **Mathf.** [Mathf.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/Math/Mathf.cs)
   implements Clamp, Lerp, Approximately, Sin (System.Math) and ClosestPowerOfTwo in managed code.
   [Math.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/Math/Math.bindings.cs)
   declares PerlinNoise and GammaToLinearSpace/LinearToGammaSpace extern.
   Cases: Managed_math / Native_perlin. ClosestPowerOfTwo was corrected from the proposed native column.
4. **Value structs / graphics.** Color.cs and Color32.cs in Math, and Rect.cs, RectInt.cs and Bounds.cs
   in [Geometry](https://github.com/Unity-Technologies/UnityCsReference/tree/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/Geometry)
   have C# constructors/value operations; Bounds.Intersects is C#. Bounds.Contains/SqrDistance/ClosestPoint
   in Math.bindings.cs are native, as are Color.linear/gamma via Mathf color-space conversions.
   Creation/use bindings for Texture/RenderTexture/Mesh are in
   [Graphics](https://github.com/Unity-Technologies/UnityCsReference/tree/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/Graphics),
   Material/Shader are in Runtime/Export/Shaders, and Sprite creation is in
   Runtime/2D/Common/ScriptBindings/Sprites.bindings.cs. Cases: Managed_values / Native_texture.
5. **Quaternion.** Quaternion.cs identity and multiplication operators have C# bodies.
   Math.bindings.cs Euler's Internal_FromEulerRad, LookRotation, Slerp, Inverse and AngleAxis use native
   bindings. Cases: Managed_quaternion / Native_quaternion. The old original stub incorrectly made
   identity/multiplication native; its managed-rotation fixture was red before correction.
6. **Matrix4x4.** Matrix4x4.cs multiplication and MultiplyPoint3x4 operate on fields in C#.
   Math.bindings.cs TRS, inverse and Perspective reach extern functions.
   Cases: Managed_matrix / Native_matrix. No native algorithm is emulated.
7. **Metadata / runtime services.** SerializeField and Range attributes, enums and value structs need
   no engine call by themselves. DebugLogHandler.cs calls Internal_Log/Internal_LogException, bound in
   [Debug.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/Debug/Debug.bindings.cs).
   Application/Application.bindings.cs, Time/Time.bindings.cs and Resources/Resources.bindings.cs
   declare the relevant engine operations extern. LogAssert instead requires the test framework's
   [LogScope.Current](https://github.com/needle-mirror/com.unity.test-framework/blob/117ede6d83ffc332b51c90d40df38451b914aabf/UnityEngine.TestRunner/Assertions/LogScope/LogScope.cs),
   which throws without a scope. Cases: Managed_attributes_and_enums / Native_logging,
   plus the existing Expects_a_log_message and helper-scope regression.
8. **Editor services.** AssetDatabase's Refresh uses native bindings in
   [AssetDatabase.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Modules/AssetDatabase/Editor/ScriptBindings/AssetDatabase.bindings.cs).
   Editor/Mono/EditorPrefs.bindings.cs and EditorUtility.bindings.cs declare their engine operations
   extern/NativeMethod. Case: Native_editor_api. Plain Editor attributes are still metadata.
9. **Allocation / scheduling / engine systems.**
   [NativeArray.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/2f6cef60096cf50741d933becf101bf3186719dd/Runtime/Export/NativeArray/NativeArray.cs)
   allocation calls native MallocTracked in Runtime/Export/Unsafe/UnsafeUtility.bindings.cs.
   Runtime/Jobs/ScriptBindings and Runtime/Export/Burst/BurstCompilerService.bindings.cs provide native
   scheduling/compilation; an IJob.Execute body or Burst attribute can itself be plain managed code.
   Physics.bindings.cs, Camera.bindings.cs and Graphics.bindings.cs bind engine operations.
   Case: Native_physics. Reading an enum or constructing an unallocated value is not allocation.

Actual CLI run of the original fixture: **45 cases: 25 passed, 1 failed, 2 skipped, 1 ignored,
11 needs-unity, 5 unity-only, exit 1**. The one failure is intentionally wrong arithmetic;
these counts describe this fixture, not the share that will run in another project.
